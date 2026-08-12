using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace LoginFunction.Helpers;

public static class MultipartFormHelper
{
    public static async Task<FileSection?> ReadSingleFileSectionAsync(HttpRequestData request)
    {
        if (!request.Headers.TryGetValues("Content-Type", out IEnumerable<string>? contentTypes))
        {
            return null;
        }

        string contentType = contentTypes.FirstOrDefault() ?? string.Empty;
        if (!Microsoft.Net.Http.Headers.MediaTypeHeaderValue.TryParse(contentType, out Microsoft.Net.Http.Headers.MediaTypeHeaderValue? mediaType)
            || mediaType?.Boundary == null)
        {
            return null;
        }

        var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;
        if (string.IsNullOrWhiteSpace(boundary))
        {
            return null;
        }

        var reader = new MultipartReader(boundary, request.Body);
        MultipartSection? section;

        while ((section = await reader.ReadNextSectionAsync()) is not null)
        {
            if (!ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out ContentDispositionHeaderValue? contentDisposition))
            {
                continue;
            }

            if (!contentDisposition.DispositionType.Equals("form-data") || string.IsNullOrWhiteSpace(contentDisposition.FileName.Value))
            {
                continue;
            }

            string fileName = Path.GetFileName(contentDisposition.FileName.Value.Trim('"'));
            string fileContentType = section.ContentType ?? "application/octet-stream";

            using var memoryStream = new MemoryStream();
            await section.Body.CopyToAsync(memoryStream);
            return new FileSection(fileName, fileContentType, memoryStream.ToArray());
        }

        return null;
    }
}

public sealed class FileSection
{
    public FileSection(string fileName, string contentType, byte[] contents)
    {
        FileName = fileName;
        ContentType = contentType;
        Contents = contents;
    }

    public string FileName { get; }
    public string ContentType { get; }
    public byte[] Contents { get; }
}
