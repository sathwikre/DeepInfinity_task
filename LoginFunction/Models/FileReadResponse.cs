namespace LoginFunction.Models;

public sealed class FileReadResponse
{
    public FileReadResponse(bool success, string fileName, string content, string message)
    {
        Success = success;
        FileName = fileName;
        Content = content;
        Message = message;
    }

    public bool Success { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Content { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
