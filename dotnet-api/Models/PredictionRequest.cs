namespace GrittingApi.Models;

/// <summary>
/// Request for gritting prediction
/// </summary>
public class PredictionRequest
{
    public string route_id { get; set; } = string.Empty;
    public WeatherData weather { get; set; } = new();
}

/// <summary>
/// Request for auto-weather prediction
/// </summary>
public class AutoWeatherRequest
{
    public string route_id { get; set; } = string.Empty;
    public double latitude { get; set; }
    public double longitude { get; set; }
}
