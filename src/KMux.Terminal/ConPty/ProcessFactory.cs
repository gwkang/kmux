using System.Collections;
using System.Runtime.InteropServices;
using System.Text;
using KMux.Core.Models;
using static KMux.Terminal.ConPty.ConPtyNativeMethods;

namespace KMux.Terminal.ConPty;

internal static unsafe class ProcessFactory
{
    public static PROCESS_INFORMATION Spawn(ShellProfile profile, PseudoConsole pty)
    {
        var cmdLine = new StringBuilder(
            string.IsNullOrWhiteSpace(profile.Arguments)
                ? profile.Executable
                : $"{profile.Executable} {profile.Arguments}");

        // ── Step 1: allocate PROC_THREAD_ATTRIBUTE_LIST ──────────────────────
        var attrListSize = IntPtr.Zero;
        InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref attrListSize);

        var pAttrList = Marshal.AllocHGlobal(attrListSize);
        var envBlock  = BuildEnvironmentBlock(profile.EnvironmentVariables);
        try
        {
            if (!InitializeProcThreadAttributeList(pAttrList, 1, 0, ref attrListSize))
                Throw("InitializeProcThreadAttributeList");

            // ── Step 2: attach ConPTY handle ─────────────────────────────────
            // Pass the HPCON value directly as lpValue — Windows stores it as-is
            // without dereferencing (matches the official EchoCon C++ sample).
            if (!UpdateProcThreadAttribute(
                    pAttrList, 0,
                    (IntPtr)PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE,
                    pty.Handle,              // HPCON value passed directly
                    (IntPtr)IntPtr.Size,
                    IntPtr.Zero, IntPtr.Zero))
                Throw("UpdateProcThreadAttribute");

            // ── Step 3: build STARTUPINFOEX on the stack and pin it ──────────
            var si = new STARTUPINFOEX();
            si.StartupInfo.cb = sizeof(STARTUPINFOEX);
            si.lpAttributeList = pAttrList;

            // ── Step 4: launch process ───────────────────────────────────────
            var creationFlags = EXTENDED_STARTUPINFO_PRESENT;
            if (envBlock != IntPtr.Zero) creationFlags |= CREATE_UNICODE_ENVIRONMENT;

            PROCESS_INFORMATION pi;
            if (!CreateProcess(
                    null,
                    cmdLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    creationFlags,
                    envBlock,
                    string.IsNullOrWhiteSpace(profile.WorkingDir) ? null : profile.WorkingDir,
                    (IntPtr)(&si),
                    out pi))
                Throw("CreateProcess");

            return pi;
        }
        finally
        {
            DeleteProcThreadAttributeList(pAttrList);
            Marshal.FreeHGlobal(pAttrList);
            if (envBlock != IntPtr.Zero) Marshal.FreeHGlobal(envBlock);
        }
    }

    /// <summary>
    /// Builds a null-terminated Unicode environment block for CreateProcess.
    /// Returns IntPtr.Zero if <paramref name="extra"/> is empty (caller inherits parent env).
    /// </summary>
    private static IntPtr BuildEnvironmentBlock(Dictionary<string, string> extra)
    {
        if (extra.Count == 0) return IntPtr.Zero;

        // Start from current process environment and merge extras
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
            env[(string)entry.Key] = (string?)entry.Value ?? "";
        foreach (var kv in extra)
            env[kv.Key] = kv.Value;

        // Build: KEY=VALUE\0 ... KEY=VALUE\0\0
        var sb = new StringBuilder();
        foreach (var kv in env)
        {
            sb.Append(kv.Key);
            sb.Append('=');
            sb.Append(kv.Value);
            sb.Append('\0');
        }
        sb.Append('\0');

        var bytes = Encoding.Unicode.GetBytes(sb.ToString());
        var ptr   = Marshal.AllocHGlobal(bytes.Length);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        return ptr;
    }

    private static void Throw(string api)
    {
        var err = Marshal.GetLastWin32Error();
        throw new InvalidOperationException(
            $"{api} failed with Win32 error {err} (0x{err:X})");
    }
}
