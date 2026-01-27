namespace GrittingApi.Models;

/// <summary>
/// Weather data for gritting prediction
/// </summary>
public class WeatherData
{
    public float temperature_c { get; set; }
    public float feels_like_c { get; set; }
    public float humidity_pct { get; set; }
    public float wind_speed_kmh { get; set; }
    public string precipitation_type { get; set; } = string.Empty;
    public float precipitation_prob_pct { get; set; }
    public float road_surface_temp_c { get; set; }
    public float forecast_min_temp_c { get; set; }
}
