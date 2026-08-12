namespace LoginClient.Models;

public sealed class FileReadResponse
{
    public bool Success { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
