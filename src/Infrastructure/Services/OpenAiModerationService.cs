using System.Net.Http.Json;
using System.Text.Json;
using EbayClone.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EbayClone.Infrastructure.Services;

public class OpenAiModerationService : IContentModerationService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _baseUrl;
    private readonly string _model;
    private readonly ILogger<OpenAiModerationService> _logger;

    public OpenAiModerationService(HttpClient httpClient, IConfiguration configuration, ILogger<OpenAiModerationService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["OpenAI:ApiKey"] ?? string.Empty;
        _baseUrl = configuration["OpenAI:BaseUrl"] ?? "https://api.openai.com/v1/chat/completions";
        _model = configuration["OpenAI:Model"] ?? "gpt-3.5-turbo";
        _logger = logger;
    }

    public async Task<(bool IsFlagged, string? Reason)> ModerateContentAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("OpenAI API Key is missing. Skipping AI moderation.");
            return (false, null);
        }

        if (string.IsNullOrWhiteSpace(content)) return (false, null);

        try
        {
            var requestBody = new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "system", content = "You are a content moderator. Analyze product names and reviews for illegal items (drugs, weapons, contraband), profanity, hate speech, spam, inserted links, vulgarity, or extreme toxicity in Vietnamese. Respond with ONLY 'TOXIC: [1-3 word reason]' (e.g., 'TOXIC: Illegal drugs', 'TOXIC: Spam link', 'TOXIC: Profanity') or 'SAFE'. If the text mentions drugs (e.g., 'ma túy'), weapons, or illegal goods, you MUST flag it as TOXIC." },
                    new { role = "user", content = $"Content to check: \"{content}\"" }
                },
                temperature = 0.1,
                max_tokens = 1000 // High limit to ensure reasoning models don't get cut off
            };

            var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl);
            request.Headers.Add("Authorization", $"Bearer {_apiKey}");
            request.Content = JsonContent.Create(requestBody);

            var response = await _httpClient.SendAsync(request, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("AI API Error ({StatusCode}): {ErrorBody}", response.StatusCode, errorBody);
                return (false, "AI Moderation Failed (API Error)");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            var rawJson = json.GetRawText();
            //_logger.LogInformation("OpenRouter Raw Response: {RawJson}", rawJson);
            
            string? result = null;

            if (json.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
            {
                var message = choices[0].GetProperty("message");
                
                // Try to get standard content
                if (message.TryGetProperty("content", out var contentElement) && contentElement.ValueKind != JsonValueKind.Null)
                {
                    result = contentElement.GetString()?.Trim();
                }
                
                // Fallback to reasoning if content is null (some OpenRouter models use this)
                if (string.IsNullOrEmpty(result) && message.TryGetProperty("reasoning", out var reasoningElement))
                {
                    result = reasoningElement.GetString()?.Trim();
                }
            }

            _logger.LogInformation("AI Moderation Result for '{Content}': {Result}", content, result ?? "(null)");

            if (result != null && result.Contains("TOXIC", StringComparison.OrdinalIgnoreCase))
            {
                var reason = result.Contains(":") ? result.Split(':')[1].Trim() : "AI Detected Toxicity";
                return (true, reason);
            }

            return (false, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI for content moderation.");
            return (false, "AI Moderation Failed (Exception)");
        }
    }
}
