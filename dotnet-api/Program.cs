using GrittingApi.Models;
using GrittingApi.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Determine which route service to use (SQLite by default, CSV as fallback)
var sqliteDbPath = Path.Combine("..", "data", "gritting_data.db");
if (!File.Exists(sqliteDbPath))
    sqliteDbPath = Path.Combine("data", "gritting_data.db");

var useSqlite = File.Exists(sqliteDbPath);

// Add route service (SQLite default, CSV fallback)
if (useSqlite)
{
    builder.Services.AddSingleton<IRouteService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<SqliteRouteService>>();
        var service = new SqliteRouteService(logger);
        service.LoadRoutes(sqliteDbPath);
        return service;
    });
}
else
{
    var csvPath = Path.Combine("..", "data", "routes_database.csv");
    if (!File.Exists(csvPath))
        csvPath = Path.Combine("data", "routes_database.csv");
    
    builder.Services.AddSingleton<IRouteService>(sp =>
    {
        var logger = sp.GetRequiredService<ILogger<CsvRouteService>>();
        var service = new CsvRouteService(logger);
        service.LoadRoutes(csvPath);
        return service;
    });
}

// Add services
builder.Services.AddSingleton<GrittingPredictionService>();
builder.Services.AddHttpClient<OpenMeteoWeatherService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });
builder.Services.AddHttpClient<WeatherService>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(10);
    });

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();

// Enable CORS
app.UseCors();

// Initialize prediction service
var predictionService = app.Services.GetRequiredService<GrittingPredictionService>();
var logger = app.Services.GetRequiredService<ILogger<Program>>();

// Log which data source is being used
logger.LogInformation("Using {DataSource} for route data", useSqlite ? "SQLite" : "CSV");

try
{
    // Load pre-trained models
    // Models must be trained separately using the ModelTrainer tool
    var modelsDir = "models";
    predictionService.LoadModels(modelsDir);
}
catch (FileNotFoundException ex)
{
    logger.LogError(ex, "Models not found. Please train models first using the ModelTrainer tool: dotnet run --project ModelTrainer");
}
catch (Exception ex)
{
    logger.LogError(ex, "Failed to initialize prediction service");
}

// === ENDPOINTS ===

/// <summary>
/// POST /predict - Make gritting prediction
/// </summary>
app.MapPost("/predict", async ([FromBody] PredictionRequest request, GrittingPredictionService service) =>
{
    try
    {
        // Validate request
        if (string.IsNullOrEmpty(request.route_id))
            return Results.BadRequest(new PredictionResponse { success = false, error = "Missing required field: route_id" });

        if (request.weather == null)
            return Results.BadRequest(new PredictionResponse { success = false, error = "Missing required field: weather" });

        // Validate weather data
        var (isValid, error) = ValidateWeatherData(request.weather);
        if (!isValid)
            return Results.BadRequest(new PredictionResponse { success = false, error = error });

        // Validate route exists
        if (!service.RouteExists(request.route_id))
            return Results.NotFound(new PredictionResponse 
            { 
                success = false, 
                error = $"Route '{request.route_id}' not found. Use GET /routes to see available routes." 
            });

        // Make prediction
        var result = service.Predict(request.route_id, request.weather);

        return Results.Ok(new PredictionResponse { success = true, prediction = result });
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "Prediction failed");
        return Results.Json(new PredictionResponse { success = false, error = ex.Message }, statusCode: 503);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled exception in /predict");
        return Results.Json(new PredictionResponse { success = false, error = "Internal server error" }, statusCode: 500);
    }
})
.WithName("Predict");

/// <summary>
/// POST /predict/auto-weather - Fetch weather and predict
/// </summary>
app.MapPost("/predict/auto-weather", async (
    [FromBody] AutoWeatherRequest request, 
    GrittingPredictionService service,
    WeatherService weatherService) =>
{
    try
    {
        // Validate request
        if (string.IsNullOrEmpty(request.route_id))
            return Results.BadRequest(new PredictionResponse { success = false, error = "Missing required field: route_id" });

        // Validate coordinates
        if (request.latitude < -90 || request.latitude > 90)
            return Results.BadRequest(new PredictionResponse { success = false, error = "latitude must be between -90 and 90" });

        if (request.longitude < -180 || request.longitude > 180)
            return Results.BadRequest(new PredictionResponse { success = false, error = "longitude must be between -180 and 180" });

        // Validate route exists
        if (!service.RouteExists(request.route_id))
            return Results.NotFound(new PredictionResponse 
            { 
                success = false, 
                error = $"Route '{request.route_id}' not found. Use GET /routes to see available routes." 
            });

        // Fetch weather
        var weather = await weatherService.FetchWeatherAsync(request.latitude, request.longitude);

        // Make prediction
        var result = service.Predict(request.route_id, weather);

        return Results.Ok(new PredictionResponse 
        { 
            success = true, 
            prediction = result,
            weather_source = "api"
        });
    }
    catch (InvalidOperationException ex) when (ex.Message.Contains("API"))
    {
        logger.LogError(ex, "Weather API error");
        return Results.Json(new PredictionResponse { success = false, error = ex.Message }, statusCode: 502);
    }
    catch (InvalidOperationException ex)
    {
        logger.LogError(ex, "Prediction failed");
        return Results.Json(new PredictionResponse { success = false, error = ex.Message }, statusCode: 503);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Unhandled exception in /predict/auto-weather");
        return Results.Json(new PredictionResponse { success = false, error = $"Internal server error: {ex.Message}" }, statusCode: 500);
    }
})
.WithName("PredictAutoWeather");

/// <summary>
/// GET /routes - List all routes
/// </summary>
app.MapGet("/routes", (GrittingPredictionService service) =>
{
    try
    {
        var routes = service.GetRoutes()
            .Select(r => new RouteDto
            {
                route_id = r.route_id,
                route_name = r.route_name,
                priority = r.priority,
                length_km = r.route_length_km
            })
            .ToList();

        return Results.Ok(new RoutesResponse { routes = routes });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to get routes");
        return Results.Json(new { success = false, error = $"Internal server error: {ex.Message}" }, statusCode: 500);
    }
})
.WithName("GetRoutes");

/// <summary>
/// GET /health - Health check
/// </summary>
app.MapGet("/health", (GrittingPredictionService service) =>
{
    try
    {
        if (service.ModelsLoaded)
        {
            return Results.Ok(new HealthResponse 
            { 
                status = "healthy", 
                models_loaded = true 
            });
        }
        else
        {
            return Results.Json(
                new HealthResponse 
                { 
                    status = "unhealthy", 
                    models_loaded = false,
                    error = "Models are not loaded"
                }, 
                statusCode: 503);
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Health check failed");
        return Results.Json(
            new HealthResponse 
            { 
                status = "unhealthy", 
                models_loaded = false,
                error = $"Internal server error: {ex.Message}"
            }, 
            statusCode: 500);
    }
})
.WithName("HealthCheck");

app.Run();

// === HELPER FUNCTIONS ===

/// <summary>
/// Validate weather data
/// </summary>
static (bool isValid, string? error) ValidateWeatherData(WeatherData weather)
{
    if (weather == null)
        return (false, "weather must be an object");

    // Check for NaN/Infinity
    if (float.IsNaN(weather.temperature_c) || float.IsInfinity(weather.temperature_c))
        return (false, "Field 'temperature_c' cannot be NaN or infinity");
    if (float.IsNaN(weather.feels_like_c) || float.IsInfinity(weather.feels_like_c))
        return (false, "Field 'feels_like_c' cannot be NaN or infinity");
    if (float.IsNaN(weather.humidity_pct) || float.IsInfinity(weather.humidity_pct))
        return (false, "Field 'humidity_pct' cannot be NaN or infinity");
    if (float.IsNaN(weather.wind_speed_kmh) || float.IsInfinity(weather.wind_speed_kmh))
        return (false, "Field 'wind_speed_kmh' cannot be NaN or infinity");
    if (float.IsNaN(weather.precipitation_prob_pct) || float.IsInfinity(weather.precipitation_prob_pct))
        return (false, "Field 'precipitation_prob_pct' cannot be NaN or infinity");
    if (float.IsNaN(weather.road_surface_temp_c) || float.IsInfinity(weather.road_surface_temp_c))
        return (false, "Field 'road_surface_temp_c' cannot be NaN or infinity");
    if (float.IsNaN(weather.forecast_min_temp_c) || float.IsInfinity(weather.forecast_min_temp_c))
        return (false, "Field 'forecast_min_temp_c' cannot be NaN or infinity");

    // Validate ranges
    if (weather.humidity_pct < 0 || weather.humidity_pct > 100)
        return (false, "humidity_pct must be between 0 and 100");

    if (weather.precipitation_prob_pct < 0 || weather.precipitation_prob_pct > 100)
        return (false, "precipitation_prob_pct must be between 0 and 100");

    if (weather.wind_speed_kmh < 0)
        return (false, "wind_speed_kmh cannot be negative");

    // Temperature sanity checks
    if (weather.temperature_c < -50 || weather.temperature_c > 50)
        return (false, "Field 'temperature_c' must be between -50 and 50 degrees Celsius");
    if (weather.feels_like_c < -50 || weather.feels_like_c > 50)
        return (false, "Field 'feels_like_c' must be between -50 and 50 degrees Celsius");
    if (weather.road_surface_temp_c < -50 || weather.road_surface_temp_c > 50)
        return (false, "Field 'road_surface_temp_c' must be between -50 and 50 degrees Celsius");
    if (weather.forecast_min_temp_c < -50 || weather.forecast_min_temp_c > 50)
        return (false, "Field 'forecast_min_temp_c' must be between -50 and 50 degrees Celsius");

    if (string.IsNullOrEmpty(weather.precipitation_type))
        return (false, "precipitation_type must be a non-empty string");

    return (true, null);
}
