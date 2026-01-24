from flask import Flask, request, jsonify
from gritting_prediction_system import GrittingPredictionSystem
import requests
import os

app = Flask(__name__)

# Initialize the prediction system (lazy-load models)
system = None
_models_loaded = False


def get_system():
    """
    Lazy-load the prediction system and models.
    Returns the system if models are loaded, raises error otherwise.
    """
    global system, _models_loaded
    
    if system is None:
        system = GrittingPredictionSystem()
        system.load_route_database('routes_database.csv')
    
    if not _models_loaded:
        try:
            system.load_models('models/gritting')
            _models_loaded = True
        except FileNotFoundError as e:
            raise RuntimeError(
                "Models not found. Please train the models first by running: "
                "python gritting_prediction_system.py"
            ) from e
    
    return system


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
        if not isinstance(weather[field], (int, float)):
            return False, f"Field '{field}' must be a number"
    
    # Validate ranges
    if not 0 <= weather['humidity_pct'] <= 100:
        return False, "humidity_pct must be between 0 and 100"
    
    if not 0 <= weather['precipitation_prob_pct'] <= 100:
        return False, "precipitation_prob_pct must be between 0 and 100"
    
    if weather['wind_speed_kmh'] < 0:
        return False, "wind_speed_kmh cannot be negative"
    
    if not isinstance(weather['precipitation_type'], str):
        return False, "precipitation_type must be a string"
    
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
        
        # Validate weather data
        is_valid, error_msg = validate_weather_data(weather_data)
        if not is_valid:
            return jsonify({
                'success': False,
                'error': error_msg
            }), 400
        
        # Get system (with lazy model loading)
        pred_system = get_system()
        
        # Validate route exists
        if route_id not in pred_system.route_lookup:
            return jsonify({
                'success': False,
                'error': f"Route '{route_id}' not found. Use GET /routes to see available routes."
            }), 404
        
        # Make prediction
        result = pred_system.predict(route_id, weather_data)
        
        return jsonify({
            'success': True,
            'prediction': result
        }), 200
        
    except RuntimeError as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 503
    except Exception:
        # Log the full exception details server-side without exposing them to the client
        app.logger.exception("Unhandled exception in /predict endpoint")
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
        
        # Get system (with lazy model loading)
        pred_system = get_system()
        
        # Validate route exists
        if route_id not in pred_system.route_lookup:
            return jsonify({
                'success': False,
                'error': f"Route '{route_id}' not found. Use GET /routes to see available routes."
            }), 404
        
        # Fetch weather from API
        weather_data = fetch_weather_from_api(lat, lon)
        
        # Make prediction
        result = pred_system.predict(route_id, weather_data)
        
        return jsonify({
            'success': True,
            'prediction': result,
            'weather_source': 'api'
        }), 200
        
    except RuntimeError as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 503
    except WeatherAPIError as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 502
    except Exception as e:
        return jsonify({
            'success': False,
            'error': f'Internal server error: {str(e)}'
        }), 500


@app.route('/routes', methods=['GET'])
def get_routes():
    """Get all available routes"""
    try:
        pred_system = get_system()
        routes = [
            {
                'route_id': rid,
                'route_name': info['route_name'],
                'priority': info['priority'],
                'length_km': info['route_length_km']
            }
            for rid, info in pred_system.route_lookup.items()
        ]
        return jsonify({'routes': routes}), 200
    except RuntimeError as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 503
    except Exception as e:
        return jsonify({
            'success': False,
            'error': f'Internal server error: {str(e)}'
        }), 500


@app.route('/health', methods=['GET'])
def health_check():
    """Health check endpoint"""
    global _models_loaded
    try:
        # Attempt to initialize the system and load models if not already done
        get_system()

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
        return jsonify({
            'status': 'unhealthy',
            'models_loaded': False,
            'error': str(e)
        }), 503
    except Exception as e:
        # Unexpected internal error during initialization
        return jsonify({
            'status': 'unhealthy',
            'models_loaded': False,
            'error': f'Internal server error: {str(e)}'
        }), 500
class WeatherAPIError(Exception):
    """Custom exception for weather API errors"""
    pass


def fetch_weather_from_api(lat, lon):
    """
    Fetch weather data from weather API.
    Uses OpenWeatherMap by default (requires OPENWEATHER_API_KEY env var).
    """
    # Get API key from environment variable
    api_key = os.environ.get('OPENWEATHER_API_KEY')
    if not api_key:
        raise WeatherAPIError(
            "Weather API key not configured. "
            "Set the OPENWEATHER_API_KEY environment variable."
        )
    
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
        weather_data = {
            'temperature_c': data['main']['temp'],
            'feels_like_c': data['main']['feels_like'],
            'humidity_pct': data['main']['humidity'],
            'wind_speed_kmh': data.get('wind', {}).get('speed', 0) * 3.6,  # m/s to km/h
            'precipitation_type': map_weather_condition(data['weather'][0]['main']),
            'precipitation_prob_pct': data.get('pop', 0.5) * 100,  # Default 50% if not provided
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
    app.run(debug=debug_mode, host='0.0.0.0', port=5000)