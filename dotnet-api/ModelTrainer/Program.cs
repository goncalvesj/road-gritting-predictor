using Microsoft.ML;
using Microsoft.ML.Data;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace GrittingApi.ModelTrainer;

/// <summary>
/// Standalone model trainer for gritting prediction ML models.
/// This tool trains ML.NET models from the training dataset and saves them
/// in a format that can be loaded by the API.
/// </summary>
class Program
{
    // Constants for model training
    private const int RandomSeed = 42;
    private const int NumberOfLeaves = 20;
    private const int NumberOfTrees = 100;
    private const int MinimumExampleCountPerLeaf = 5;
    private const double LearningRate = 0.1;

    static int Main(string[] args)
    {
        Console.WriteLine("=================================================");
        Console.WriteLine("Road Gritting ML.NET Model Trainer");
        Console.WriteLine("=================================================\n");

        // Parse command line arguments
        string trainingDataPath = args.Length > 0 ? args[0] : "../../edinburgh_gritting_training_dataset.csv";
        string outputDir = args.Length > 1 ? args[1] : "../models";

        // Validate training data exists
        if (!File.Exists(trainingDataPath))
        {
            Console.Error.WriteLine($"Error: Training data file not found: {trainingDataPath}");
            Console.Error.WriteLine("Usage: dotnet run [training_data_path] [output_dir]");
            return 1;
        }

        try
        {
            TrainAndSaveModels(trainingDataPath, outputDir);
            Console.WriteLine("\n=================================================");
            Console.WriteLine("Model training completed successfully!");
            Console.WriteLine($"Models saved to: {Path.GetFullPath(outputDir)}");
            Console.WriteLine("=================================================");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error during training: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    static void TrainAndSaveModels(string trainingDataPath, string outputDir)
    {
        var mlContext = new MLContext(seed: RandomSeed);

        Console.WriteLine($"Loading training data from: {trainingDataPath}");

        // Load training data using CsvHelper for more control
        List<TrainingData> trainingRecords;
        using (var reader = new StreamReader(trainingDataPath))
        using (var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)))
        {
            trainingRecords = csv.GetRecords<TrainingData>().ToList();
        }

        Console.WriteLine($"Loaded {trainingRecords.Count} training records");

        // Build precipitation type encoding
        var precipTypes = trainingRecords
            .Select(x => x.precipitation_type)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        var precipTypeEncoding = precipTypes
            .Select((type, index) => new { type, index })
            .ToDictionary(x => x.type, x => (float)x.index);

        Console.WriteLine($"Precipitation types found: {string.Join(", ", precipTypeEncoding.Keys)}");

        // Engineer features
        Console.WriteLine("Engineering features...");
        var processedData = trainingRecords.Select(row => new TrainingRow
        {
            Features = CreateFeatures(row, precipTypeEncoding),
            Label = row.gritting_decision == "gritted",
            Amount = row.salt_amount_kg
        }).ToList();

        // Split data for evaluation
        var grittedCount = processedData.Count(x => x.Label);
        var notGrittedCount = processedData.Count(x => !x.Label);
        Console.WriteLine($"Class distribution: {grittedCount} gritted, {notGrittedCount} not gritted");

        // Train decision model
        Console.WriteLine("\n--- Training Decision Model (Binary Classification) ---");
        var decisionModel = TrainDecisionModel(mlContext, processedData);

        // Train amount model (only on gritted instances)
        Console.WriteLine("\n--- Training Amount Model (Regression) ---");
        var grittedData = processedData.Where(x => x.Label).ToList();
        Console.WriteLine($"Training on {grittedData.Count} gritted instances");
        var amountModel = TrainAmountModel(mlContext, grittedData);

        // Save models
        Console.WriteLine("\n--- Saving Models ---");
        Directory.CreateDirectory(outputDir);

        var decisionModelPath = Path.Combine(outputDir, "decision_model.zip");
        var amountModelPath = Path.Combine(outputDir, "amount_model.zip");
        var encodingPath = Path.Combine(outputDir, "precip_encoding.json");

        mlContext.Model.Save(decisionModel, null, decisionModelPath);
        Console.WriteLine($"Decision model saved to: {decisionModelPath}");

        mlContext.Model.Save(amountModel, null, amountModelPath);
        Console.WriteLine($"Amount model saved to: {amountModelPath}");

        File.WriteAllText(encodingPath, System.Text.Json.JsonSerializer.Serialize(precipTypeEncoding));
        Console.WriteLine($"Precipitation encoding saved to: {encodingPath}");

        // Validate saved models can be loaded
        Console.WriteLine("\n--- Validating Model Format ---");
        ValidateModels(mlContext, decisionModelPath, amountModelPath);
    }

    static GrittingFeatures CreateFeatures(TrainingData row, Dictionary<string, float> precipTypeEncoding)
    {
        return new GrittingFeatures
        {
            priority = row.priority,
            temperature_c = row.temperature_c,
            feels_like_c = row.feels_like_c,
            humidity_pct = row.humidity_pct,
            wind_speed_kmh = row.wind_speed_kmh,
            precipitation_type_encoded = precipTypeEncoding.GetValueOrDefault(row.precipitation_type, 0),
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

    static float EncodeRisk(string risk) => risk switch
    {
        "low" => 0,
        "medium" => 1,
        "high" => 2,
        _ => 0
    };

    static ITransformer TrainDecisionModel(MLContext mlContext, List<TrainingRow> data)
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

        var dataView = mlContext.Data.LoadFromEnumerable(trainData);

        // Split for evaluation
        var split = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: RandomSeed);

        var pipeline = mlContext.Transforms.Concatenate("Features",
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
            .Append(mlContext.BinaryClassification.Trainers.FastTree(
                numberOfLeaves: NumberOfLeaves,
                numberOfTrees: NumberOfTrees,
                minimumExampleCountPerLeaf: MinimumExampleCountPerLeaf,
                learningRate: LearningRate));

        Console.WriteLine("Training FastTree binary classification model...");
        var model = pipeline.Fit(split.TrainSet);

        // Evaluate
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.BinaryClassification.Evaluate(predictions);
        
        Console.WriteLine($"  Accuracy: {metrics.Accuracy:P2}");
        Console.WriteLine($"  AUC: {metrics.AreaUnderRocCurve:P2}");
        Console.WriteLine($"  F1 Score: {metrics.F1Score:P2}");

        // Train on full dataset for production
        Console.WriteLine("Training final model on full dataset...");
        return pipeline.Fit(dataView);
    }

    static ITransformer TrainAmountModel(MLContext mlContext, List<TrainingRow> data)
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

        var dataView = mlContext.Data.LoadFromEnumerable(trainData);

        // Split for evaluation
        var split = mlContext.Data.TrainTestSplit(dataView, testFraction: 0.2, seed: RandomSeed);

        var pipeline = mlContext.Transforms.Concatenate("Features",
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
            .Append(mlContext.Regression.Trainers.FastTree(
                numberOfLeaves: NumberOfLeaves,
                numberOfTrees: NumberOfTrees,
                minimumExampleCountPerLeaf: MinimumExampleCountPerLeaf,
                learningRate: LearningRate));

        Console.WriteLine("Training FastTree regression model...");
        var model = pipeline.Fit(split.TrainSet);

        // Evaluate
        var predictions = model.Transform(split.TestSet);
        var metrics = mlContext.Regression.Evaluate(predictions);
        
        Console.WriteLine($"  R² Score: {metrics.RSquared:P2}");
        Console.WriteLine($"  MAE: {metrics.MeanAbsoluteError:F2}");
        Console.WriteLine($"  RMSE: {metrics.RootMeanSquaredError:F2}");

        // Train on full dataset for production
        Console.WriteLine("Training final model on full dataset...");
        return pipeline.Fit(dataView);
    }

    static void ValidateModels(MLContext mlContext, string decisionModelPath, string amountModelPath)
    {
        // Try loading the saved models to ensure format is correct
        Console.WriteLine("Loading decision model to verify format...");
        var decisionModel = mlContext.Model.Load(decisionModelPath, out var decisionSchema);
        Console.WriteLine($"  ✓ Decision model loaded successfully (schema: {decisionSchema?.Count ?? 0} columns)");

        Console.WriteLine("Loading amount model to verify format...");
        var amountModel = mlContext.Model.Load(amountModelPath, out var amountSchema);
        Console.WriteLine($"  ✓ Amount model loaded successfully (schema: {amountSchema?.Count ?? 0} columns)");

        // Create prediction engines to verify they work
        Console.WriteLine("Creating prediction engines...");
        var decisionEngine = mlContext.Model.CreatePredictionEngine<GrittingFeatures, DecisionPrediction>(decisionModel);
        var amountEngine = mlContext.Model.CreatePredictionEngine<GrittingFeatures, AmountPrediction>(amountModel);

        // Test prediction with sample data
        var testFeatures = new GrittingFeatures
        {
            priority = 1,
            temperature_c = -3.5f,
            feels_like_c = -7.0f,
            humidity_pct = 85,
            wind_speed_kmh = 18,
            precipitation_type_encoded = 3, // snow
            precipitation_prob_pct = 80,
            road_surface_temp_c = -4.0f,
            forecast_min_temp_c = -5.0f,
            ice_risk_encoded = 2, // high
            snow_risk_encoded = 2, // high
            route_length_km = 17.0f,
            temp_below_zero = 1,
            surface_temp_below_zero = 1,
            high_precip_prob = 1
        };

        var decisionPred = decisionEngine.Predict(testFeatures);
        var amountPred = amountEngine.Predict(testFeatures);

        Console.WriteLine($"  ✓ Test prediction: Decision={decisionPred.Decision}, Confidence={decisionPred.Probability:P1}, Amount={amountPred.Amount:F0}kg");
        Console.WriteLine("\nModel format validation completed successfully!");
    }
}

// Data classes for ML.NET
public class TrainingData
{
    public string route_id { get; set; } = "";
    public string route_name { get; set; } = "";
    public int priority { get; set; }
    public string road_type { get; set; } = "";
    public float route_length_km { get; set; }
    public float temperature_c { get; set; }
    public float feels_like_c { get; set; }
    public float humidity_pct { get; set; }
    public float wind_speed_kmh { get; set; }
    public string precipitation_type { get; set; } = "";
    public float precipitation_prob_pct { get; set; }
    public float road_surface_temp_c { get; set; }
    public float forecast_min_temp_c { get; set; }
    public string ice_risk { get; set; } = "";
    public string snow_risk { get; set; } = "";
    public string gritting_decision { get; set; } = "";
    public float salt_amount_kg { get; set; }
    public float spread_rate_g_m2 { get; set; }
    public float estimated_duration_min { get; set; }
}

/// <summary>
/// Strongly-typed training row for model training
/// </summary>
public class TrainingRow
{
    public GrittingFeatures Features { get; set; } = new();
    public bool Label { get; set; }
    public float Amount { get; set; }
}

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

public class DecisionPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Decision { get; set; }
    public float Probability { get; set; }
}

public class AmountPrediction
{
    [ColumnName("Score")]
    public float Amount { get; set; }
}

public class LabeledFeatures
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

public class RegressionFeatures
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
