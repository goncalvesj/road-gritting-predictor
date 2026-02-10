from flask import Flask, request, jsonify
from flask_cors import CORS
from gritting_predictor import GrittingPredictor
from gritting_data_service import create_route_service
from open_meteo_weather_service import OpenMeteoWeatherService, OpenMeteoWeatherServiceError
import requests
import os
import math
import logging

from azure.monitor.opentelemetry import configure_azure_monitor
from opentelemetry import trace
from opentelemetry.instrumentation.flask import FlaskInstrumentor
from opentelemetry.instrumentation.requests import RequestsInstrumentor
from opentelemetry.sdk.resources import Resource

# Configure Azure Monitor OpenTelemetry
# Picks up APPLICATIONINSIGHTS_CONNECTION_STRING from environment automatically.
# Resource attributes map to App Insights fields for querying:
#   service.name       → cloud_RoleName
#   service.version    → application_Version
#   service.instance.id → cloud_RoleInstance
if os.environ.get('APPLICATIONINSIGHTS_CONNECTION_STRING'):
    configure_azure_monitor(
        resource=Resource.create({
            "service.name": "gritting-api",
            "service.version": "1.0.0",
            "service.instance.id": os.environ.get("HOSTNAME", "local"),
            "service.namespace": "road-gritting-predictor",
        })
    )

tracer = trace.get_tracer(__name__)

app = Flask(__name__)
CORS(app)

# Explicitly instrument the Flask app and outbound requests library.
# This ensures HTTP requests appear in App Insights Requests/Dependencies tables.
FlaskInstrumentor().instrument_app(app)
RequestsInstrumentor().instrument()

# Initialize route service (SQLite by default, CSV fallback)
route_service = None

# Initialize the prediction system (lazy-load models)
predictor = None
_models_loaded = False

# Initialize weather service
weather_service = OpenMeteoWeatherService()


def get_predictor():
    """
    Lazy-load the predictor and models.
    Returns the predictor if models are loaded, raises error otherwise.
    """
    global predictor, _models_loaded, route_service
    
    if route_service is None:
        route_service = create_route_service()
    
    if predictor is None:
        predictor = GrittingPredictor(route_lookup=route_service.route_lookup)
    
    if not _models_loaded:
        try:
            predictor.load_models('models/gritting')
            _models_loaded = True
        except FileNotFoundError as e:
            raise RuntimeError(
                "Models not found. Please train the models first by running: "
                "python model_trainer.py"
            ) from e
    
    return predictor


def validate_weather_data(weather):
    """
    Validate weather data has all required fields with valid values.
    Returns tuple (is_valid, error_message).
    """
    required_fields = [
        'temperature_c',
        'feels_like_c', 
        'humidity_pct',
        'wind_speed_kmh',
        'precipitation_type',
        'precipitation_prob_pct',
        'road_surface_temp_c',
        'forecast_min_temp_c'
    ]
    
    if not isinstance(weather, dict):
        return False, "weather must be an object"
    
    missing_fields = [f for f in required_fields if f not in weather]
    if missing_fields:
        return False, f"Missing required weather fields: {', '.join(missing_fields)}"
    
    # Validate numeric fields
    numeric_fields = [
        'temperature_c', 'feels_like_c', 'humidity_pct', 
        'wind_speed_kmh', 'precipitation_prob_pct',
        'road_surface_temp_c', 'forecast_min_temp_c'
    ]
    
    for field in numeric_fields:
        value = weather[field]
        if not isinstance(value, (int, float)):
            return False, f"Field '{field}' must be a number"
        # Reject NaN and infinity values
        if math.isnan(value) or math.isinf(value):
            return False, f"Field '{field}' cannot be NaN or infinity"
    
    # Validate ranges
    if not 0 <= weather['humidity_pct'] <= 100:
        return False, "humidity_pct must be between 0 and 100"
    
    if not 0 <= weather['precipitation_prob_pct'] <= 100:
        return False, "precipitation_prob_pct must be between 0 and 100"
    
    if weather['wind_speed_kmh'] < 0:
        return False, "wind_speed_kmh cannot be negative"
    
    # Temperature sanity checks (-50°C to +50°C covers extreme but plausible conditions)
    temperature_fields = ['temperature_c', 'feels_like_c', 'road_surface_temp_c', 'forecast_min_temp_c']
    for field in temperature_fields:
        if not -50 <= weather[field] <= 50:
            return False, f"Field '{field}' must be between -50 and 50 degrees Celsius"
    
    if not isinstance(weather['precipitation_type'], str) or not weather['precipitation_type']:
        return False, "precipitation_type must be a non-empty string"
    
    return True, None


@app.route('/predict', methods=['POST'])
def predict_gritting():
    """
    API endpoint for gritting predictions
    
    POST /predict
    Body: {
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
    """
    try:
        # Validate JSON body
        data = request.json
        if data is None:
            return jsonify({
                'success': False,
                'error': 'Request body must be valid JSON'
            }), 400
        
        # Validate required fields
        if 'route_id' not in data:
            return jsonify({
                'success': False,
                'error': 'Missing required field: route_id'
            }), 400
        
        if 'weather' not in data:
            return jsonify({
                'success': False,
                'error': 'Missing required field: weather'
            }), 400
        
        route_id = data['route_id']
        weather_data = data['weather']
        
        # Validate route_id type
        if not isinstance(route_id, str):
            return jsonify({
                'success': False,
                'error': 'route_id must be a string'
            }), 400
        
        # Validate weather data
        is_valid, error_msg = validate_weather_data(weather_data)
        if not is_valid:
            return jsonify({
                'success': False,
                'error': error_msg
            }), 400
        
        # Get predictor (with lazy model loading)
        pred = get_predictor()
        
        # Validate route exists
        if route_id not in pred.route_lookup:
            return jsonify({
                'success': False,
                'error': f"Route '{route_id}' not found. Use GET /routes to see available routes."
            }), 404
        
        # Make prediction
        with tracer.start_as_current_span("predict") as span:
            span.set_attribute("route.id", route_id)
            span.set_attribute("weather.temperature_c", weather_data['temperature_c'])
            span.set_attribute("weather.precipitation_type", weather_data['precipitation_type'])
            result = pred.predict(route_id, weather_data)
            span.set_attribute("prediction.gritting_decision", str(result.get('gritting_decision', '')))
        
        return jsonify({
            'success': True,
            'prediction': result
        }), 200
        
    except RuntimeError as e:
        logging.error("Service temporarily unavailable", exc_info=True)
        return jsonify({
            'success': False,
            'error': 'Service temporarily unavailable'
        }), 503
    except Exception as e:
        logging.error("Internal server error", exc_info=True)
        return jsonify({
            'success': False,
            'error': 'Internal server error'
        }), 500


@app.route('/predict/auto-weather', methods=['POST'])
def predict_with_auto_weather():
    """
    API endpoint that fetches weather automatically
    
    POST /predict/auto-weather
    Body: {
        "route_id": "R001",
        "latitude": 55.9533,
        "longitude": -3.1883
    }
    """
    try:
        # Validate JSON body
        data = request.json
        if data is None:
            return jsonify({
                'success': False,
                'error': 'Request body must be valid JSON'
            }), 400
        
        # Validate required fields
        required = ['route_id', 'latitude', 'longitude']
        missing = [f for f in required if f not in data]
        if missing:
            return jsonify({
                'success': False,
                'error': f"Missing required fields: {', '.join(missing)}"
            }), 400
        
        route_id = data['route_id']
        lat = data['latitude']
        lon = data['longitude']
        
        # Validate route_id type
        if not isinstance(route_id, str):
            return jsonify({
                'success': False,
                'error': 'route_id must be a string'
            }), 400
        
        # Validate coordinates
        if not isinstance(lat, (int, float)) or not isinstance(lon, (int, float)):
            return jsonify({
                'success': False,
                'error': 'latitude and longitude must be numbers'
            }), 400
        
        if not -90 <= lat <= 90:
            return jsonify({
                'success': False,
                'error': 'latitude must be between -90 and 90'
            }), 400
        
        if not -180 <= lon <= 180:
            return jsonify({
                'success': False,
                'error': 'longitude must be between -180 and 180'
            }), 400
        
        # Get predictor (with lazy model loading)
        pred = get_predictor()
        
        # Validate route exists
        if route_id not in pred.route_lookup:
            return jsonify({
                'success': False,
                'error': f"Route '{route_id}' not found. Use GET /routes to see available routes."
            }), 404
        
        # Fetch weather from API
        with tracer.start_as_current_span("fetch_weather") as span:
            span.set_attribute("weather.latitude", lat)
            span.set_attribute("weather.longitude", lon)
            weather_data = fetch_weather_from_api(lat, lon)
            span.set_attribute("weather.temperature_c", weather_data['temperature_c'])
            span.set_attribute("weather.precipitation_type", weather_data['precipitation_type'])
        
        # Make prediction
        with tracer.start_as_current_span("predict") as span:
            span.set_attribute("route.id", route_id)
            result = pred.predict(route_id, weather_data)
            span.set_attribute("prediction.gritting_decision", str(result.get('gritting_decision', '')))
        
        return jsonify({
            'success': True,
            'prediction': result,
            'weather': weather_data,
            'weather_source': 'open-meteo'
        }), 200
        
    except RuntimeError as e:
        logging.error("Service temporarily unavailable", exc_info=True)
        return jsonify({
            'success': False,
            'error': 'Service temporarily unavailable'
        }), 503
    except WeatherAPIError as e:
        logging.error("Weather service unavailable", exc_info=True)
        return jsonify({
            'success': False,
            'error': 'Weather service unavailable'
        }), 502
    except Exception as e:
        logging.error("Internal server error", exc_info=True)
        return jsonify({
            'success': False,
            'error': 'Internal server error'
        }), 500


@app.route('/routes', methods=['GET'])
def get_routes():
    """Get all available routes"""
    global route_service
    try:
        if route_service is None:
            route_service = create_route_service()
        
        routes = [
            {
                'route_id': rid,
                'route_name': info['route_name'],
                'priority': info['priority'],
                'length_km': info['route_length_km'],
                'latitude': info['latitude'],
                'longitude': info['longitude']
            }
            for rid, info in route_service.route_lookup.items()
        ]
        return jsonify({'routes': routes}), 200
    except Exception as e:
        logging.error("Internal server error", exc_info=True)
        return jsonify({
            'success': False,
            'error': 'Internal server error'
        }), 500


@app.route('/history', methods=['GET'])
def get_history():
    """Get last 50 prediction history records from database"""
    import sqlite3
    try:
        db_path = '../data/gritting_data.db'
        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row
        cursor = conn.execute("""
            SELECT date, time, route_id, route_name, temperature_c, precipitation_type,
                   ice_risk, snow_risk, gritting_decision, salt_amount_kg
            FROM training_data
            ORDER BY date DESC, time DESC
            LIMIT 50
        """)
        history = []
        for row in cursor:
            history.append({
                'timestamp': f"{row['date']}T{row['time']}:00",
                'route_id': row['route_id'],
                'route_name': row['route_name'],
                'temperature_c': row['temperature_c'],
                'precipitation_type': row['precipitation_type'],
                'ice_risk': row['ice_risk'],
                'snow_risk': row['snow_risk'],
                'gritting_decision': 'yes' if row['gritting_decision'] == 'gritted' else 'no',
                'salt_amount_kg': row['salt_amount_kg']
            })
        conn.close()
        return jsonify({'history': history}), 200
    except Exception as e:
        logging.error("Internal server error", exc_info=True)
        return jsonify({'success': False, 'error': 'Internal server error'}), 500


@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    global _models_loaded
    try:
        # Attempt to initialize predictor and load models if not already done
        get_predictor()

        # If models are loaded, the service is healthy
        if _models_loaded:
            return jsonify({
                'status': 'healthy',
                'models_loaded': True
            }), 200

        # If initialization returned without error but models are not loaded,
        # treat this as a degraded/unready state.
        return jsonify({
            'status': 'unhealthy',
            'models_loaded': False,
            'error': 'Models are not loaded'
        }), 503
    except RuntimeError as e:
        # Expected failure mode when models are missing or cannot be loaded
        logging.error("Service initialization failed", exc_info=True)
        return jsonify({
            'status': 'unhealthy',
            'models_loaded': False,
            'error': 'Service initialization failed'
        }), 503
    except Exception as e:
        # Unexpected internal error during initialization
        logging.error("Internal server error", exc_info=True)
        return jsonify({
            'status': 'unhealthy',
            'models_loaded': False,
            'error': 'Internal server error'
        }), 500
class WeatherAPIError(Exception):
    """Custom exception for weather API errors"""
    pass


def fetch_weather_from_api(lat, lon):
    """
    Fetch weather data from weather API.
    Uses Open-Meteo as the primary provider (no API key required).
    Falls back to OpenWeatherMap if OPENWEATHER_API_KEY env var is set.
    
    Open-Meteo provides accurate precipitation probability and forecast data
    without requiring an API key, making it ideal for this application.
    """
    # Try Open-Meteo first (no API key required)
    try:
        weather_data = weather_service.fetch_weather(lat, lon)
        return weather_data
    except OpenMeteoWeatherServiceError as e:
        # If Open-Meteo fails, try OpenWeatherMap as fallback if API key is available
        api_key = os.environ.get('OPENWEATHER_API_KEY')
        if api_key:
            return fetch_weather_from_openweathermap(lat, lon, api_key)
        else:
            # No fallback available, raise the original error
            raise WeatherAPIError(f"Open-Meteo error: {str(e)}")


def fetch_weather_from_openweathermap(lat, lon, api_key):
    """
    Fetch weather data from OpenWeatherMap API (legacy fallback).
    
    Note: The OpenWeatherMap current weather endpoint (/data/2.5/weather) does not 
    include probability of precipitation (pop). This field is only available in the 
    forecast endpoint. We default to 50% when missing.
    """
    url = f"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={api_key}&units=metric"
    
    try:
        response = requests.get(url, timeout=10)
        response.raise_for_status()
        data = response.json()
    except requests.exceptions.Timeout:
        raise WeatherAPIError("Weather API request timed out")
    except requests.exceptions.ConnectionError:
        raise WeatherAPIError("Could not connect to weather API")
    except requests.exceptions.HTTPError as e:
        if response.status_code == 401:
            raise WeatherAPIError("Invalid weather API key")
        raise WeatherAPIError(f"Weather API error: {e}")
    except requests.exceptions.RequestException as e:
        raise WeatherAPIError(f"Weather API request failed: {e}")
    
    try:
        # Map API response to our format
        # Note: 'pop' is not available in current weather endpoint, defaults to 50%
        weather_data = {
            'temperature_c': data['main']['temp'],
            'feels_like_c': data['main']['feels_like'],
            'humidity_pct': data['main']['humidity'],
            'wind_speed_kmh': data.get('wind', {}).get('speed', 0) * 3.6,  # m/s to km/h
            'precipitation_type': map_weather_condition(data['weather'][0]['main']),
            'precipitation_prob_pct': data.get('pop', 0.5) * 100,  # Default 50% - see docstring
            'road_surface_temp_c': data['main']['temp'] - 1.5,  # Estimate
            'forecast_min_temp_c': data['main']['temp_min']
        }
    except KeyError as e:
        raise WeatherAPIError(f"Unexpected weather API response format: missing {e}")
    
    return weather_data


def map_weather_condition(condition):
    """Map API weather condition to our categories"""
    mapping = {
        'Clear': 'none',
        'Clouds': 'none',
        'Rain': 'rain',
        'Drizzle': 'rain',
        'Snow': 'snow',
        'Sleet': 'sleet',
        'Mist': 'none',
        'Fog': 'none'
    }
    return mapping.get(condition, 'none')


if __name__ == '__main__':
    # Note: debug=True should only be used in development
    # Set FLASK_DEBUG=1 environment variable to enable debug mode
    debug_mode = os.environ.get('FLASK_DEBUG', '0') == '1'
    app.run(debug=debug_mode, host='0.0.0.0', port=8080)