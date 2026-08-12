namespace LoginFunction.Models;

public sealed class AudioTranscriptionResponse
{
    public AudioTranscriptionResponse(bool success, string fileName, string transcript, string message)
    {
        Success = success;
        FileName = fileName;
        Transcript = transcript;
        Message = message;
    }

    public bool Success { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Transcript { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
