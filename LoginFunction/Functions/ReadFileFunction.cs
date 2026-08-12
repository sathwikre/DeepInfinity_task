using System.Net;
using System.Text;
using System.Text.Json;
using LoginFunction.Helpers;
using LoginFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Tesseract;
using UglyToad.PdfPig;

namespace LoginFunction.Functions;

public sealed class ReadFileFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SupportedExtensions = { ".txt", ".pdf", ".jpg", ".jpeg", ".png" };
    private static readonly string TessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");

    private readonly ILogger<ReadFileFunction> _logger;

    public ReadFileFunction(ILogger<ReadFileFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(ReadFile))]
    public async Task<HttpResponseData> ReadFile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "read-file")] HttpRequestData request)
    {
        try
        {
            FileSection? fileSection = await MultipartFormHelper.ReadSingleFileSectionAsync(request);
            if (fileSection is null)
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                    new FileReadResponse(false, string.Empty, string.Empty, "A file must be uploaded using multipart/form-data."));
            }

            string extension = Path.GetExtension(fileSection.FileName).ToLowerInvariant();
            if (!SupportedExtensions.Contains(extension))
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                    new FileReadResponse(false, fileSection.FileName, string.Empty, "Unsupported file type. Use .txt, .pdf, .jpg, .jpeg, or .png."));
            }

            if (fileSection.Contents.Length == 0)
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                    new FileReadResponse(false, fileSection.FileName, string.Empty, "The uploaded file is empty."));
            }

            string extractedText = extension switch
            {
                ".txt" => ReadTextFile(fileSection.Contents),
                ".pdf" => ReadPdfText(fileSection.Contents),
                ".jpg" or ".jpeg" or ".png" => ReadImageText(fileSection.Contents),
                _ => string.Empty
            };

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.OK,
                    new FileReadResponse(false, fileSection.FileName, string.Empty,
                        "Unable to extract content from the selected file."));
            }

            return await CreateJsonResponseAsync(request, HttpStatusCode.OK,
                new FileReadResponse(true, fileSection.FileName, extractedText.Trim(), string.Empty));
        }
        catch (TesseractException exception)
        {
            _logger.LogError(exception, "An OCR error occurred.");
            return await CreateJsonResponseAsync(request, HttpStatusCode.InternalServerError,
                new FileReadResponse(false, string.Empty, string.Empty,
                    "OCR failed. Verify that tessdata is installed in the LoginFunction/tessdata folder."));
        }
        catch (JsonException exception)
        {
            _logger.LogError(exception, "A JSON serialization error occurred.");
            return await CreateJsonResponseAsync(request, HttpStatusCode.InternalServerError,
                new FileReadResponse(false, string.Empty, string.Empty,
                    "An error occurred while preparing the response."));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An unexpected error occurred while processing read-file.");
            return await CreateJsonResponseAsync(request, HttpStatusCode.InternalServerError,
                new FileReadResponse(false, string.Empty, string.Empty,
                    "An unexpected server error occurred while reading the file."));
        }
    }

    private static string ReadTextFile(byte[] contents)
    {
        using var memoryStream = new MemoryStream(contents);
        using var reader = new StreamReader(memoryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string ReadPdfText(byte[] contents)
    {
        using var memoryStream = new MemoryStream(contents);
        using var document = PdfDocument.Open(memoryStream);
        var builder = new StringBuilder();

        foreach (var page in document.GetPages())
        {
            builder.AppendLine(page.Text);
        }

        return builder.ToString();
    }

    private static string ReadImageText(byte[] contents)
    {
        if (!Directory.Exists(TessDataPath))
        {
            throw new TesseractException("tessdata not found");
        }

        using var engine = new TesseractEngine(TessDataPath, "eng", EngineMode.Default);
        using var pix = Pix.LoadFromMemory(contents);
        using var page = engine.Process(pix);
        return page.GetText() ?? string.Empty;
    }

    private static async Task<HttpResponseData> CreateJsonResponseAsync<T>(HttpRequestData request, HttpStatusCode statusCode, T body)
    {
        HttpResponseData response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, JsonOptions));
        return response;
    }
}
