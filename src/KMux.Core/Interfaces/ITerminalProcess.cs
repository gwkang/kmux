using KMux.Core.Events;

namespace KMux.Core.Interfaces;

public interface ITerminalProcess : IAsyncDisposable
{
    Guid Id { get; }
    int  ProcessId { get; }
    bool IsRunning { get; }
    event EventHandler<TerminalDataEventArgs>? OutputReceived;
    event EventHandler? Exited;
    void Write(string data);
    void Write(byte[] data);
    void Resize(short cols, short rows);
    void Kill();
}
