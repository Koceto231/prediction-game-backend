using System.Net.Http.Headers;
using System.Text;

namespace BPFL.API.Shared.External
{
    /// <summary>
    /// Generates images via Stability AI stable-image/generate/core.
    /// Requires a funded API key at https://platform.stability.ai/account/credits
    /// </summary>
    public class StabilityAIClient
    {
        private readonly HttpClient                 _http;
        private readonly ILogger<StabilityAIClient> _logger;

        public StabilityAIClient(HttpClient http, ILogger<StabilityAIClient> logger)
        {
            _http   = http;
            _logger = logger;
        }

        /// <summary>Generate an image and return PNG bytes, or throws on failure.</summary>
        public async Task<byte[]?> GenerateImageAsync(
            string prompt,
            string aspectRatio = "16:9",
            CancellationToken ct = default)
        {
            // Build multipart body manually — avoids .NET's escaped-quote issue with
            // MultipartFormDataContent which Stability AI rejects.
            var boundary = "sai" + Guid.NewGuid().ToString("N");

            var sb = new StringBuilder();
            void AppendField(string name, string value)
            {
                sb.Append($"--{boundary}\r\n");
                sb.Append($"Content-Disposition: form-data; name=\"{name}\"\r\n");
                sb.Append("\r\n");
                sb.Append(value);
                sb.Append("\r\n");
            }

            AppendField("prompt",        prompt);
            AppendField("aspect_ratio",  aspectRatio);
            AppendField("output_format", "png");
            sb.Append($"--{boundary}--\r\n");

            var bodyBytes = Encoding.UTF8.GetBytes(sb.ToString());

            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                "https://api.stability.ai/v2beta/stable-image/generate/core");

            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

            var content = new ByteArrayContent(bodyBytes);
            content.Headers.TryAddWithoutValidation(
                "Content-Type", $"multipart/form-data; boundary={boundary}");
            request.Content = content;

            var response = await _http.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Stability AI {Code}: {Err}", (int)response.StatusCode, err);
                throw new HttpRequestException($"Stability AI {(int)response.StatusCode}: {err}");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            _logger.LogInformation("Stability AI returned {Bytes} bytes.", bytes.Length);
            return bytes;
        }
    }
}
