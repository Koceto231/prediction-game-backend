using System.Net.Http.Headers;
using System.Text;

namespace BPFL.API.Shared.External
{
    /// <summary>
    /// Calls Stability AI's stable-image/generate/core endpoint.
    /// Returns raw PNG bytes, or null on failure.
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

        /// <summary>Generate an image and return PNG bytes, or null on failure.</summary>
        public async Task<byte[]?> GenerateImageAsync(
            string prompt,
            string aspectRatio = "16:9",
            CancellationToken ct = default)
        {
            try
            {
                using var form = new MultipartFormDataContent();

                // Use Add(content) — NOT Add(content, name).
                // The two-arg overload wraps the name in escaped quotes ("\"prompt\"")
                // which Stability AI's parser rejects. We pre-set Content-Disposition
                // ourselves with a plain unquoted name so the header reads:
                //   Content-Disposition: form-data; name=prompt
                form.Add(MakePart("prompt",        prompt));
                form.Add(MakePart("aspect_ratio",  aspectRatio));
                form.Add(MakePart("output_format", "png"));

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.stability.ai/v2beta/stable-image/generate/core");

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
                request.Content = form;

                var response = await _http.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Stability AI {Code}: {Err}", (int)response.StatusCode, err);
                    // Throw so the caller can surface the exact error (e.g. in backfill response)
                    throw new HttpRequestException(
                        $"Stability AI {(int)response.StatusCode}: {err}");
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                _logger.LogInformation("Stability AI returned {Bytes} bytes.", bytes.Length);
                return bytes;
            }
            catch (HttpRequestException)
            {
                throw; // propagate Stability AI errors up the stack
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StabilityAIClient.GenerateImageAsync failed.");
                throw new Exception($"StabilityAI request failed: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Builds a form part whose Content-Disposition already has the field name set
        /// without extra quoting, so Stability AI can parse it correctly.
        /// </summary>
        private static HttpContent MakePart(string fieldName, string value)
        {
            var content = new ByteArrayContent(Encoding.UTF8.GetBytes(value));
            content.Headers.ContentDisposition = new ContentDispositionHeaderValue("form-data");
            // Assign directly — avoids the double-quote wrapping that StringContent adds
            content.Headers.ContentDisposition.Name = fieldName;
            return content;
        }
    }
}
