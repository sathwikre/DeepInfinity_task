using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using LoginFunction.Helpers;
using LoginFunction.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace LoginFunction.Functions;

public sealed class TranscribeAudioFunction
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SupportedExtensions = { ".wav", ".mp3" };

    // Path to whisper_transcribe.py, copied to the output directory alongside this assembly.
    private static readonly string ScriptPath = Path.Combine(AppContext.BaseDirectory, "whisper_transcribe.py");

    // Python executable: prefer the setting from local.settings.json / environment, fall back to "py -3.11".
    // Set WHISPER_PYTHON_EXE in local.settings.json Values to override (e.g. full path to python.exe).
    private static readonly string PythonExe =
        Environment.GetEnvironmentVariable("WHISPER_PYTHON_EXE") ?? "py";

    // Whisper model size: tiny | base | small | medium | large.
    // "base" is a good balance of speed and accuracy for local use.
    private static readonly string WhisperModel =
        Environment.GetEnvironmentVariable("WHISPER_MODEL") ?? "base";

    private readonly ILogger<TranscribeAudioFunction> _logger;

    public TranscribeAudioFunction(ILogger<TranscribeAudioFunction> logger)
    {
        _logger = logger;
    }

    [Function(nameof(TranscribeAudio))]
    public async Task<HttpResponseData> TranscribeAudio(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "transcribe-audio")] HttpRequestData request)
    {
        try
        {
            FileSection? fileSection = await MultipartFormHelper.ReadSingleFileSectionAsync(request);
            if (fileSection is null)
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                    new AudioTranscriptionResponse(false, string.Empty, string.Empty,
                        "An audio file must be uploaded using multipart/form-data."));
            }

            string extension = Path.GetExtension(fileSection.FileName).ToLowerInvariant();
            if (!SupportedExtensions.Contains(extension))
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                    new AudioTranscriptionResponse(false, fileSection.FileName, string.Empty,
                        $"Unsupported audio format '{extension}'. Use .wav or .mp3."));
            }

            if (fileSection.Contents.Length == 0)
            {
                return await CreateJsonResponseAsync(request, HttpStatusCode.BadRequest,
                    new AudioTranscriptionResponse(false, fileSection.FileName, string.Empty,
                        "The uploaded audio file is empty."));
            }

            // Write the uploaded bytes to a temp file so Whisper (via Python) can read it.
            string tempAudioPath = Path.Combine(Path.GetTempPath(), $"whisper-{Guid.NewGuid():N}{extension}");
            try
            {
                await File.WriteAllBytesAsync(tempAudioPath, fileSection.Contents);

                string transcript = await RunWhisperAsync(tempAudioPath, fileSection.FileName);

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    return await CreateJsonResponseAsync(request, HttpStatusCode.OK,
                        new AudioTranscriptionResponse(false, fileSection.FileName, string.Empty,
                            "Whisper returned an empty transcript. The audio may contain no speech."));
                }

                return await CreateJsonResponseAsync(request, HttpStatusCode.OK,
                    new AudioTranscriptionResponse(true, fileSection.FileName, transcript, string.Empty));
            }
            finally
            {
                TryDeleteFile(tempAudioPath);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "An unexpected error occurred while processing transcribe-audio.");
            return await CreateJsonResponseAsync(request, HttpStatusCode.InternalServerError,
                new AudioTranscriptionResponse(false, string.Empty, string.Empty,
                    "An unexpected server error occurred during audio transcription."));
        }
    }

    /// <summary>
    /// Invokes whisper_transcribe.py as a subprocess and returns the trimmed transcript from stdout.
    /// Throws <see cref="InvalidOperationException"/> if the process exits with a non-zero code.
    /// </summary>
    private async Task<string> RunWhisperAsync(string audioFilePath, string originalFileName)
    {
        if (!File.Exists(ScriptPath))
        {
            throw new InvalidOperationException(
                $"whisper_transcribe.py was not found at '{ScriptPath}'. " +
                "Ensure it is set to CopyToOutputDirectory in the project file.");
        }

        // Build args: when PythonExe is "py" we also need the version switch "-3.11".
        string arguments = PythonExe.Equals("py", StringComparison.OrdinalIgnoreCase)
            ? $"-3.11 \"{ScriptPath}\" \"{audioFilePath}\" {WhisperModel}"
            : $"\"{ScriptPath}\" \"{audioFilePath}\" {WhisperModel}";

        _logger.LogInformation("Running Whisper on '{FileName}' using model '{Model}'.",
            originalFileName, WhisperModel);

        var psi = new ProcessStartInfo
        {
            FileName = PythonExe,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        using var process = new Process { StartInfo = psi };

        var stdoutBuilder = new System.Text.StringBuilder();
        var stderrBuilder = new System.Text.StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null) stdoutBuilder.AppendLine(e.Data);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderrBuilder.AppendLine(e.Data);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        // Allow up to 10 minutes for longer audio files on CPU.
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new InvalidOperationException(
                "Transcription timed out after 10 minutes. Try a shorter audio clip or set WHISPER_MODEL=tiny for faster processing.");
        }

        string stderr = stderrBuilder.ToString().Trim();
        string stdout = stdoutBuilder.ToString().Trim();

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            _logger.LogDebug("Whisper stderr: {Stderr}", stderr);
        }

        if (process.ExitCode != 0)
        {
            string errorDetail = string.IsNullOrWhiteSpace(stderr) ? "No details available." : stderr;
            _logger.LogError("Whisper process exited with code {Code}. Stderr: {Stderr}",
                process.ExitCode, errorDetail);
            throw new InvalidOperationException(
                $"Transcription failed (exit code {process.ExitCode}): {errorDetail}");
        }

        return stdout;
    }

    private void TryDeleteFile(string path)
    {
        if (!File.Exists(path)) return;
        try { File.Delete(path); }
        catch (Exception ex) { _logger.LogWarning(ex, "Could not delete temp file '{Path}'.", path); }
    }

    private static async Task<HttpResponseData> CreateJsonResponseAsync<T>(
        HttpRequestData request, HttpStatusCode statusCode, T body)
    {
        HttpResponseData response = request.CreateResponse(statusCode);
        response.Headers.Add("Content-Type", "application/json; charset=utf-8");
        await response.WriteStringAsync(JsonSerializer.Serialize(body, JsonOptions));
        return response;
    }
}
