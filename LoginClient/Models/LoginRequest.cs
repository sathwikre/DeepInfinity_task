namespace LoginClient.Models;

/// <summary>Payload sent to the local Azure Function.</summary>
public sealed class LoginRequest
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
