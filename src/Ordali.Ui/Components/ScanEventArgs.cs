namespace Ordali.Ui.Components;

/// <summary>A code read by a <see cref="ScanField"/>. Call <see cref="Reject"/> when it doesn't fit.</summary>
public sealed class ScanEventArgs(string code) : EventArgs
{
    public string Code { get; } = code;

    public bool Rejected { get; private set; }

    public void Reject() => Rejected = true;
}
