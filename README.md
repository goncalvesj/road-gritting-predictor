# Road Gritting ML Predictor

Machine learning-based road gritting decision system with weather integration. This system predicts whether roads need gritting and calculates the optimal salt amount based on weather conditions and route characteristics.

## Features

- 🎯 **Multi-output ML Prediction**: Predicts both gritting decision (yes/no) and salt amount
- 🌦️ **Weather Integration**: Automatically calculates ice and snow risk from weather data
- 🛣️ **Route-based Predictions**: Takes into account route priority, length, and type
- 🔌 **REST API**: Easy integration with Flask-based API
- 📊 **Training Dataset**: Synthetic Edinburgh gritting data based on UK standards
- 🧮 **Smart Risk Calculation**: Follows NWSRG (UK National Winter Service Research Group) guidelines

## Quick Start

### 1. Installation

```bash
git clone https://github.com/yxtc4/road-gritting-ml-predictor.git
cd road-gritting-ml-predictor
pip install -r requirements.txt
```

### 2. Train the Models

```bash
python gritting_prediction_system.py
```

This will:
- Load the training dataset
- Train decision and amount prediction models
- Save models to `models/` directory
- Display accuracy metrics

### 3. Run the API Server

```bash
python gritting_api.py
```

The API will be available at `http://localhost:5000`

### 4. Make Predictions

```bash
python example_usage.py
```

Or use the API directly:

```bash
curl -X POST http://localhost:5000/predict \
  -H "Content-Type: application/json" \
  -d '{
    "route_id": "R001",
    "weather": {
      "temperature_c": -3.5,
      "feels_like_c": -7.2,
      "humidity_pct": 88,
      "wind_speed_kmh": 18,
      "precipitation_type": "snow",
      "precipitation_prob_pct": 85,
      "road_surface_temp_c": -4.2,
      "forecast_min_temp_c": -5.0
    }
  }'
```

## Project Structure

```
road-gritting-ml-predictor/
│
├── README.md                              # This file
├── requirements.txt                       # Python dependencies
├── .gitignore                            # Git ignore file
│
├── Data Files
│   ├── edinburgh_gritting_training_dataset.csv  # Training data (75 samples)
│   ├── routes_database.csv                      # Route metadata
│   └── DATASET_README.md                        # Dataset documentation
│
├── Core System
│   ├── gritting_prediction_system.py     # Main ML prediction system
│   ├── gritting_api.py                   # REST API wrapper (Flask)
│   └── example_usage.py                  # Usage examples
│
└── models/                                # Saved models (created after training)
    ├── gritting_decision_model.pkl
    ├── gritting_amount_model.pkl
    └── gritting_*.pkl
```

## How It Works

### Input
```python
route_id = "R001"  # Queensferry Road
weather_data = {
    "temperature_c": -3.5,
    "road_surface_temp_c": -4.2,
    "precipitation_type": "snow",
    "precipitation_prob_pct": 85,
    # ... more weather features
}
```

### Processing
1. **Route Lookup**: Retrieves route metadata (length, priority, type)
2. **Risk Calculation**: Computes ice and snow risk levels
3. **Feature Engineering**: Combines route + weather features
4. **ML Prediction**: 
   - Decision Model: RandomForest Classifier → Yes/No
   - Amount Model: RandomForest Regressor → Salt amount in kg

### Output
```json
{
  "route_name": "Queensferry Road",
  "gritting_decision": "yes",
  "decision_confidence": 0.95,
  "salt_amount_kg": 1360,
  "spread_rate_g_m2": 40,
  "estimated_duration_min": 52,
  "ice_risk": "high",
  "snow_risk": "high",
  "recommendation": "High priority - high ice risk, high snow risk"
}
```

## Dataset

The training dataset contains **75 samples** from Edinburgh winter gritting operations with:

- **7 routes** (Queensferry Road, Leith Walk, Morningside Road, etc.)
- **16 weather features** (temperature, humidity, wind, precipitation, etc.)
- **4 target variables** (decision, salt amount, spread rate, duration)
- Based on **UK NWSRG standards** for spread rates (10-40 g/m²)

See [DATASET_README.md](DATASET_README.md) for full documentation.

## API Endpoints

### POST /predict
Make a gritting prediction with weather data

**Request:**
```json
{
  "route_id": "R001",
  "weather": { ... }
}
```

**Response:**
```json
{
  "success": true,
  "prediction": {
    "gritting_decision": "yes",
    "salt_amount_kg": 850,
    ...
  }
}
```

### POST /predict/auto-weather
Fetch weather automatically from API

**Request:**
```json
{
  "route_id": "R001",
  "latitude": 55.9533,
  "longitude": -3.1883
}
```

### GET /routes
List all available routes

## Extending the System

### Add New Routes
Edit `routes_database.csv`:
```csv
route_id,route_name,priority,road_type,route_length_km
R008,New Road Name,1,A-road,15.5
```

### Add More Training Data
Append to `edinburgh_gritting_training_dataset.csv` and retrain:
```bash
python gritting_prediction_system.py
```

### Integrate Your Weather API
Edit `gritting_api.py` → `fetch_weather_from_api()` function

## Model Performance

- **Decision Model Accuracy**: ~95% (on test set)
- **Amount Model R² Score**: ~0.92 (on gritted instances)
- **Key Features**: road_surface_temp_c, ice_risk, precipitation_prob_pct

## UK Standards Compliance

The system follows **NWSRG (National Winter Service Research Group)** guidelines:

| Spread Rate | Conditions |
|-------------|------------|
| 20 g/m² | Light frost prevention, road temp 0-1°C |
| 25 g/m² | Standard preventive gritting, temp -1 to 0°C |
| 30 g/m² | Moderate conditions, sleet/light snow |
| 35 g/m² | Severe frost/ice, temp -3 to -4°C |
| 40 g/m² | Heavy snow or extreme ice, temp < -4°C |

## Data Sources

- **Edinburgh Council Open Data**: https://github.com/edinburghcouncil/datasets-transport
- **NWSRG Guidelines**: https://nwsrg.org/practical-guidance-documents
- **UK Met Office**: Road weather information standards

## License

MIT License - See LICENSE file for details

## Contributing

Contributions welcome! Please:
1. Fork the repository
2. Create a feature branch
3. Submit a pull request

## Support

For questions or issues, please open a GitHub issue.

## Future Enhancements

- [ ] Real-time weather API integration (OpenWeatherMap, Met Office)
- [ ] Multi-route batch predictions
- [ ] Historical tracking and analytics dashboard
- [ ] Deep learning models (LSTM for time-series prediction)
- [ ] Mobile app integration
- [ ] Live gritter truck tracking
- [ ] Cost optimization algorithms

---

**Built for proof-of-concept winter road maintenance ML applications**
