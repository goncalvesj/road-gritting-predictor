"""
Test script for Open-Meteo weather service integration
"""
from open_meteo_weather_service import OpenMeteoWeatherService
import json

# Initialize service
service = OpenMeteoWeatherService()

# Test with Edinburgh coordinates
print("Testing Open-Meteo Weather Service")
print("=" * 60)

# Edinburgh coordinates
lat = 55.9533
lon = -3.1883

print(f"\nFetching weather for Edinburgh (lat={lat}, lon={lon})...")

try:
    weather_data = service.fetch_weather(lat, lon)
    print("\nWeather data received:")
    print(json.dumps(weather_data, indent=2))
    
    print("\n✓ Open-Meteo integration successful!")
except Exception as e:
    print(f"\n✗ Error: {e}")
    import traceback
    traceback.print_exc()
