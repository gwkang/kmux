namespace KMux.Core.Events;

public sealed class TerminalDataEventArgs : EventArgs
{
    public byte[] Data { get; }
    public TerminalDataEventArgs(byte[] data) => Data = data;
}
