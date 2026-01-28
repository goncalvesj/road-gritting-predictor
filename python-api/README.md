# Python Gritting Prediction API

Flask-based REST API for road gritting predictions using machine learning.

## Quick Start

### Local Development

```bash
# Install dependencies
pip install -r requirements.txt

# Train the ML models
python gritting_prediction_system.py

# Run the API server
python gritting_api.py

# Test with example usage
python example_usage.py
```

### Docker

```bash
# Build and run
docker-compose up -d

# API available at http://localhost:5000
```

## Files

| File | Description |
|------|-------------|
| `gritting_prediction_system.py` | ML model training and prediction logic |
| `gritting_api.py` | Flask REST API |
| `open_meteo_weather_service.py` | Open-Meteo weather service (primary provider) |
| `example_usage.py` | API usage examples |
| `api.http` | HTTP test file for testing API endpoints |
| `requirements.txt` | Python dependencies |

**Data files (located in `../data/`):**
- `edinburgh_gritting_training_dataset.csv` - Training data
- `routes_database.csv` - Route metadata
- `DATASET_README.md` - Dataset documentation

## API Endpoints

- `POST /predict` - Make prediction with weather data
- `POST /predict/auto-weather` - Fetch weather and predict (uses Open-Meteo by default)
- `GET /routes` - List available routes
- `GET /health` - Health check

## Weather Provider

The API uses **Open-Meteo** as the primary weather provider (no API key required). If Open-Meteo is unavailable and you have set the `OPENWEATHER_API_KEY` environment variable, the system will fall back to OpenWeatherMap.

See the main [README](../README.md) for full documentation.
