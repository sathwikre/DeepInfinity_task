using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
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

        return new LoginResponse
        {
            Success = false,
            Message = response.IsSuccessStatusCode
                ? "The server returned an invalid response."
                : $"The server returned HTTP {(int)response.StatusCode}."
        };
    }

    public async Task<FileReadResponse> ReadFileAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        using HttpResponseMessage response = await HttpClient.PostAsync("api/read-file", content, cancellationToken);
        FileReadResponse? result = await response.Content.ReadFromJsonAsync<FileReadResponse>(cancellationToken: cancellationToken);

        if (result is not null)
        {
            return result;
        }

        return new FileReadResponse
        {
            Success = false,
            FileName = fileName,
            Content = string.Empty,
            Message = response.IsSuccessStatusCode
                ? "The server returned an invalid response."
                : $"The server returned HTTP {(int)response.StatusCode}."
        };
    }

    public async Task<TranscriptionResponse> TranscribeAudioAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        using var streamContent = new StreamContent(fileStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(streamContent, "file", fileName);

        using HttpResponseMessage response = await HttpClient.PostAsync("api/transcribe-audio", content, cancellationToken);
        TranscriptionResponse? result = await response.Content.ReadFromJsonAsync<TranscriptionResponse>(cancellationToken: cancellationToken);

        if (result is not null)
        {
            return result;
        }

        return new TranscriptionResponse
        {
            Success = false,
            FileName = fileName,
            Transcript = string.Empty,
            Message = response.IsSuccessStatusCode
                ? "The server returned an invalid response."
                : $"The server returned HTTP {(int)response.StatusCode}."
        };
    }
}
