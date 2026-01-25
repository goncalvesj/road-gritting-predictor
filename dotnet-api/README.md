# Road Gritting ML Prediction API - .NET 10 Implementation

A production-ready .NET 10 Web API for road gritting predictions using ML.NET. This service predicts whether road gritting is needed based on weather conditions and route information.

## Features

- **ML.NET Models**: Binary classification for gritting decision, regression for salt amount
- **Separate Model Training**: Dedicated `dotnet-model-trainer` tool for training models independently from the API
- **RESTful API**: Four endpoints matching Python API functionality
- **Auto-weather**: Integration with OpenWeatherMap API
- **Containerized**: Docker support for easy deployment
- **Risk Assessment**: Automatic ice and snow risk calculation

## Architecture

The solution is split into two separate projects:
1. **dotnet-model-trainer** - Console application for training ML.NET models (separate folder)
2. **dotnet-api** - Web API that loads pre-trained models and serves predictions

This separation allows:
- Models to be trained offline or in CI/CD pipelines
- API to start faster without training overhead
- Model versioning and validation before deployment
- Compliance with ML.NET best practices

## Prerequisites

- .NET 10 Preview SDK
- Docker (for containerized deployment)
- OpenWeatherMap API key (for auto-weather endpoint)

## Quick Start

### Training Models

Before running the API, you must train the models using the ModelTrainer tool:

```bash
# From repository root
cd dotnet-model-trainer
dotnet run ../edinburgh_gritting_training_dataset.csv ../dotnet-api/models

# Or with default paths (from dotnet-model-trainer directory):
dotnet run
```

The ModelTrainer will:
- Load training data from CSV
- Train FastTree binary classification model (gritting decision)
- Train FastTree regression model (salt amount)
- Validate model format and compatibility
- Save models as `decision_model.zip` and `amount_model.zip`
- Save precipitation encoding as `precip_encoding.json`

### Running the API

Once models are trained:

```bash
# From dotnet-api directory
dotnet run

# API available at http://localhost:5000
```

### Docker Deployment

The Docker build automatically trains models during the build process:

```bash
# Build and run with Docker Compose
docker-compose up --build

# API available at http://localhost:5000
```

## API Endpoints

### 1. POST /predict
Make gritting prediction with manual weather data.

**Request:**
```json
{
  "route_id": "R001",
  "weather": {
    "temperature_c": -2.5,
    "feels_like_c": -6.0,
    "humidity_pct": 85,
    "wind_speed_kmh": 18,
    "precipitation_type": "snow",
    "precipitation_prob_pct": 80,
    "road_surface_temp_c": -3.0,
    "forecast_min_temp_c": -4.5
  }
}
```

**Response:**
```json
{
  "success": true,
  "prediction": {
    "route_id": "R001",
    "route_name": "Queensferry Road",
    "gritting_decision": "yes",
    "decision_confidence": 0.95,
    "salt_amount_kg": 850,
    "spread_rate_g_m2": 25,
    "estimated_duration_min": 45,
    "ice_risk": "high",
    "snow_risk": "high",
    "recommendation": "High priority - high ice risk, high snow risk"
  }
}
```

### 2. POST /predict/auto-weather
Fetch weather automatically from OpenWeatherMap API.

**Request:**
```json
{
  "route_id": "R001",
  "latitude": 55.9533,
  "longitude": -3.1883
}
```

**Response:** Same as /predict with additional `"weather_source": "api"` field.

**Requirements:** Set `OPENWEATHER_API_KEY` environment variable.

### 3. GET /routes
List all available routes.

**Response:**
```json
{
  "routes": [
    {
      "route_id": "R001",
      "route_name": "Queensferry Road",
      "priority": 1,
      "length_km": 17.0
    }
  ]
}
```

### 4. GET /health
Health check endpoint.

**Response:**
```json
{
  "status": "healthy",
  "models_loaded": true
}
```

## ML Models

### Model Format

Models are saved in ML.NET's native `.zip` format, which:
- Is fully compatible with ML.NET PredictionEngine
- Supports versioning and schema validation
- Can be loaded without retraining
- Is validated during the training process

### Training Data
- **Source**: `edinburgh_gritting_training_dataset.csv`
- **Routes**: `routes_database.csv`

### Features (15 total)
- Route metadata: priority, route_length_km
- Weather conditions: temperature_c, feels_like_c, humidity_pct, wind_speed_kmh, precipitation_prob_pct, road_surface_temp_c, forecast_min_temp_c
- Encoded: precipitation_type_encoded, ice_risk_encoded, snow_risk_encoded
- Engineered: temp_below_zero, surface_temp_below_zero, high_precip_prob

### Models
1. **Decision Model**: FastTree binary classifier for gritting yes/no
2. **Amount Model**: FastTree regression for salt amount prediction

### Model Validation

The ModelTrainer validates models after training:
- Verifies models can be loaded from disk
- Creates prediction engines successfully
- Runs test predictions to ensure correct format

### Risk Calculations

**Ice Risk:**
- High: road_temp ≤ -2°C and precip_prob > 60% OR road_temp ≤ 0°C and precip_prob > 40%
- Medium: road_temp ≤ 1°C and precip_prob > 50% OR air_temp ≤ 0°C
- Low: otherwise

**Snow Risk:**
- High: precipitation_type == "snow" and precip_prob > 70%
- Medium: precipitation_type == "sleet" and precip_prob > 60% OR precipitation_type == "snow" and precip_prob > 40%
- Low: otherwise

## Configuration

### Environment Variables
- `OPENWEATHER_API_KEY`: OpenWeatherMap API key (required for /predict/auto-weather)
- `ASPNETCORE_ENVIRONMENT`: Environment setting (Development/Production)
- `ASPNETCORE_URLS`: Listening URLs (default: http://+:5000)

### Retraining Models

To retrain models:

```bash
# Delete existing models
rm -rf models/

# Run ModelTrainer from dotnet-model-trainer folder
cd ../dotnet-model-trainer
dotnet run ../edinburgh_gritting_training_dataset.csv ../dotnet-api/models
```

## Project Structure

```
road-gritting-ml-predictor/
├── dotnet-api/                    # Web API project
│   ├── Models/                    # Data transfer objects and ML models
│   │   ├── WeatherData.cs
│   │   ├── PredictionRequest.cs
│   │   ├── PredictionResponse.cs
│   │   ├── RouteInfo.cs
│   │   ├── HealthResponse.cs
│   │   └── MLModels.cs
│   ├── Services/                  # Business logic
│   │   ├── GrittingPredictionService.cs
│   │   └── WeatherService.cs
│   ├── Program.cs                 # API endpoints and startup
│   ├── GrittingApi.csproj         # Project file
│   ├── Dockerfile                 # Container image
│   └── docker-compose.yml         # Container orchestration
├── dotnet-model-trainer/          # Model training tool (separate project)
│   ├── ModelTrainer.csproj
│   ├── Program.cs                 # Training logic with validation
│   └── README.md
└── edinburgh_gritting_training_dataset.csv
```

## Dependencies

- **Microsoft.ML**: ML.NET framework for machine learning
- **Microsoft.ML.FastTree**: FastTree algorithms
- **CsvHelper**: CSV parsing for routes and training data

## Validation

All weather data is validated:
- Temperature range: -50°C to +50°C
- Humidity: 0-100%
- Precipitation probability: 0-100%
- Wind speed: ≥ 0 km/h
- No NaN or Infinity values allowed

## Error Handling

- **400 Bad Request**: Invalid input data
- **404 Not Found**: Route not found
- **500 Internal Server Error**: Unexpected errors
- **502 Bad Gateway**: Weather API errors
- **503 Service Unavailable**: Models not loaded

## Performance

- Models loaded once at startup
- Prediction engines cached for fast inference
- Typical prediction time: < 10ms

## Comparison with Python API

This .NET implementation mirrors the Python Flask API exactly:
- Same endpoints and request/response formats
- Same ML logic (decision + amount models)
- Same risk calculation formulas
- Same validation rules
- Same error handling patterns

## License

MIT License - see parent repository for details.
