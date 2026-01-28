using GrittingApi.Models;
using System.Text.Json;

namespace GrittingApi.Services;

/// <summary>
/// Service for fetching weather from external API.
/// Uses Open-Meteo as the primary provider (no API key required).
/// Falls back to OpenWeatherMap if OPENWEATHER_API_KEY is configured and Open-Meteo fails.
/// </summary>
public class WeatherService
{
    private readonly ILogger<WeatherService> _logger;
    private readonly HttpClient _httpClient;
    private readonly OpenMeteoWeatherService _openMeteoService;
    private readonly string? _apiKey;

    // Constants for weather estimation
    // Default precipitation probability when OpenWeatherMap current endpoint doesn't provide 'pop' field
    private const float DefaultPrecipitationProbPct = 50f;
    // Road surface temperature is typically slightly lower than air temperature due to thermal radiation
    private const float RoadSurfaceTempOffsetC = 1.5f;

    public WeatherService(
        ILogger<WeatherService> logger, 
        HttpClient httpClient, 
        IConfiguration configuration,
        OpenMeteoWeatherService openMeteoService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiKey = configuration["OPENWEATHER_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY");
        
        // Use injected Open-Meteo service
        _openMeteoService = openMeteoService;
    }

    /// <summary>
    /// Fetch weather data using Open-Meteo as primary source with OpenWeatherMap fallback.
    /// Open-Meteo provides accurate precipitation probability and forecast data
    /// without requiring an API key, making it ideal for this application.
    /// </summary>
    public async Task<WeatherData> FetchWeatherAsync(double latitude, double longitude)
    {
        // Try Open-Meteo first (no API key required)
        try
        {
            _logger.LogInformation("Fetching weather from Open-Meteo");
            return await _openMeteoService.FetchWeatherAsync(latitude, longitude);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Open-Meteo request failed, attempting fallback to OpenWeatherMap");
            
            // If Open-Meteo fails and we have an API key, try OpenWeatherMap as fallback
            if (!string.IsNullOrEmpty(_apiKey))
            {
                return await FetchWeatherFromOpenWeatherMapAsync(latitude, longitude);
            }
            else
            {
                // No fallback available, re-throw the original error
                throw new InvalidOperationException($"Open-Meteo error and no fallback configured: {ex.Message}", ex);
            }
        }
    }

    /// <summary>
    /// Fetch weather data from OpenWeatherMap API (legacy fallback).
    /// Note: The OpenWeatherMap current weather endpoint does not include precipitation probability.
    /// </summary>
    private async Task<WeatherData> FetchWeatherFromOpenWeatherMapAsync(double latitude, double longitude)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Weather API key not configured. Set the OPENWEATHER_API_KEY environment variable.");

        var url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={_apiKey}&units=metric";

        try
        {
            _logger.LogInformation("Fetching weather from OpenWeatherMap");
            var response = await _httpClient.GetAsync(url);
            
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("Invalid weather API key");

            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // Extract weather data
            var main = data.GetProperty("main");
            var weather = data.GetProperty("weather")[0];
            var wind = data.TryGetProperty("wind", out var windElem) ? windElem : default;

            var weatherData = new WeatherData
            {
                temperature_c = main.GetProperty("temp").GetSingle(),
                feels_like_c = main.GetProperty("feels_like").GetSingle(),
                humidity_pct = main.GetProperty("humidity").GetSingle(),
                wind_speed_kmh = (wind.ValueKind != JsonValueKind.Undefined && wind.TryGetProperty("speed", out var speed) 
                    ? speed.GetSingle() : 0) * 3.6f, // m/s to km/h
                precipitation_type = MapWeatherCondition(weather.GetProperty("main").GetString() ?? "Clear"),
                precipitation_prob_pct = DefaultPrecipitationProbPct,
                road_surface_temp_c = main.GetProperty("temp").GetSingle() - RoadSurfaceTempOffsetC,
                forecast_min_temp_c = main.GetProperty("temp_min").GetSingle()
            };

            return weatherData;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch weather data from OpenWeatherMap");
            throw new InvalidOperationException($"Weather API request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("Weather API request timed out");
        }
    }

    /// <summary>
    /// Map OpenWeatherMap condition to our categories
    /// </summary>
    private string MapWeatherCondition(string condition) => condition switch
    {
        "Clear" => "none",
        "Clouds" => "none",
        "Rain" => "rain",
        "Drizzle" => "rain",
        "Snow" => "snow",
        "Sleet" => "sleet",
        "Mist" => "none",
        "Fog" => "none",
        _ => "none"
    };
}
