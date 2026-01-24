using Microsoft.ML.Data;

namespace GrittingApi.Models;

/// <summary>
/// Training data model matching the CSV structure
/// </summary>
public class TrainingData
{
    [LoadColumn(0)] public string date = string.Empty;
    [LoadColumn(1)] public string time = string.Empty;
    [LoadColumn(2)] public string route_id = string.Empty;
    [LoadColumn(3)] public string route_name = string.Empty;
    [LoadColumn(4)] public float priority;
    [LoadColumn(5)] public string road_type = string.Empty;
    [LoadColumn(6)] public float temperature_c;
    [LoadColumn(7)] public float feels_like_c;
    [LoadColumn(8)] public float humidity_pct;
    [LoadColumn(9)] public float wind_speed_kmh;
    [LoadColumn(10)] public string precipitation_type = string.Empty;
    [LoadColumn(11)] public float precipitation_prob_pct;
    [LoadColumn(12)] public float road_surface_temp_c;
    [LoadColumn(13)] public float forecast_min_temp_c;
    [LoadColumn(14)] public string ice_risk = string.Empty;
    [LoadColumn(15)] public string snow_risk = string.Empty;
    [LoadColumn(16)] public string gritting_decision = string.Empty;
    [LoadColumn(17)] public float salt_amount_kg;
    [LoadColumn(18)] public float spread_rate_g_m2;
    [LoadColumn(19)] public float route_length_km;
    [LoadColumn(20)] public float estimated_duration_min;
}

/// <summary>
/// Feature vector for prediction
/// </summary>
public class GrittingFeatures
{
    public float priority;
    public float temperature_c;
    public float feels_like_c;
    public float humidity_pct;
    public float wind_speed_kmh;
    public float precipitation_type_encoded;
    public float precipitation_prob_pct;
    public float road_surface_temp_c;
    public float forecast_min_temp_c;
    public float ice_risk_encoded;
    public float snow_risk_encoded;
    public float route_length_km;
    public float temp_below_zero;
    public float surface_temp_below_zero;
    public float high_precip_prob;
}

/// <summary>
/// Decision prediction output (binary classification)
/// </summary>
public class DecisionPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Decision;
    
    [ColumnName("Probability")]
    public float Probability;
}

/// <summary>
/// Amount prediction output (regression)
/// </summary>
public class AmountPrediction
{
    [ColumnName("Score")]
    public float Amount;
}
