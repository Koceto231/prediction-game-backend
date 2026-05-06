namespace BPFL.API.Shared.External
{
    /// <summary>
    /// Generates images via Pollinations.AI — free, no API key required.
    /// GET https://image.pollinations.ai/prompt/{encoded}?width=1280&height=720&nologo=true
    /// </summary>
    public class StabilityAIClient          // name kept to avoid refactoring all references
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
            string aspectRatio = "16:9",   // kept for signature compatibility; Pollinations uses w/h
            CancellationToken ct = default)
        {
            var encoded = Uri.EscapeDataString(prompt);
            var url     = $"https://image.pollinations.ai/prompt/{encoded}?width=1280&height=720&nologo=true&model=flux";

            _logger.LogInformation("Pollinations request: {Url}", url[..Math.Min(120, url.Length)]);

            var response = await _http.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("Pollinations {Code}: {Err}", (int)response.StatusCode, err);
                throw new HttpRequestException($"Pollinations {(int)response.StatusCode}: {err}");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(ct);
            _logger.LogInformation("Pollinations returned {Bytes} bytes.", bytes.Length);
            return bytes;
        }
    }
}
