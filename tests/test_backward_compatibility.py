"""
Backward compatibility test for the Python API
Ensures that existing functionality still works after Open-Meteo integration
"""
import sys
sys.path.insert(0, '../python-api')

from gritting_predictor import GrittingPredictor
from gritting_data_service import create_route_service
from gritting_api import validate_weather_data, map_weather_condition

print("=" * 70)
print("BACKWARD COMPATIBILITY TEST - Python API")
print("=" * 70)

# Test 1: Verify the prediction system still works
print("\n1. Testing GrittingPredictor...")
route_service = create_route_service('../data/gritting_data.db', '../data/routes_database.csv')
predictor = GrittingPredictor(route_lookup=route_service.route_lookup)
predictor.load_models('../python-api/models/gritting')

weather_data = {
    'temperature_c': -3.5,
    'feels_like_c': -7.2,
    'humidity_pct': 88,
    'wind_speed_kmh': 18,
    'precipitation_type': 'snow',
    'precipitation_prob_pct': 85,
    'road_surface_temp_c': -4.2,
    'forecast_min_temp_c': -5.0
}

result = predictor.predict('R001', weather_data)
print(f"   ✓ Prediction successful: {result['gritting_decision']}")
assert 'route_id' in result
assert 'gritting_decision' in result
assert 'salt_amount_kg' in result
print("   ✓ All expected fields present")

# Test 2: Verify weather data validation
print("\n2. Testing weather data validation...")
valid_weather = {
    'temperature_c': -2.5,
    'feels_like_c': -6.0,
    'humidity_pct': 85,
    'wind_speed_kmh': 18,
    'precipitation_type': 'snow',
    'precipitation_prob_pct': 80,
    'road_surface_temp_c': -3.0,
    'forecast_min_temp_c': -4.5
}
is_valid, error = validate_weather_data(valid_weather)
assert is_valid == True
print("   ✓ Valid weather data accepted")

invalid_weather = valid_weather.copy()
invalid_weather['humidity_pct'] = 150  # Invalid range
is_valid, error = validate_weather_data(invalid_weather)
assert is_valid == False
assert 'humidity_pct' in error
print("   ✓ Invalid weather data rejected")

# Test 3: Verify precipitation type sanitization
print("\n3. Testing precipitation type sanitization...")
test_weather = valid_weather.copy()
test_weather['precipitation_type'] = 'unknown_type'
result = predictor.predict('R001', test_weather)
assert result is not None  # Should not crash
print("   ✓ Unknown precipitation types handled gracefully")

# Test 4: Verify OpenWeatherMap fallback mapping
print("\n4. Testing OpenWeatherMap condition mapping...")
assert map_weather_condition('Clear') == 'none'
assert map_weather_condition('Rain') == 'rain'
assert map_weather_condition('Snow') == 'snow'
assert map_weather_condition('Sleet') == 'sleet'
print("   ✓ Weather condition mapping works")

# Test 5: Verify route lookup
print("\n5. Testing route lookup...")
assert 'R001' in predictor.route_lookup
assert 'R002' in predictor.route_lookup
route_info = predictor.route_lookup['R001']
assert 'route_name' in route_info
assert 'priority' in route_info
print(f"   ✓ Route lookup works (found {len(predictor.route_lookup)} routes)")

# Test 6: Verify all prediction response fields
print("\n6. Testing prediction response structure...")
expected_fields = [
    'route_id', 'route_name', 'gritting_decision', 
    'decision_confidence', 'salt_amount_kg', 'spread_rate_g_m2',
    'estimated_duration_min', 'ice_risk', 'snow_risk', 'recommendation'
]
for field in expected_fields:
    assert field in result, f"Missing field: {field}"
print(f"   ✓ All {len(expected_fields)} expected fields present in response")

print("\n" + "=" * 70)
print("✓ ALL BACKWARD COMPATIBILITY TESTS PASSED")
print("=" * 70)
print("\nConclusion: The Open-Meteo integration preserves all existing")
print("functionality. The API structure remains unchanged.")
