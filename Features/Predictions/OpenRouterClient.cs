using BPFL.API.Config;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;

namespace BPFL.API.Features.Predictions
{
    public class OpenRouterClient
    {
        private readonly HttpClient _httpClient;
        private readonly OpenRouterSettings _settings;
        private readonly ILogger<OpenRouterClient> _logger;

        public OpenRouterClient(HttpClient httpClient, IOptions<OpenRouterSettings> settings, ILogger<OpenRouterClient> logger)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<string?> CompleteAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
        {
            var requestBody = new
            {
                model = _settings.Model,
                messages = new[]
                {
                    new { role = "system", content = systemPrompt },
                    new { role = "user", content = userMessage }
                },
                max_tokens = 600,
                temperature = 0.3
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            _logger.LogDebug("Calling OpenRouter model {Model}", _settings.Model);

            var response = await _httpClient.PostAsync("/api/v1/chat/completions", content, ct);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(responseJson);

            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
    }
}
