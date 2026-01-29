using Microsoft.ML;
using GrittingApi.Models;

namespace GrittingApi.Services;

/// <summary>
/// ML.NET-based gritting prediction service.
/// This service loads pre-trained models and performs predictions.
/// For model training, use the ModelTrainer tool.
/// </summary>
public class GrittingPredictionService
{
    private readonly MLContext _mlContext;
    private ITransformer? _decisionModel;
    private ITransformer? _amountModel;
    private PredictionEngine<GrittingFeatures, DecisionPrediction>? _decisionEngine;
    private PredictionEngine<GrittingFeatures, AmountPrediction>? _amountEngine;
    private Dictionary<string, float> _precipTypeEncoding = new();
    private readonly ILogger<GrittingPredictionService> _logger;
    private readonly IRouteService _routeService;

    // Constants for duration and spread rate calculations
    // Average speed of gritter truck in km per 10 minutes
    private const float GritterSpeedKmPer10Min = 3.0f;
    // Base setup time in minutes before route gritting begins
    private const int BaseSetupTimeMin = 5;
    // Conversion factor for duration calculation (10 minutes per speed unit)
    private const float DurationFactorMin = 10f;

    public bool ModelsLoaded => _decisionModel != null && _amountModel != null;

    public GrittingPredictionService(ILogger<GrittingPredictionService> logger, IRouteService routeService)
    {
        _mlContext = new MLContext(seed: 42);
        _logger = logger;
        _routeService = routeService;
    }

    /// <summary>
    /// Get all routes
    /// </summary>
    public List<RouteInfo> GetRoutes() => _routeService.GetRoutes();

    /// <summary>
    /// Check if route exists
    /// </summary>
    public bool RouteExists(string routeId) => _routeService.RouteExists(routeId);

    /// <summary>
    /// Create features from route and weather data
    /// </summary>
    public GrittingFeatures CreateFeatures(string routeId, WeatherData weather)
    {
        var route = _routeService.GetRoute(routeId);
        if (route == null)
            throw new ArgumentException($"Route {routeId} not found");

        var precipType = SanitizePrecipitationType(weather.precipitation_type);
        var iceRisk = CalculateIceRisk(weather.road_surface_temp_c, weather.temperature_c, weather.precipitation_prob_pct);
        var snowRisk = CalculateSnowRisk(weather.temperature_c, precipType, weather.precipitation_prob_pct);

        return new GrittingFeatures
        {
            priority = route.priority,
            temperature_c = weather.temperature_c,
            feels_like_c = weather.feels_like_c,
            humidity_pct = weather.humidity_pct,
            wind_speed_kmh = weather.wind_speed_kmh,
            precipitation_type_encoded = _precipTypeEncoding.GetValueOrDefault(precipType, 0),
            precipitation_prob_pct = weather.precipitation_prob_pct,
            road_surface_temp_c = weather.road_surface_temp_c,
            forecast_min_temp_c = weather.forecast_min_temp_c,
            ice_risk_encoded = EncodeRisk(iceRisk),
            snow_risk_encoded = EncodeRisk(snowRisk),
            route_length_km = route.route_length_km,
            temp_below_zero = weather.temperature_c < 0 ? 1 : 0,
            surface_temp_below_zero = weather.road_surface_temp_c < 0 ? 1 : 0,
            high_precip_prob = weather.precipitation_prob_pct > 60 ? 1 : 0
        };
    }

    /// <summary>
    /// Calculate ice risk level
    /// </summary>
    private string CalculateIceRisk(float roadTemp, float airTemp, float precipProb)
    {
        if (roadTemp <= -2 && precipProb > 60) return "high";
        if (roadTemp <= 0 && precipProb > 40) return "high";
        if (roadTemp <= 1 && precipProb > 50) return "medium";
        if (airTemp <= 0) return "medium";
        return "low";
    }

    /// <summary>
    /// Calculate snow risk level
    /// </summary>
    private string CalculateSnowRisk(float temp, string precipType, float precipProb)
    {
        if (precipType == "snow" && precipProb > 70) return "high";
        if (precipType == "sleet" && precipProb > 60) return "medium";
        if (precipType == "snow" && precipProb > 40) return "medium";
        return "low";
    }

    /// <summary>
    /// Encode risk level to numeric
    /// </summary>
    private float EncodeRisk(string risk) => risk switch
    {
        "low" => 0,
        "medium" => 1,
        "high" => 2,
        _ => 0
    };

    /// <summary>
    /// Sanitize precipitation type to handle unknown values
    /// </summary>
    private string SanitizePrecipitationType(string precipType)
    {
        if (string.IsNullOrEmpty(precipType)) return "none";

        var knownTypes = new[] { "none", "rain", "sleet", "snow" };
        if (knownTypes.Contains(precipType)) return precipType;

        var lower = precipType.ToLower();
        
        if (lower.Contains("snow") || lower.Contains("blizzard") || lower.Contains("flurr"))
            return "snow";
        
        if (lower.Contains("sleet") || lower.Contains("ice") || lower.Contains("hail") || lower.Contains("freez"))
            return "sleet";
        
        if (lower.Contains("rain") || lower.Contains("drizzle") || lower.Contains("shower") || lower.Contains("storm"))
            return "rain";
        
        return "none";
    }

    /// <summary>
    /// Make prediction
    /// </summary>
    public PredictionResult Predict(string routeId, WeatherData weather)
    {
        if (_decisionEngine == null || _amountEngine == null)
            throw new InvalidOperationException("Models not loaded");

        var route = _routeService.GetRoute(routeId);
        if (route == null)
            throw new ArgumentException($"Route {routeId} not found");

        var sanitizedWeather = new WeatherData
        {
            temperature_c = weather.temperature_c,
            feels_like_c = weather.feels_like_c,
            humidity_pct = weather.humidity_pct,
            wind_speed_kmh = weather.wind_speed_kmh,
            precipitation_type = SanitizePrecipitationType(weather.precipitation_type),
            precipitation_prob_pct = weather.precipitation_prob_pct,
            road_surface_temp_c = weather.road_surface_temp_c,
            forecast_min_temp_c = weather.forecast_min_temp_c
        };

        var features = CreateFeatures(routeId, sanitizedWeather);
        var iceRisk = CalculateIceRisk(weather.road_surface_temp_c, weather.temperature_c, weather.precipitation_prob_pct);
        var snowRisk = CalculateSnowRisk(weather.temperature_c, sanitizedWeather.precipitation_type, weather.precipitation_prob_pct);

        // Predict decision
        var decisionPred = _decisionEngine.Predict(features);
        var decision = decisionPred.Decision;
        var confidence = decisionPred.Probability;

        // Predict amount if gritting
        int saltAmount = 0;
        int spreadRate = 0;
        int estimatedDuration = 0;

        if (decision)
        {
            var amountPred = _amountEngine.Predict(features);
            saltAmount = (int)amountPred.Amount;
            // Spread rate (g/m²) = total salt (kg) / route area (km * 1000m) * 1000 (kg to g)
            // Simplified: salt_kg / route_km gives g/m² directly
            spreadRate = (int)(saltAmount / route.route_length_km);
            // Duration = (route length / gritter speed) * time factor + base setup time
            estimatedDuration = (int)(route.route_length_km / GritterSpeedKmPer10Min * DurationFactorMin) + BaseSetupTimeMin;
        }

        var recommendation = GenerateRecommendation(route, weather, iceRisk, snowRisk, decision);

        return new PredictionResult
        {
            route_id = routeId,
            route_name = route.route_name,
            gritting_decision = decision ? "yes" : "no",
            decision_confidence = Math.Round(confidence, 3),
            salt_amount_kg = saltAmount,
            spread_rate_g_m2 = spreadRate,
            estimated_duration_min = estimatedDuration,
            ice_risk = iceRisk,
            snow_risk = snowRisk,
            recommendation = recommendation
        };
    }

    /// <summary>
    /// Generate human-readable recommendation
    /// </summary>
    private string GenerateRecommendation(RouteInfo route, WeatherData weather, string iceRisk, string snowRisk, bool decision)
    {
        if (!decision)
            return "No gritting required - conditions safe";

        var reasons = new List<string>();
        if (iceRisk == "high") reasons.Add("high ice risk");
        if (snowRisk == "high") reasons.Add("high snow risk");
        if (weather.road_surface_temp_c < -3) reasons.Add("very low road temperature");
        if (weather.precipitation_prob_pct > 80) reasons.Add("high precipitation probability");

        var priorityText = route.priority == 1 ? "High priority" : "Medium priority";

        if (reasons.Any())
            return $"{priorityText} - {string.Join(", ", reasons)}";
        else
            return $"{priorityText} - preventive gritting recommended";
    }

    /// <summary>
    /// Load models from disk
    /// </summary>
    public void LoadModels(string modelsDir)
    {
        var decisionPath = Path.Combine(modelsDir, "decision_model.zip");
        var amountPath = Path.Combine(modelsDir, "amount_model.zip");
        var encodingPath = Path.Combine(modelsDir, "precip_encoding.json");

        if (!File.Exists(decisionPath) || !File.Exists(amountPath))
            throw new FileNotFoundException("Model files not found. Please train the models first.");

        _decisionModel = _mlContext.Model.Load(decisionPath, out _);
        _amountModel = _mlContext.Model.Load(amountPath, out _);

        if (File.Exists(encodingPath))
        {
            var json = File.ReadAllText(encodingPath);
            _precipTypeEncoding = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, float>>(json) ?? new();
        }

        _decisionEngine = _mlContext.Model.CreatePredictionEngine<GrittingFeatures, DecisionPrediction>(_decisionModel);
        _amountEngine = _mlContext.Model.CreatePredictionEngine<GrittingFeatures, AmountPrediction>(_amountModel);

        _logger.LogInformation("Models loaded successfully from {Path}", modelsDir);
    }
}
