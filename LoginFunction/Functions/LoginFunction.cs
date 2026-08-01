using System.Net;
using System.IO;
using System.Text.Json;
using LoginFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LoginFunction.Functions;

public sealed class LoginFunction
{
    private const string ValidUsername = "admin";
    private const string ValidPassword = "1234";
    private readonly ILogger<LoginFunction> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public LoginFunction(ILogger<LoginFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(Login))]
    public async Task<HttpResponseData> Login(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "login")] HttpRequestData request)
    {
        try
        {
            using var reader = new StreamReader(request.Body);
            string requestBody = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(requestBody))
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                    new LoginResponse(false, "The request body is required."));
            }

            LoginRequest? loginRequest = JsonSerializer.Deserialize<LoginRequest>(
                requestBody, JsonOptions);

            if (loginRequest is null ||
                string.IsNullOrWhiteSpace(loginRequest.Username) ||
                string.IsNullOrWhiteSpace(loginRequest.Password))
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                    new LoginResponse(false, "Username and password are required."));
            }

            bool credentialsAreValid = loginRequest.Username == ValidUsername &&
                                       loginRequest.Password == ValidPassword;

            return await CreateJsonResponseAsync(request, HttpStatusCode.OK,
                credentialsAreValid
                    ? new LoginResponse(true, "Login Successful")
                    : new LoginResponse(false, "Invalid Credentials"));
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "The login request did not contain valid JSON.");
            return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                new LoginResponse(false, "The request body must contain valid JSON."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An unexpected error occurred while processing login.");
            return await CreateJsonResponseAsync(request, HttpStatusCode.InternalServerError,
                new LoginResponse(false, "An unexpected server error occurred."));
        }
    }

    private static async Task<HttpResponseData> CreateJsonResponseAsync(
        HttpRequestData request, HttpStatusCode statusCode, LoginResponse body)
    {
        HttpResponseData response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, JsonOptions));
        return response;
    }
}
