using System.Runtime.InteropServices;

namespace KMux.Terminal.ConPty;

/// <summary>
/// Reads runtime state (e.g. current working directory) from a live Win32 process
/// by walking its PEB via NtQueryInformationProcess + ReadProcessMemory.
/// Only works on x64 — which matches the project's PlatformTarget.
/// </summary>
public static class ProcessUtils
{
    // x64 offsets (verified against public ntdll / ntifs headers)
    private const int PEB_PROCESS_PARAMS_OFFSET    = 0x20; // PEB.ProcessParameters (Ptr64)
    private const int PP_CURDIR_DOSPATH_OFFSET     = 0x38; // RTL_USER_PROCESS_PARAMETERS.CurrentDirectory.DosPath
    private const int UNICODE_STRING_BUFFER_OFFSET = 0x08; // UNICODE_STRING.Buffer (past Length+MaxLen+4-byte pad)

    private const uint PROCESS_QUERY_INFORMATION = 0x0400;
    private const uint PROCESS_VM_READ           = 0x0010;

    /// <summary>
    /// Returns the shell process's current working directory by reading its PEB via Win32.
    /// </summary>
    public static string? GetCurrentDirectory(int shellPid)
    {
        if (shellPid <= 0) return null;

        var hProcess = OpenProcess(PROCESS_QUERY_INFORMATION | PROCESS_VM_READ, false, (uint)shellPid);
        if (hProcess == IntPtr.Zero) return null;
        try
        {
            // 1. Get PEB base address via ProcessBasicInformation (class 0)
            //    PROCESS_BASIC_INFORMATION on x64 is 6 × 8 = 48 bytes;
            //    PebBaseAddress is the second field (offset 8).
            int    pbiLen = 6 * IntPtr.Size;
            var    pbi    = Marshal.AllocHGlobal(pbiLen);
            IntPtr pebAddr;
            try
            {
                if (NtQueryInformationProcess(hProcess, 0, pbi, pbiLen, out _) != 0)
                    return null;
                pebAddr = Marshal.ReadIntPtr(pbi, IntPtr.Size);
            }
            finally { Marshal.FreeHGlobal(pbi); }

            // 2. Read PEB.ProcessParameters pointer
            var ppAddr = ReadRemotePtr(hProcess, pebAddr + PEB_PROCESS_PARAMS_OFFSET);
            if (ppAddr == IntPtr.Zero) return null;

            // 3. Read UNICODE_STRING.Length (bytes, not chars)
            var length = ReadRemoteUshort(hProcess, ppAddr + PP_CURDIR_DOSPATH_OFFSET);
            if (length == 0) return null;

            // 4. Read UNICODE_STRING.Buffer pointer
            var bufAddr = ReadRemotePtr(hProcess, ppAddr + PP_CURDIR_DOSPATH_OFFSET + UNICODE_STRING_BUFFER_OFFSET);
            if (bufAddr == IntPtr.Zero) return null;

            // 5. Read UTF-16LE string; strip trailing backslash (root paths like "C:\" keep it)
            var raw = ReadRemoteUnicodeString(hProcess, bufAddr, length);
            if (raw is null) return null;

            return raw.Length > 3 ? raw.TrimEnd('\\') : raw; // keep "C:\" intact
        }
        catch { return null; }
        finally { CloseHandle(hProcess); }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static IntPtr ReadRemotePtr(IntPtr hProcess, IntPtr address)
    {
        var buf = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            return ReadProcessMemory(hProcess, address, buf, (nint)IntPtr.Size, out _)
                ? Marshal.ReadIntPtr(buf)
                : IntPtr.Zero;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static ushort ReadRemoteUshort(IntPtr hProcess, IntPtr address)
    {
        var buf = Marshal.AllocHGlobal(2);
        try
        {
            return ReadProcessMemory(hProcess, address, buf, 2, out _)
                ? (ushort)Marshal.ReadInt16(buf)
                : (ushort)0;
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    private static string? ReadRemoteUnicodeString(IntPtr hProcess, IntPtr address, ushort byteLen)
    {
        var buf = Marshal.AllocHGlobal(byteLen);
        try
        {
            if (!ReadProcessMemory(hProcess, address, buf, byteLen, out _)) return null;
            return Marshal.PtrToStringUni(buf, byteLen / 2);
        }
        finally { Marshal.FreeHGlobal(buf); }
    }

    // ── P/Invoke ─────────────────────────────────────────────────────────────

    [DllImport("ntdll.dll")]
    private static extern int NtQueryInformationProcess(
        IntPtr ProcessHandle, int ProcessInformationClass,
        IntPtr ProcessInformation, int ProcessInformationLength, out int ReturnLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        IntPtr hProcess, IntPtr lpBaseAddress,
        IntPtr lpBuffer, nint nSize, out nint lpNumberOfBytesRead);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr hObject);
}
