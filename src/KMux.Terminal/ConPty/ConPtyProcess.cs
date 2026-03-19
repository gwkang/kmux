using System.Text;
using KMux.Core.Events;
using KMux.Core.Interfaces;
using KMux.Core.Models;
using Microsoft.Win32.SafeHandles;
using static KMux.Terminal.ConPty.ConPtyNativeMethods;

namespace KMux.Terminal.ConPty;

public sealed class ConPtyProcess : ITerminalProcess
{
    public Guid Id        { get; } = Guid.NewGuid();
    public int  ProcessId { get; private set; }
    public bool IsRunning => !_exitCts.IsCancellationRequested;

    public event EventHandler<TerminalDataEventArgs>? OutputReceived;
    public event EventHandler? Exited;

    private readonly PseudoConsole           _pty;
    private readonly PROCESS_INFORMATION     _processInfo;
    private readonly CancellationTokenSource _exitCts = new();
    private readonly Task                    _pumpTask;

    // Keep streams open for the lifetime of the process.
    // IMPORTANT: do NOT dispose these streams prematurely —
    // FileStream(SafeFileHandle) takes ownership by default and closes
    // the handle on Dispose, which would break subsequent reads/writes.
    private readonly FileStream _inputStream;
    private readonly FileStream _outputStream;

    private ConPtyProcess(PseudoConsole pty, PROCESS_INFORMATION pi)
    {
        _pty         = pty;
        _processInfo = pi;
        ProcessId    = (int)pi.dwProcessId;

        // Detect immediate crash (e.g. STATUS_DLL_INIT_FAILED = 0xc0000142)
        if (WaitForSingleObject(pi.hProcess, 200) == 0) // WAIT_OBJECT_0
        {
            GetExitCodeProcess(pi.hProcess, out var exitCode);
            throw new InvalidOperationException(
                $"Shell process exited immediately with code 0x{exitCode:X}");
        }

        // bufferSize=1 disables internal .NET buffering; we handle our own.
        // isAsync=false: CreatePipe creates synchronous (non-overlapped) handles.
        // Use non-owning SafeFileHandle wrappers so FileStream.Dispose() does NOT
        // close the underlying handles — the PseudoConsole owns them.
        _inputStream  = new FileStream(
            new SafeFileHandle(pty.InputWrite.DangerousGetHandle(), ownsHandle: false),
            FileAccess.Write, bufferSize: 1, isAsync: false);
        _outputStream = new FileStream(
            new SafeFileHandle(pty.OutputRead.DangerousGetHandle(), ownsHandle: false),
            FileAccess.Read, bufferSize: 1, isAsync: false);

        _pumpTask = Task.Factory.StartNew(PumpOutput,
                                          TaskCreationOptions.LongRunning);

        // Watch for shell process exit. ConPTY keeps the output pipe open even
        // after the child exits, so PumpOutput would block forever. Closing
        // OutputRead unblocks the Read() call, letting PumpOutput fire Exited.
        _ = Task.Factory.StartNew(() =>
        {
            WaitForSingleObject(_processInfo.hProcess, uint.MaxValue);
            if (!_exitCts.IsCancellationRequested)
                _pty.OutputRead.Dispose();
        }, TaskCreationOptions.LongRunning);
    }

    public static ConPtyProcess Start(ShellProfile profile,
                                      short cols = 120, short rows = 30)
    {
        var pty = PseudoConsole.Create(cols, rows);
        try
        {
            var pi = ProcessFactory.Spawn(profile, pty);
            return new ConPtyProcess(pty, pi);
        }
        catch
        {
            pty.Dispose();
            throw;
        }
    }

    // ── Write ────────────────────────────────────────────────────────────────

    public void Write(string data) => Write(Encoding.UTF8.GetBytes(data));

    public void Write(byte[] data)
    {
        if (_exitCts.IsCancellationRequested) return;
        try
        {
            lock (_inputStream)
                _inputStream.Write(data, 0, data.Length);
        }
        catch { /* process exited */ }
    }

    // ── Resize ───────────────────────────────────────────────────────────────

    public void Resize(short cols, short rows)
    {
        if (_exitCts.IsCancellationRequested) return;
        _pty.Resize(cols, rows);
    }

    // ── Kill ─────────────────────────────────────────────────────────────────

    public void Kill()
    {
        if (_exitCts.IsCancellationRequested) return;
        TerminateProcess(_processInfo.hProcess, 1);
    }

    // ── Output pump (blocking read on thread-pool LongRunning thread) ────────

    private void PumpOutput()
    {
        const int bufSize = 4096;
        var buf = new byte[bufSize];
        try
        {
            while (!_exitCts.IsCancellationRequested)
            {
                // Synchronous read — blocks until data arrives or pipe closes
                int read = _outputStream.Read(buf, 0, bufSize);
                if (read == 0) break;

                var copy = new byte[read];
                Buffer.BlockCopy(buf, 0, copy, 0, read);
                OutputReceived?.Invoke(this, new TerminalDataEventArgs(copy));
            }
        }
        catch { /* pipe broken = process exited */ }
        finally
        {
            _exitCts.Cancel();
            Exited?.Invoke(this, EventArgs.Empty);
        }
    }

    // ── Dispose ──────────────────────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        // Safe on any thread — just flips a flag
        _exitCts.Cancel();

        // All handle operations must run off the UI thread:
        // - TerminateProcess: quick P/Invoke, but no reason to block UI
        // - ClosePseudoConsole: blocks until conhost.exe exits
        // - CloseHandle on pipe: blocks until pending synchronous I/O completes
        await Task.Run(() =>
        {
            try { TerminateProcess(_processInfo.hProcess, 0); } catch { }

            // ClosePseudoConsole kills conhost, which closes the write end of the
            // output pipe. This causes PumpOutput's blocking ReadFile to return,
            // eliminating any pending I/O so CloseHandle won't deadlock.
            try { ClosePseudoConsole(_pty.Handle); } catch { }

            // Now safe — no pending I/O on these handles
            try { _inputStream.Dispose(); }  catch { }
            try { _outputStream.Dispose(); } catch { }
            _pty.InputWrite.Dispose();
            _pty.OutputRead.Dispose();

            CloseHandle(_processInfo.hThread);
            CloseHandle(_processInfo.hProcess);
        }).ConfigureAwait(false);

        // Wait for PumpOutput to finish (should already be done after pipe closure)
        try { await _pumpTask.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false); } catch { }

        _exitCts.Dispose();
    }
}
