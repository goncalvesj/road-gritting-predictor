# Logic Apps Standard API

This is an Azure Logic Apps Standard application that orchestrates API calls for road gritting predictions.

## Overview

This Logic App demonstrates how Azure Logic Apps can act as an API orchestration layer. It:
1. Receives a request with route_id, latitude, and longitude
2. Calls the Open-Meteo weather API to get current weather conditions
3. Processes weather data using JavaScript inline code to determine precipitation type and calculate derived values (road surface temp, forecast min temp)
4. Calls the Python prediction API with the enriched weather data
5. Returns the combined prediction and weather data to the caller

## Workflow: GetPredictionApi

The workflow orchestrates calls to the weather and prediction APIs with error handling:

### Actions
1. **Get_Weather_Data** - Calls Open-Meteo API with latitude/longitude
2. **Parse_Weather_Response** - Parses the JSON response from the weather API
3. **Process_Weather_Data** - JavaScript inline code that:
   - Determines precipitation type from WMO weather codes (snow, rain, sleet, or none)
   - Calculates road surface temperature (air temp - 1.5°C)
   - Extracts forecast minimum temperature from hourly data
   - Gets precipitation probability
4. **Call_Prediction_API** - Sends enriched weather data to the Python prediction API
5. **Response** - Returns combined results

### Error Handling
- Returns HTTP 502 if the weather API call fails
- Returns HTTP 502 if the prediction API call fails
- Returns HTTP 200 with combined results on success

### Input
```json
{
  "route_id": "R001",
  "latitude": 55.9533,
  "longitude": -3.1883
}
```

### Output
```json
{
  "success": true,
  "prediction": {
    "route_id": "R001",
    "route_name": "...",
    "gritting_decision": "yes",
    "decision_confidence": 0.85,
    "salt_amount_kg": 500,
    "spread_rate_g_m2": 20,
    "estimated_duration_min": 45,
    "ice_risk": "high",
    "snow_risk": "medium",
    "recommendation": "..."
  },
  "weather": {
    "temperature_c": -2.5,
    "feels_like_c": -6.0,
    "humidity_pct": 85,
    "wind_speed_kmh": 18,
    "precipitation_type": "snow",
    "precipitation_prob_pct": 80,
    "road_surface_temp_c": -4.0,
    "forecast_min_temp_c": -5.0
  },
  "weather_source": "open-meteo"
}
```

## Local Development

### Prerequisites
- [Azure Functions Core Tools v4](https://learn.microsoft.com/en-us/azure/azure-functions/functions-run-local)
- [Visual Studio Code](https://code.visualstudio.com/) with the [Azure Logic Apps (Standard) extension](https://marketplace.visualstudio.com/items?itemName=ms-azuretools.vscode-azurelogicapps)
- [Azurite](https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite) for local storage emulation

### Setup

1. Copy the example settings file:
   ```bash
   cp local.settings.json.example local.settings.json
   ```

2. Start Azurite (storage emulator):
   ```bash
   azurite --silent
   ```

3. Start the Python prediction API (in another terminal):
   ```bash
   cd ../python-api
   python gritting_api.py
   ```

4. Start the Logic App:
   ```bash
   func start
   ```

5. The workflow will be available at: `http://localhost:7071/api/GetPredictionApi/triggers/manual/invoke`

### Testing

Use the API endpoint with a POST request:
```bash
curl -X POST http://localhost:7071/api/GetPredictionApi/triggers/manual/invoke \
  -H "Content-Type: application/json" \
  -d '{
    "route_id": "R001",
    "latitude": 55.9533,
    "longitude": -3.1883
  }'
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `PREDICTION_API_URL` | URL of the prediction API | `http://localhost:8080` |
| `AzureWebJobsStorage` | Azure Storage connection string | `UseDevelopmentStorage=true` |

## Deployment to Azure

1. Create a Logic Apps Standard resource in Azure
2. Deploy using VS Code Azure Logic Apps extension or Azure CLI:
   ```bash
   az logicapp deployment source config-zip -g <resource-group> -n <app-name> --src <zip-file>
   ```
3. Configure the `PREDICTION_API_URL` application setting to point to your deployed prediction API
