using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using static KMux.Terminal.ConPty.ConPtyNativeMethods;

namespace KMux.Terminal.ConPty;

internal sealed class PseudoConsole : IDisposable
{
    public IntPtr Handle { get; }

    /// <summary>C# side writes here → pty reads (user input)</summary>
    public SafeFileHandle InputWrite { get; }

    /// <summary>C# side reads here ← pty writes (terminal output)</summary>
    public SafeFileHandle OutputRead { get; }

    private PseudoConsole(IntPtr hPC, SafeFileHandle inputWrite, SafeFileHandle outputRead)
    {
        Handle      = hPC;
        InputWrite  = inputWrite;
        OutputRead  = outputRead;
    }

    public static PseudoConsole Create(short cols, short rows)
    {
        var sa = new SECURITY_ATTRIBUTES
        {
            nLength       = Marshal.SizeOf<SECURITY_ATTRIBUTES>(),
            bInheritHandle = 1   // TRUE — conhost.exe needs inheritable handles
        };

        // Pipe: C# InputWrite → pty InputRead
        if (!CreatePipe(out var inputRead, out var inputWrite, ref sa, 0))
            throw new InvalidOperationException($"CreatePipe (input) failed: {Marshal.GetLastWin32Error()}");

        // Pipe: pty OutputWrite → C# OutputRead
        if (!CreatePipe(out var outputRead, out var outputWrite, ref sa, 0))
            throw new InvalidOperationException($"CreatePipe (output) failed: {Marshal.GetLastWin32Error()}");

        // Prevent child-process inheritance of the ends we keep
        SetHandleInformation(inputWrite,  HANDLE_FLAG_INHERIT, 0);
        SetHandleInformation(outputRead,  HANDLE_FLAG_INHERIT, 0);

        var size = new COORD { X = cols, Y = rows };
        var hr   = CreatePseudoConsole(size, inputRead, outputWrite, 0, out var hPC);
        if (hr != 0)
            throw new InvalidOperationException($"CreatePseudoConsole failed: HRESULT 0x{hr:X}");

        // The pty now owns these ends; close our copies
        inputRead.Dispose();
        outputWrite.Dispose();

        return new PseudoConsole(hPC, inputWrite, outputRead);
    }

    public void Resize(short cols, short rows)
    {
        var size = new COORD { X = cols, Y = rows };
        ResizePseudoConsole(Handle, size);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        ClosePseudoConsole(Handle);
        InputWrite.Dispose();
        OutputRead.Dispose();
    }
    private bool _disposed;
}
