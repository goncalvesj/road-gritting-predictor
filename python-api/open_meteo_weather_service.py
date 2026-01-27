"""
Open-Meteo Weather Service for fetching weather data.
Open-Meteo is a free, open-source weather API that requires no API key.
"""
import requests
from typing import Dict


class OpenMeteoWeatherServiceError(Exception):
    """Custom exception for Open-Meteo weather service errors"""
    pass


class OpenMeteoWeatherService:
    """
    Service for fetching weather data from Open-Meteo API.
    
    Open-Meteo provides free weather data with no API key required.
    API Documentation: https://open-meteo.com/en/docs
    """
    
    BASE_URL = "https://api.open-meteo.com/v1/forecast"
    
    # Constants for weather estimation
    # Road surface temperature is typically slightly lower than air temperature due to thermal radiation
    ROAD_SURFACE_TEMP_OFFSET_C = 1.5
    
    def __init__(self):
        """Initialize the Open-Meteo weather service"""
        pass
    
    def fetch_weather(self, latitude: float, longitude: float) -> Dict[str, float]:
        """
        Fetch current weather data from Open-Meteo API.
        
        Args:
            latitude: Location latitude (-90 to 90)
            longitude: Location longitude (-180 to 180)
            
        Returns:
            dict: Weather data in the format expected by the gritting prediction system:
                {
                    'temperature_c': float,
                    'feels_like_c': float,
                    'humidity_pct': float,
                    'wind_speed_kmh': float,
                    'precipitation_type': str,
                    'precipitation_prob_pct': float,
                    'road_surface_temp_c': float,
                    'forecast_min_temp_c': float
                }
                
        Raises:
            OpenMeteoWeatherServiceError: If the API request fails
        """
        # Request current weather and hourly forecast for next 24 hours
        params = {
            'latitude': latitude,
            'longitude': longitude,
            'current': ','.join([
                'temperature_2m',
                'apparent_temperature',  # feels like temperature
                'relative_humidity_2m',
                'wind_speed_10m',
                'precipitation',
                'weather_code'
            ]),
            'hourly': ','.join([
                'temperature_2m',
                'precipitation_probability'
            ]),
            'forecast_days': 1,
            'timezone': 'auto'
        }
        
        try:
            response = requests.get(self.BASE_URL, params=params, timeout=10)
            response.raise_for_status()
            data = response.json()
        except requests.exceptions.Timeout:
            raise OpenMeteoWeatherServiceError("Weather API request timed out")
        except requests.exceptions.ConnectionError:
            raise OpenMeteoWeatherServiceError("Could not connect to weather API")
        except requests.exceptions.HTTPError as e:
            raise OpenMeteoWeatherServiceError(f"Weather API error: {e}")
        except requests.exceptions.RequestException as e:
            raise OpenMeteoWeatherServiceError(f"Weather API request failed: {e}")
        
        try:
            current = data['current']
            hourly = data.get('hourly', {})
            
            # Get current weather
            temperature_c = current['temperature_2m']
            feels_like_c = current['apparent_temperature']
            humidity_pct = current['relative_humidity_2m']
            wind_speed_kmh = current['wind_speed_10m']
            weather_code = current['weather_code']
            
            # Map weather code to precipitation type
            precipitation_type = self._map_weather_code_to_precipitation(weather_code)
            
            # Get precipitation probability from hourly forecast (next hour)
            precipitation_prob_pct = 0.0
            if 'precipitation_probability' in hourly and hourly['precipitation_probability']:
                # Get the first non-null probability value (current or next hour)
                for prob in hourly['precipitation_probability'][:3]:  # Check first 3 hours
                    if prob is not None:
                        precipitation_prob_pct = float(prob)
                        break
            
            # Get minimum temperature from hourly forecast (next 24 hours)
            forecast_min_temp_c = temperature_c
            if 'temperature_2m' in hourly and hourly['temperature_2m']:
                temps = [t for t in hourly['temperature_2m'] if t is not None]
                if temps:
                    forecast_min_temp_c = min(temps)
            
            # Estimate road surface temperature
            road_surface_temp_c = temperature_c - self.ROAD_SURFACE_TEMP_OFFSET_C
            
            return {
                'temperature_c': temperature_c,
                'feels_like_c': feels_like_c,
                'humidity_pct': humidity_pct,
                'wind_speed_kmh': wind_speed_kmh,
                'precipitation_type': precipitation_type,
                'precipitation_prob_pct': precipitation_prob_pct,
                'road_surface_temp_c': road_surface_temp_c,
                'forecast_min_temp_c': forecast_min_temp_c
            }
        except KeyError as e:
            raise OpenMeteoWeatherServiceError(f"Unexpected weather API response format: missing {e}")
    
    def _map_weather_code_to_precipitation(self, weather_code: int) -> str:
        """
        Map Open-Meteo WMO weather codes to precipitation types.
        
        WMO Weather interpretation codes (WW):
        0: Clear sky
        1, 2, 3: Mainly clear, partly cloudy, and overcast
        45, 48: Fog
        51, 53, 55: Drizzle
        56, 57: Freezing Drizzle
        61, 63, 65: Rain
        66, 67: Freezing Rain
        71, 73, 75: Snow fall
        77: Snow grains
        80, 81, 82: Rain showers
        85, 86: Snow showers
        95: Thunderstorm
        96, 99: Thunderstorm with hail
        
        Args:
            weather_code: WMO weather code from Open-Meteo
            
        Returns:
            str: One of 'none', 'rain', 'sleet', 'snow'
        """
        # Snow conditions
        if weather_code in [71, 73, 75, 77, 85, 86]:
            return 'snow'
        
        # Sleet/freezing conditions (freezing rain, freezing drizzle, thunderstorm with hail)
        if weather_code in [56, 57, 66, 67, 96, 99]:
            return 'sleet'
        
        # Rain conditions (drizzle, rain, rain showers, thunderstorm)
        if weather_code in [51, 53, 55, 61, 63, 65, 80, 81, 82, 95]:
            return 'rain'
        
        # Clear, cloudy, fog - no precipitation
        return 'none'
