"""
Unit test for Open-Meteo weather service (with mocked HTTP responses)
"""
import json
from unittest.mock import Mock, patch

from open_meteo_weather_service import OpenMeteoWeatherService

# Mock response data from Open-Meteo API
mock_response_data = {
    "latitude": 55.95,
    "longitude": -3.19,
    "current": {
        "time": "2024-01-15T12:00",
        "temperature_2m": -2.5,
        "apparent_temperature": -6.0,
        "relative_humidity_2m": 85.0,
        "wind_speed_10m": 18.0,
        "precipitation": 0.5,
        "weather_code": 71  # Snow fall
    },
    "hourly": {
        "time": ["2024-01-15T12:00", "2024-01-15T13:00", "2024-01-15T14:00"],
        "temperature_2m": [-2.5, -3.0, -3.5],
        "precipitation_probability": [80.0, 85.0, 90.0]
    }
}

print("Testing Open-Meteo Weather Service (mocked)")
print("=" * 60)

# Mock the requests.get call
with patch('open_meteo_weather_service.requests.get') as mock_get:
    # Configure mock
    mock_response = Mock()
    mock_response.status_code = 200
    mock_response.json.return_value = mock_response_data
    mock_get.return_value = mock_response
    
    # Test the service
    service = OpenMeteoWeatherService()
    
    try:
        weather_data = service.fetch_weather(55.9533, -3.1883)
        
        print("\n✓ Successfully parsed weather data")
        print(json.dumps(weather_data, indent=2))
        
        # Validate the data
        assert weather_data['temperature_c'] == -2.5, "Temperature mismatch"
        assert weather_data['feels_like_c'] == -6.0, "Feels like temperature mismatch"
        assert weather_data['humidity_pct'] == 85.0, "Humidity mismatch"
        assert weather_data['wind_speed_kmh'] == 18.0, "Wind speed mismatch"
        assert weather_data['precipitation_type'] == 'snow', "Precipitation type should be snow"
        assert weather_data['precipitation_prob_pct'] == 80.0, "Precipitation probability mismatch"
        assert weather_data['road_surface_temp_c'] == -4.0, f"Road surface temp should be -4.0, got {weather_data['road_surface_temp_c']}"
        assert weather_data['forecast_min_temp_c'] == -3.5, "Forecast min temp mismatch"
        
        print("\n✓ All validations passed!")
        print("\nWeather code mapping test:")
        print(f"  Weather code 71 (Snow) -> {service._map_weather_code_to_precipitation(71)}")
        print(f"  Weather code 61 (Rain) -> {service._map_weather_code_to_precipitation(61)}")
        print(f"  Weather code 66 (Freezing Rain) -> {service._map_weather_code_to_precipitation(66)}")
        print(f"  Weather code 0 (Clear) -> {service._map_weather_code_to_precipitation(0)}")
        
        print("\n✓ Open-Meteo integration tests passed!")
        
    except Exception as e:
        print(f"\n✗ Test failed: {e}")
        import traceback
        traceback.print_exc()
