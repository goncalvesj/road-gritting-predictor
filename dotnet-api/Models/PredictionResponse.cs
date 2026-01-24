namespace GrittingApi.Models;

/// <summary>
/// Response from gritting prediction
/// </summary>
public class PredictionResponse
{
    public bool success { get; set; }
    public PredictionResult? prediction { get; set; }
    public string? error { get; set; }
    public string? weather_source { get; set; }
}

/// <summary>
/// Gritting prediction result
/// </summary>
public class PredictionResult
{
    public string route_id { get; set; } = string.Empty;
    public string route_name { get; set; } = string.Empty;
    public string gritting_decision { get; set; } = string.Empty;
    public double decision_confidence { get; set; }
    public int salt_amount_kg { get; set; }
    public int spread_rate_g_m2 { get; set; }
    public int estimated_duration_min { get; set; }
    public string ice_risk { get; set; } = string.Empty;
    public string snow_risk { get; set; } = string.Empty;
    public string recommendation { get; set; } = string.Empty;
}
