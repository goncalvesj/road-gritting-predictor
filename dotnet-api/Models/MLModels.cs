using Microsoft.ML.Data;

namespace GrittingApi.Models;

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
