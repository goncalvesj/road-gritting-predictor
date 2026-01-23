from flask import Flask, request, jsonify
from gritting_prediction_system import GrittingPredictionSystem
import requests

app = Flask(__name__)

# Initialize and load the prediction system
system = GrittingPredictionSystem()
system.load_route_database('routes_database.csv')
system.load_models('models/gritting')

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
        data = request.json
        route_id = data['route_id']
        weather_data = data['weather']
        
        # Make prediction
        result = system.predict(route_id, weather_data)
        
        return jsonify({
            'success': True,
            'prediction': result
        }), 200
        
    except Exception as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 400


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
        data = request.json
        route_id = data['route_id']
        lat = data['latitude']
        lon = data['longitude']
        
        # Fetch weather from your API (example with OpenWeatherMap)
        weather_data = fetch_weather_from_api(lat, lon)
        
        # Make prediction
        result = system.predict(route_id, weather_data)
        
        return jsonify({
            'success': True,
            'prediction': result,
            'weather_source': 'api'
        }), 200
        
    except Exception as e:
        return jsonify({
            'success': False,
            'error': str(e)
        }), 400


@app.route('/routes', methods=['GET'])
def get_routes():
    """Get all available routes"""
    routes = [
        {
            'route_id': rid,
            'route_name': info['route_name'],
            'priority': info['priority'],
            'length_km': info['route_length_km']
        }
        for rid, info in system.route_lookup.items()
    ]
    return jsonify({'routes': routes}), 200


def fetch_weather_from_api(lat, lon):
    """
    Fetch weather data from your weather API
    Adapt this to your specific API
    """
    # Example with OpenWeatherMap (replace with your API)
    api_key = "YOUR_API_KEY"
    url = f"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={api_key}&units=metric"
    
    response = requests.get(url)
    data = response.json()
    
    # Map API response to our format
    weather_data = {
        'temperature_c': data['main']['temp'],
        'feels_like_c': data['main']['feels_like'],
        'humidity_pct': data['main']['humidity'],
        'wind_speed_kmh': data['wind']['speed'] * 3.6,  # m/s to km/h
        'precipitation_type': map_weather_condition(data['weather'][0]['main']),
        'precipitation_prob_pct': data.get('pop', 0) * 100 if 'pop' in data else 50,
        'road_surface_temp_c': data['main']['temp'] - 1.5,  # Estimate
        'forecast_min_temp_c': data['main']['temp_min']
    }
    
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
    app.run(debug=True, host='0.0.0.0', port=5000)