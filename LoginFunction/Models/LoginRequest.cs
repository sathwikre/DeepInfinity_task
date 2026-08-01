namespace LoginFunction.Models;

/// <summary>Expected JSON body for POST /api/login.</summary>
public sealed class LoginRequest
{
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
