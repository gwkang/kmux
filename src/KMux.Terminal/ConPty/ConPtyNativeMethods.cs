using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace KMux.Terminal.ConPty;

internal static class ConPtyNativeMethods
{
    // ── Pseudo Console ───────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    internal static extern int CreatePseudoConsole(
        COORD size,
        SafeFileHandle hInput,
        SafeFileHandle hOutput,
        uint dwFlags,
        out IntPtr phPC);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    internal static extern int ResizePseudoConsole(IntPtr hPC, COORD size);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    internal static extern void ClosePseudoConsole(IntPtr hPC);

    // ── Pipes ────────────────────────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CreatePipe(
        out SafeFileHandle hReadPipe,
        out SafeFileHandle hWritePipe,
        ref SECURITY_ATTRIBUTES lpPipeAttributes,
        uint nSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool SetHandleInformation(
        SafeFileHandle hObject,
        uint dwMask,
        uint dwFlags);

    internal const uint HANDLE_FLAG_INHERIT = 0x00000001;

    // ── Process Thread Attributes ────────────────────────────────────────────

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool InitializeProcThreadAttributeList(
        IntPtr lpAttributeList,
        int dwAttributeCount,
        int dwFlags,
        ref IntPtr lpSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool UpdateProcThreadAttribute(
        IntPtr lpAttributeList,
        uint dwFlags,
        IntPtr Attribute,
        IntPtr lpValue,
        IntPtr cbSize,
        IntPtr lpPreviousValue,
        IntPtr lpReturnSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

    internal const uint PROC_THREAD_ATTRIBUTE_PSEUDOCONSOLE = 0x00020016;

    // ── CreateProcess ─────────────────────────────────────────────────────────
    // lpStartupInfo is IntPtr so we can pass a pinned pointer without
    // the P/Invoke marshaler copying the struct (which would invalidate
    // the lpAttributeList pointer embedded inside it).

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern bool CreateProcess(
        string?       lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr  lpProcessAttributes,
        IntPtr  lpThreadAttributes,
        bool    bInheritHandles,
        uint    dwCreationFlags,
        IntPtr  lpEnvironment,
        string? lpCurrentDirectory,
        IntPtr  lpStartupInfo,           // raw pointer – no marshaling
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool GetExitCodeProcess(IntPtr hProcess, out uint lpExitCode);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    internal const uint EXTENDED_STARTUPINFO_PRESENT  = 0x00080000;
    internal const uint CREATE_UNICODE_ENVIRONMENT    = 0x00000400;
    internal const int  STARTF_USESTDHANDLES          = 0x00000100;
    internal static readonly IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

    // ── Structures ───────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    internal struct COORD
    {
        public short X;
        public short Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct SECURITY_ATTRIBUTES
    {
        public int    nLength;
        public IntPtr lpSecurityDescriptor;
        public int    bInheritHandle;    // int not bool to avoid marshaling surprises
    }

    /// <summary>
    /// All string fields are IntPtr to keep the struct blittable (no marshaler intervention).
    /// On 64-bit: sizeof = 104 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFO
    {
        public int    cb;
        public IntPtr lpReserved;
        public IntPtr lpDesktop;
        public IntPtr lpTitle;
        public int    dwX;
        public int    dwY;
        public int    dwXSize;
        public int    dwYSize;
        public int    dwXCountChars;
        public int    dwYCountChars;
        public int    dwFillAttribute;
        public int    dwFlags;
        public short  wShowWindow;
        public short  cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    /// <summary>On 64-bit: sizeof = 112 bytes (STARTUPINFO 104 + pointer 8).</summary>
    [StructLayout(LayoutKind.Sequential)]
    internal struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public IntPtr      lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int    dwProcessId;
        public int    dwThreadId;
    }
}
