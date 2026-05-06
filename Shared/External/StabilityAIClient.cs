using System.Net.Http.Headers;
using System.Text.Json;

namespace BPFL.API.Shared.External
{
    /// <summary>
    /// Calls Stability AI's stable-image/generate/core endpoint.
    /// Returns raw PNG bytes, or null on failure.
    /// </summary>
    public class StabilityAIClient
    {
        private readonly HttpClient              _http;
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
                form.Add(new StringContent(prompt),      "prompt");
                form.Add(new StringContent(aspectRatio), "aspect_ratio");
                form.Add(new StringContent("none"),      "output_format");   // PNG

                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    "https://api.stability.ai/v2beta/stable-image/generate/core");

                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));
                request.Content = form;

                var response = await _http.SendAsync(request, ct);

                if (!response.IsSuccessStatusCode)
                {
                    var err = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("Stability AI error {Code}: {Err}", response.StatusCode, err);
                    return null;
                }

                return await response.Content.ReadAsByteArrayAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "StabilityAIClient.GenerateImageAsync failed.");
                return null;
            }
        }
    }
}
