namespace LoginClient.Models;

/// <summary>Result returned by the local Azure Function.</summary>
public sealed class LoginResponse
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;
}
