using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.FastTree;
using GrittingApi.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace GrittingApi.Services;

/// <summary>
/// ML.NET-based gritting prediction service
/// </summary>
public class GrittingPredictionService
{
    private readonly MLContext _mlContext;
    private ITransformer? _decisionModel;
    private ITransformer? _amountModel;
    private PredictionEngine<GrittingFeatures, DecisionPrediction>? _decisionEngine;
    private PredictionEngine<GrittingFeatures, AmountPrediction>? _amountEngine;
    private Dictionary<string, RouteInfo> _routeLookup = new();
    private Dictionary<string, float> _precipTypeEncoding = new();
    private readonly ILogger<GrittingPredictionService> _logger;

    // Constants for duration and spread rate calculations
    // Average speed of gritter truck in km per 10 minutes
    private const float GritterSpeedKmPer10Min = 3.0f;
    // Base setup time in minutes before route gritting begins
    private const int BaseSetupTimeMin = 5;
    // Conversion factor for duration calculation (10 minutes per speed unit)
    private const float DurationFactorMin = 10f;

    public bool ModelsLoaded => _decisionModel != null && _amountModel != null;

    public GrittingPredictionService(ILogger<GrittingPredictionService> logger)
    {
        _mlContext = new MLContext(seed: 42);
        _logger = logger;
    }

    /// <summary>
    /// Load routes from CSV file
    /// </summary>
    public void LoadRoutes(string routesPath)
    {
        using var reader = new StreamReader(routesPath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
        
        var routes = csv.GetRecords<RouteInfo>().ToList();
        _routeLookup = routes.ToDictionary(r => r.route_id);
        
        _logger.LogInformation("Loaded {Count} routes from database", _routeLookup.Count);
    }

    /// <summary>
    /// Get all routes
    /// </summary>
    public List<RouteInfo> GetRoutes() => _routeLookup.Values.ToList();

    /// <summary>
    /// Check if route exists
    /// </summary>
    public bool RouteExists(string routeId) => _routeLookup.ContainsKey(routeId);

    /// <summary>
    /// Train models from training data
    /// </summary>
    public void TrainModels(string trainingDataPath, string modelsDir)
    {
        _logger.LogInformation("Loading training data from {Path}", trainingDataPath);
        
        // Load training data
        var dataView = _mlContext.Data.LoadFromTextFile<TrainingData>(
            trainingDataPath,
            separatorChar: ',',
            hasHeader: true
        );

        // Build precipitation type encoding
        var precipTypes = _mlContext.Data.CreateEnumerable<TrainingData>(dataView, reuseRowObject: false)
            .Select(x => x.precipitation_type)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        
        _precipTypeEncoding = precipTypes.Select((type, index) => new { type, index })
            .ToDictionary(x => x.type, x => (float)x.index);

        _logger.LogInformation("Precipitation types: {Types}", string.Join(", ", _precipTypeEncoding.Keys));

        // Engineer features and prepare data
        var processedData = _mlContext.Data.CreateEnumerable<TrainingData>(dataView, reuseRowObject: false)
            .Select(row => new TrainingRow
            {
                Features = CreateFeatures(row),
                Label = row.gritting_decision == "gritted",
                Amount = row.salt_amount_kg
            })
            .ToList();

        // Train decision model
        _logger.LogInformation("Training decision model...");
        _decisionModel = TrainDecisionModel(processedData);

        // Train amount model (only on gritted instances)
        _logger.LogInformation("Training amount model...");
        var grittedData = processedData.Where(x => x.Label).ToList();
        _amountModel = TrainAmountModel(grittedData);

        // Save models
        Directory.CreateDirectory(modelsDir);
        SaveModels(modelsDir);

        // Create prediction engines
        _decisionEngine = _mlContext.Model.CreatePredictionEngine<GrittingFeatures, DecisionPrediction>(_decisionModel);
        _amountEngine = _mlContext.Model.CreatePredictionEngine<GrittingFeatures, AmountPrediction>(_amountModel);

        _logger.LogInformation("Models trained and saved successfully");
    }

    private class TrainingRow
    {
        public GrittingFeatures Features { get; set; } = new();
        public bool Label { get; set; }
        public float Amount { get; set; }
    }

    /// <summary>
    /// Train decision classification model
    /// </summary>
    private ITransformer TrainDecisionModel(List<TrainingRow> data)
    {
        var trainData = data.Select(x => new LabeledFeatures
        {
            Label = x.Label,
            priority = x.Features.priority,
            temperature_c = x.Features.temperature_c,
            feels_like_c = x.Features.feels_like_c,
            humidity_pct = x.Features.humidity_pct,
            wind_speed_kmh = x.Features.wind_speed_kmh,
            precipitation_type_encoded = x.Features.precipitation_type_encoded,
            precipitation_prob_pct = x.Features.precipitation_prob_pct,
            road_surface_temp_c = x.Features.road_surface_temp_c,
            forecast_min_temp_c = x.Features.forecast_min_temp_c,
            ice_risk_encoded = x.Features.ice_risk_encoded,
            snow_risk_encoded = x.Features.snow_risk_encoded,
            route_length_km = x.Features.route_length_km,
            temp_below_zero = x.Features.temp_below_zero,
            surface_temp_below_zero = x.Features.surface_temp_below_zero,
            high_precip_prob = x.Features.high_precip_prob
        }).ToList();

        var dataView = _mlContext.Data.LoadFromEnumerable(trainData);

        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(LabeledFeatures.priority),
                nameof(LabeledFeatures.temperature_c),
                nameof(LabeledFeatures.feels_like_c),
                nameof(LabeledFeatures.humidity_pct),
                nameof(LabeledFeatures.wind_speed_kmh),
                nameof(LabeledFeatures.precipitation_type_encoded),
                nameof(LabeledFeatures.precipitation_prob_pct),
                nameof(LabeledFeatures.road_surface_temp_c),
                nameof(LabeledFeatures.forecast_min_temp_c),
                nameof(LabeledFeatures.ice_risk_encoded),
                nameof(LabeledFeatures.snow_risk_encoded),
                nameof(LabeledFeatures.route_length_km),
                nameof(LabeledFeatures.temp_below_zero),
                nameof(LabeledFeatures.surface_temp_below_zero),
                nameof(LabeledFeatures.high_precip_prob))
            .Append(_mlContext.BinaryClassification.Trainers.FastTree(
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 5,
                learningRate: 0.1));

        return pipeline.Fit(dataView);
    }

    private class LabeledFeatures
    {
        public bool Label { get; set; }
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
    /// Train amount regression model
    /// </summary>
    private ITransformer TrainAmountModel(List<TrainingRow> data)
    {
        var trainData = data.Select(x => new RegressionFeatures
        {
            Label = x.Amount,
            priority = x.Features.priority,
            temperature_c = x.Features.temperature_c,
            feels_like_c = x.Features.feels_like_c,
            humidity_pct = x.Features.humidity_pct,
            wind_speed_kmh = x.Features.wind_speed_kmh,
            precipitation_type_encoded = x.Features.precipitation_type_encoded,
            precipitation_prob_pct = x.Features.precipitation_prob_pct,
            road_surface_temp_c = x.Features.road_surface_temp_c,
            forecast_min_temp_c = x.Features.forecast_min_temp_c,
            ice_risk_encoded = x.Features.ice_risk_encoded,
            snow_risk_encoded = x.Features.snow_risk_encoded,
            route_length_km = x.Features.route_length_km,
            temp_below_zero = x.Features.temp_below_zero,
            surface_temp_below_zero = x.Features.surface_temp_below_zero,
            high_precip_prob = x.Features.high_precip_prob
        }).ToList();

        var dataView = _mlContext.Data.LoadFromEnumerable(trainData);

        var pipeline = _mlContext.Transforms.Concatenate("Features",
                nameof(RegressionFeatures.priority),
                nameof(RegressionFeatures.temperature_c),
                nameof(RegressionFeatures.feels_like_c),
                nameof(RegressionFeatures.humidity_pct),
                nameof(RegressionFeatures.wind_speed_kmh),
                nameof(RegressionFeatures.precipitation_type_encoded),
                nameof(RegressionFeatures.precipitation_prob_pct),
                nameof(RegressionFeatures.road_surface_temp_c),
                nameof(RegressionFeatures.forecast_min_temp_c),
                nameof(RegressionFeatures.ice_risk_encoded),
                nameof(RegressionFeatures.snow_risk_encoded),
                nameof(RegressionFeatures.route_length_km),
                nameof(RegressionFeatures.temp_below_zero),
                nameof(RegressionFeatures.surface_temp_below_zero),
                nameof(RegressionFeatures.high_precip_prob))
            .Append(_mlContext.Regression.Trainers.FastTree(
                numberOfLeaves: 20,
                numberOfTrees: 100,
                minimumExampleCountPerLeaf: 5,
                learningRate: 0.1));

        return pipeline.Fit(dataView);
    }

    private class RegressionFeatures
    {
        public float Label { get; set; }
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
    /// Create features from training data row
    /// </summary>
    private GrittingFeatures CreateFeatures(TrainingData row)
    {
        return new GrittingFeatures
        {
            priority = row.priority,
            temperature_c = row.temperature_c,
            feels_like_c = row.feels_like_c,
            humidity_pct = row.humidity_pct,
            wind_speed_kmh = row.wind_speed_kmh,
            precipitation_type_encoded = _precipTypeEncoding.GetValueOrDefault(row.precipitation_type, 0),
            precipitation_prob_pct = row.precipitation_prob_pct,
            road_surface_temp_c = row.road_surface_temp_c,
            forecast_min_temp_c = row.forecast_min_temp_c,
            ice_risk_encoded = EncodeRisk(row.ice_risk),
            snow_risk_encoded = EncodeRisk(row.snow_risk),
            route_length_km = row.route_length_km,
            temp_below_zero = row.temperature_c < 0 ? 1 : 0,
            surface_temp_below_zero = row.road_surface_temp_c < 0 ? 1 : 0,
            high_precip_prob = row.precipitation_prob_pct > 60 ? 1 : 0
        };
    }

    /// <summary>
    /// Create features from route and weather data
    /// </summary>
    public GrittingFeatures CreateFeatures(string routeId, WeatherData weather)
    {
        if (!_routeLookup.TryGetValue(routeId, out var route))
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

        if (!_routeLookup.TryGetValue(routeId, out var route))
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
    /// Save models to disk
    /// </summary>
    private void SaveModels(string modelsDir)
    {
        if (_decisionModel != null)
            _mlContext.Model.Save(_decisionModel, null, Path.Combine(modelsDir, "decision_model.zip"));
        
        if (_amountModel != null)
            _mlContext.Model.Save(_amountModel, null, Path.Combine(modelsDir, "amount_model.zip"));

        // Save precipitation encoding
        var encodingPath = Path.Combine(modelsDir, "precip_encoding.json");
        File.WriteAllText(encodingPath, System.Text.Json.JsonSerializer.Serialize(_precipTypeEncoding));
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
