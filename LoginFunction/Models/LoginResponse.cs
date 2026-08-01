namespace LoginFunction.Models;

/// <summary>JSON result returned by the login endpoint.</summary>
public sealed record LoginResponse(bool Success, string Message);
