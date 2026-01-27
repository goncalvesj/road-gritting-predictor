using GrittingApi.Models;
using System.Text.Json;

namespace GrittingApi.Services;

/// <summary>
/// Service for fetching weather data from Open-Meteo API.
/// Open-Meteo is a free, open-source weather API that requires no API key.
/// API Documentation: https://open-meteo.com/en/docs
/// </summary>
public class OpenMeteoWeatherService
{
    private readonly ILogger<OpenMeteoWeatherService> _logger;
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.open-meteo.com/v1/forecast";

    // Constants for weather estimation
    // Road surface temperature is typically slightly lower than air temperature due to thermal radiation
    private const float RoadSurfaceTempOffsetC = 1.5f;

    public OpenMeteoWeatherService(ILogger<OpenMeteoWeatherService> logger, HttpClient httpClient)
    {
        _logger = logger;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Fetch current weather data from Open-Meteo API
    /// </summary>
    /// <param name="latitude">Location latitude (-90 to 90)</param>
    /// <param name="longitude">Location longitude (-180 to 180)</param>
    /// <returns>Weather data in the format expected by the gritting prediction system</returns>
    public async Task<WeatherData> FetchWeatherAsync(double latitude, double longitude)
    {
        // Build query parameters
        var currentParams = string.Join(",", new[]
        {
            "temperature_2m",
            "apparent_temperature",  // feels like temperature
            "relative_humidity_2m",
            "wind_speed_10m",
            "precipitation",
            "weather_code"
        });

        var hourlyParams = string.Join(",", new[]
        {
            "temperature_2m",
            "precipitation_probability"
        });

        var url = $"{BaseUrl}?latitude={latitude}&longitude={longitude}" +
                  $"&current={currentParams}" +
                  $"&hourly={hourlyParams}" +
                  $"&forecast_days=1" +
                  $"&timezone=auto";

        try
        {
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            var data = JsonSerializer.Deserialize<JsonElement>(json);

            // Extract current weather
            var current = data.GetProperty("current");
            var hourly = data.TryGetProperty("hourly", out var hourlyElem) ? hourlyElem : default;

            var temperatureC = current.GetProperty("temperature_2m").GetSingle();
            var feelsLikeC = current.GetProperty("apparent_temperature").GetSingle();
            var humidityPct = current.GetProperty("relative_humidity_2m").GetSingle();
            var windSpeedKmh = current.GetProperty("wind_speed_10m").GetSingle();
            var weatherCode = current.GetProperty("weather_code").GetInt32();

            // Map weather code to precipitation type
            var precipitationType = MapWeatherCodeToPrecipitation(weatherCode);

            // Get precipitation probability from hourly forecast (next hour)
            float precipitationProbPct = 0f;
            if (hourly.ValueKind != JsonValueKind.Undefined && 
                hourly.TryGetProperty("precipitation_probability", out var precipProbArray))
            {
                // Get the first non-null probability value (current or next hour)
                var arrayLength = precipProbArray.GetArrayLength();
                for (int i = 0; i < Math.Min(3, arrayLength); i++)  // Check first 3 hours
                {
                    var prob = precipProbArray[i];
                    if (prob.ValueKind != JsonValueKind.Null)
                    {
                        precipitationProbPct = prob.GetSingle();
                        break;
                    }
                }
            }

            // Get minimum temperature from hourly forecast (next 24 hours)
            float forecastMinTempC = temperatureC;
            if (hourly.ValueKind != JsonValueKind.Undefined && 
                hourly.TryGetProperty("temperature_2m", out var tempArray))
            {
                var minTemp = float.MaxValue;
                var arrayLength = tempArray.GetArrayLength();
                for (int i = 0; i < arrayLength; i++)
                {
                    var temp = tempArray[i];
                    if (temp.ValueKind != JsonValueKind.Null)
                    {
                        var tempValue = temp.GetSingle();
                        if (tempValue < minTemp)
                            minTemp = tempValue;
                    }
                }
                if (minTemp != float.MaxValue)
                    forecastMinTempC = minTemp;
            }

            // Estimate road surface temperature
            var roadSurfaceTempC = temperatureC - RoadSurfaceTempOffsetC;

            return new WeatherData
            {
                temperature_c = temperatureC,
                feels_like_c = feelsLikeC,
                humidity_pct = humidityPct,
                wind_speed_kmh = windSpeedKmh,
                precipitation_type = precipitationType,
                precipitation_prob_pct = precipitationProbPct,
                road_surface_temp_c = roadSurfaceTempC,
                forecast_min_temp_c = forecastMinTempC
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch weather data from Open-Meteo");
            throw new InvalidOperationException($"Weather API request failed: {ex.Message}", ex);
        }
        catch (TaskCanceledException)
        {
            throw new InvalidOperationException("Weather API request timed out");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse Open-Meteo response");
            throw new InvalidOperationException($"Unexpected weather API response format: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Map Open-Meteo WMO weather codes to precipitation types.
    /// 
    /// WMO Weather interpretation codes (WW):
    /// 0: Clear sky
    /// 1, 2, 3: Mainly clear, partly cloudy, and overcast
    /// 45, 48: Fog
    /// 51, 53, 55: Drizzle
    /// 56, 57: Freezing Drizzle
    /// 61, 63, 65: Rain
    /// 66, 67: Freezing Rain
    /// 71, 73, 75: Snow fall
    /// 77: Snow grains
    /// 80, 81, 82: Rain showers
    /// 85, 86: Snow showers
    /// 95: Thunderstorm
    /// 96, 99: Thunderstorm with hail
    /// </summary>
    private string MapWeatherCodeToPrecipitation(int weatherCode)
    {
        // Snow conditions
        if (weatherCode is 71 or 73 or 75 or 77 or 85 or 86)
            return "snow";

        // Sleet/freezing conditions (freezing rain, freezing drizzle, thunderstorm with hail)
        if (weatherCode is 56 or 57 or 66 or 67 or 96 or 99)
            return "sleet";

        // Rain conditions (drizzle, rain, rain showers, thunderstorm)
        if (weatherCode is 51 or 53 or 55 or 61 or 63 or 65 or 80 or 81 or 82 or 95)
            return "rain";

        // Clear, cloudy, fog - no precipitation
        return "none";
    }
}
