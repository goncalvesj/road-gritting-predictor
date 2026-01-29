import requests
import json

# Example 1: Direct prediction with weather data
def example_direct_prediction():
    url = "http://localhost:5000/predict"
    
    payload = {
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
    }
    
    response = requests.post(url, json=payload)
    result = response.json()
    
    print("Direct Prediction Result:")
    print(json.dumps(result, indent=2))
    
    if result['success']:
        pred = result['prediction']
        print(f"\n✅ Route: {pred['route_name']}")
        print(f"Decision: {pred['gritting_decision'].upper()}")
        if pred['gritting_decision'] == 'yes':
            print(f"Salt needed: {pred['salt_amount_kg']} kg")
            print(f"Spread rate: {pred['spread_rate_g_m2']} g/m²")
            print(f"Duration: {pred['estimated_duration_min']} minutes")
        print(f"Recommendation: {pred['recommendation']}")


# Example 2: Auto-fetch weather from API
def example_auto_weather():
    url = "http://localhost:5000/predict/auto-weather"
    
    payload = {
        "route_id": "R002",
        "latitude": 55.9533,  # Edinburgh coordinates
        "longitude": -3.1883
    }
    
    response = requests.post(url, json=payload)
    result = response.json()
    
    print("\nAuto-Weather Prediction Result:")
    print(json.dumps(result, indent=2))


# Example 3: Get all routes
def example_get_routes():
    url = "http://localhost:5000/routes"
    response = requests.get(url)
    result = response.json()
    
    print("\nAvailable Routes:")
    for route in result['routes']:
        print(f"  {route['route_id']}: {route['route_name']} "
              f"(Priority {route['priority']}, {route['length_km']} km)")


if __name__ == "__main__":
    example_direct_prediction()
    # example_auto_weather()  # Uncomment when API key is configured
    # example_get_routes()