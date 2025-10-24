using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShahdCooperative.AuthService.Domain.Interfaces;
using System.Text.Json;

namespace ShahdCooperative.AuthService.Infrastructure.Services;

public class GoogleRecaptchaService : ICaptchaService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleRecaptchaService> _logger;

    public GoogleRecaptchaService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<GoogleRecaptchaService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> VerifyTokenAsync(string token, string? ipAddress = null)
    {
        var isEnabled = _configuration.GetValue<bool>("GoogleRecaptcha:Enabled", true);

        // If CAPTCHA is disabled in configuration, always return true (for testing/development)
        if (!isEnabled)
        {
            _logger.LogInformation("CAPTCHA verification is disabled in configuration");
            return true;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("CAPTCHA token is null or empty");
            return false;
        }

        try
        {
            var secretKey = _configuration["GoogleRecaptcha:SecretKey"];

            if (string.IsNullOrWhiteSpace(secretKey))
            {
                _logger.LogError("Google reCAPTCHA secret key is not configured");
                return false;
            }

            var httpClient = _httpClientFactory.CreateClient();
            var requestData = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("secret", secretKey),
                new KeyValuePair<string, string>("response", token),
                new KeyValuePair<string, string>("remoteip", ipAddress ?? string.Empty)
            });

            var response = await httpClient.PostAsync("https://www.google.com/recaptcha/api/siteverify", requestData);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("reCAPTCHA API request failed with status {StatusCode}", response.StatusCode);
                return false;
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            var recaptchaResponse = JsonSerializer.Deserialize<RecaptchaResponse>(responseContent);

            if (recaptchaResponse == null)
            {
                _logger.LogError("Failed to deserialize reCAPTCHA response");
                return false;
            }

            if (!recaptchaResponse.Success)
            {
                _logger.LogWarning("reCAPTCHA verification failed. Errors: {Errors}",
                    string.Join(", ", recaptchaResponse.ErrorCodes ?? Array.Empty<string>()));
                return false;
            }

            // For reCAPTCHA v3, check the score
            var minimumScore = _configuration.GetValue<double>("GoogleRecaptcha:MinimumScore", 0.5);
            if (recaptchaResponse.Score.HasValue && recaptchaResponse.Score.Value < minimumScore)
            {
                _logger.LogWarning("reCAPTCHA score {Score} is below minimum {MinimumScore}",
                    recaptchaResponse.Score.Value, minimumScore);
                return false;
            }

            _logger.LogInformation("reCAPTCHA verification successful. Score: {Score}", recaptchaResponse.Score);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred during reCAPTCHA verification");
            return false;
        }
    }

    private class RecaptchaResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("success")]
        public bool Success { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("score")]
        public double? Score { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("action")]
        public string? Action { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("challenge_ts")]
        public string? ChallengeTimestamp { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("hostname")]
        public string? Hostname { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("error-codes")]
        public string[]? ErrorCodes { get; set; }
    }
}
