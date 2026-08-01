using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using LoginClient.Models;

namespace LoginClient.Services;

/// <summary>Encapsulates calls from the WPF application to its HTTP API.</summary>
public sealed class ApiService
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri("http://localhost:7071/")
    };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Posts credentials to POST /api/login.</summary>
    public async Task<LoginResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        // StringContent supplies a Content-Length header. This avoids an empty-body issue
        // seen with chunked JSON requests in some local Functions Core Tools hosts.
        string json = JsonSerializer.Serialize(request, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using HttpResponseMessage response = await HttpClient.PostAsync(
            "api/login", content, cancellationToken);

        LoginResponse? loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(
            cancellationToken: cancellationToken);

        if (loginResponse is not null)
        {
            return loginResponse;
        }

        // This protects the UI from a malformed or unexpected server response.
        return new LoginResponse
        {
            Success = false,
            Message = response.IsSuccessStatusCode
                ? "The server returned an invalid response."
                : $"The server returned HTTP {(int)response.StatusCode}."
        };
    }
}
