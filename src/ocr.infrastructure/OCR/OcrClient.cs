using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Ocr.Core.Abstractions;
using Ocr.Core.OCR.DTOs;

namespace Ocr.Infrastructure.OCR

{
    public class OcrClient : IOcrClient
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public OcrClient(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;

            var baseUrl = _config.GetValue<string>("Ocr:BaseUrl") ?? "http://localhost:8080";
            _http.BaseAddress = new Uri(baseUrl);
        }

        private async Task<OcrExtraction> SendAsync(
         string pathTemplate,
         Stream imageStream,
         string fileName,
         string? contentType,
         int threshold,
         CancellationToken ct)
        {
            var path = pathTemplate ?? string.Empty;

            if (!path.StartsWith("/"))
            {
                path = "/" + path;
            }

            var separator =
                path.Contains('?')
                    ? "&"
                    : "?";

            path =
                $"{path}{separator}threshold={threshold}";


            using var content =
                new MultipartFormDataContent();

            var fileContent =
                new StreamContent(imageStream);

            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(
                    contentType
                    ?? "application/octet-stream"
                );

            content.Add(
                fileContent,
                "file",
                fileName
            );


            using var response =
                await _http.PostAsync(
                    path,
                    content,
                    ct
                );

            var raw =
                await response.Content
                    .ReadAsStringAsync(ct);


            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Python OCR returned " +
                    $"{(int)response.StatusCode} " +
                    $"{response.StatusCode}. " +
                    $"Body: {raw}"
                );
            }


            return new OcrExtraction(raw);
        }

        public Task<OcrExtraction> ExtractFrontAsync(
            Stream imageStream, string fileName, string? contentType, int threshold, CancellationToken ct = default) =>
            SendAsync(_config["Ocr:FrontPath"]!, imageStream, fileName, contentType, threshold, ct);

        public Task<OcrExtraction> ExtractBackAsync(
            Stream imageStream, string fileName, string? contentType, int threshold, CancellationToken ct = default) =>
            SendAsync(_config["Ocr:BackPath"]!, imageStream, fileName, contentType, threshold, ct);
    }
}
