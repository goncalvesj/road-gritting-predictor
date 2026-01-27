using GrittingApi.Models;
using System.Text.Json;

namespace GrittingApi.Services;

/// <summary>
/// Service for fetching weather from external API
/// </summary>
public class WeatherService
{
    private readonly ILogger<WeatherService> _logger;
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;

    // Constants for weather estimation
    // Default precipitation probability when OpenWeatherMap current endpoint doesn't provide 'pop' field
    private const float DefaultPrecipitationProbPct = 50f;
    // Road surface temperature is typically slightly lower than air temperature due to thermal radiation
    private const float RoadSurfaceTempOffsetC = 1.5f;

    public WeatherService(ILogger<WeatherService> logger, HttpClient httpClient, IConfiguration configuration)
    {
        _logger = logger;
        _httpClient = httpClient;
        _apiKey = configuration["OPENWEATHER_API_KEY"] ?? Environment.GetEnvironmentVariable("OPENWEATHER_API_KEY");
    }

    /// <summary>
    /// Fetch weather data from OpenWeatherMap API
    /// </summary>
    public async Task<WeatherData> FetchWeatherAsync(double latitude, double longitude)
    {
        if (string.IsNullOrEmpty(_apiKey))
            throw new InvalidOperationException("Weather API key not configured. Set the OPENWEATHER_API_KEY environment variable.");

        var url = $"https://api.openweathermap.org/data/2.5/weather?lat={latitude}&lon={longitude}&appid={_apiKey}&units=metric";

        try
        {
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
            _logger.LogError(ex, "Failed to fetch weather data");
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
