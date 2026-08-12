namespace LoginClient.Models;

public sealed class TranscriptionResponse
{
    public bool Success { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Transcript { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
