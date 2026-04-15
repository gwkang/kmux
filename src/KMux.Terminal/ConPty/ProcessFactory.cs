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

            // Explicitly null out the standard handles so they are never inherited,
            // even when the parent process has its stdin/stdout/stderr redirected
            // (e.g. VS Code debugger's internalConsole). Without STARTF_USESTDHANDLES
            // the child would fall back to the parent's standard handles, causing shell
            // output to leak into the debug console instead of the ConPTY pipe.
            si.StartupInfo.dwFlags   = STARTF_USESTDHANDLES;
            si.StartupInfo.hStdInput  = INVALID_HANDLE_VALUE;
            si.StartupInfo.hStdOutput = INVALID_HANDLE_VALUE;
            si.StartupInfo.hStdError  = INVALID_HANDLE_VALUE;

            // ── Step 4: launch process ───────────────────────────────────────
            // envBlock is always non-null (BuildEnvironmentBlock always builds an explicit block)
            var creationFlags = EXTENDED_STARTUPINFO_PRESENT | CREATE_UNICODE_ENVIRONMENT;

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

    // Env vars injected by the VS Code coreclr debugger (Hot Reload agent, test host, etc.)
    // that must NOT leak into child shells — pwsh.exe is itself .NET and would try to load
    // the debugger's startup hook, stalling before producing any output.
    private static readonly string[] _debuggerExactKeys =
    [
        "DOTNET_STARTUP_HOOKS",
        "DOTNET_MODIFIABLE_ASSEMBLIES",
        "DOTNET_HOTRELOAD_NAMEDPIPE_NAME",
    ];

    private static readonly string[] _debuggerPrefixes =
    [
        "DOTNET_WATCH",
        "ASPNETCORE_",
        "VSTEST_",
    ];

    private static bool IsDebuggerInjected(string key)
    {
        foreach (var k in _debuggerExactKeys)
            if (string.Equals(key, k, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var p in _debuggerPrefixes)
            if (key.StartsWith(p, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    /// <summary>
    /// Builds a null-terminated Unicode environment block for CreateProcess.
    /// Always constructs an explicit block (never returns IntPtr.Zero) so that
    /// debugger-injected vars are filtered out regardless of <paramref name="extra"/>.
    /// </summary>
    private static IntPtr BuildEnvironmentBlock(Dictionary<string, string> extra)
    {
        // Start from current process environment, strip debugger-injected keys, merge extras
        var env = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            var key = (string)entry.Key;
            if (!IsDebuggerInjected(key))
                env[key] = (string?)entry.Value ?? "";
        }
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
