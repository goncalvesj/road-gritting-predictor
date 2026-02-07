import { useState, useEffect } from 'react';
import type { Route, Prediction, WeatherData } from './types';

const API_URL = import.meta.env.VITE_API_URL || '/api';

interface AutoWeatherResponse {
  success: boolean;
  prediction: Prediction;
  weather: WeatherData;
  weather_source: string;
  error?: string;
}

function getPrecipitationIcon(type: string): string {
  switch (type.toLowerCase()) {
    case 'snow': return '❄️';
    case 'rain': return '🌧️';
    case 'sleet': return '🌨️';
    default: return '☀️';
  }
}

function getPrecipitationLabel(type: string): string {
  switch (type.toLowerCase()) {
    case 'snow': return 'Snow';
    case 'rain': return 'Rain';
    case 'sleet': return 'Sleet';
    case 'none': return 'Clear';
    default: return type;
  }
}

export function AutoWeatherPage() {
  const [routes, setRoutes] = useState<Route[]>([]);
  const [selectedRoute, setSelectedRoute] = useState('');
  const [latitude, setLatitude] = useState<number>(55.9533); // Edinburgh default
  const [longitude, setLongitude] = useState<number>(-3.1883);
  const [loading, setLoading] = useState(false);
  const [locating, setLocating] = useState(false);
  const [error, setError] = useState('');
  const [result, setResult] = useState<AutoWeatherResponse | null>(null);

  useEffect(() => {
    fetchRoutes();
  }, []);

  const fetchRoutes = async () => {
    try {
      const response = await fetch(`${API_URL}/routes`);
      if (!response.ok) throw new Error('Failed to fetch routes');
      const data = await response.json();
      setRoutes(data.routes);
      if (data.routes.length > 0) {
        setSelectedRoute(data.routes[0].route_id);
        setLatitude(data.routes[0].latitude);
        setLongitude(data.routes[0].longitude);
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch routes');
    }
  };

  const handleGetLocation = () => {
    if (!navigator.geolocation) {
      setError('Geolocation is not supported by your browser');
      return;
    }

    setLocating(true);
    setError('');

    navigator.geolocation.getCurrentPosition(
      (position) => {
        setLatitude(Math.round(position.coords.latitude * 10000) / 10000);
        setLongitude(Math.round(position.coords.longitude * 10000) / 10000);
        setLocating(false);
      },
      (err) => {
        setError(`Failed to get location: ${err.message}`);
        setLocating(false);
      },
      { enableHighAccuracy: true, timeout: 10000 }
    );
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setResult(null);

    try {
      const response = await fetch(`${API_URL}/predict/auto-weather`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          route_id: selectedRoute,
          latitude,
          longitude,
        }),
      });

      const data = await response.json();
      
      if (!response.ok) {
        throw new Error(data.error || 'Prediction failed');
      }
      
      setResult(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Prediction failed');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="card">
      <div className="card-header">
        <span className="card-icon">🌤️</span>
        <h2>Get Gritting Prediction</h2>
      </div>

      <div className="card-body">
        {/* Step 1: Route Selection */}
        <div className="input-section">
          <div className="section-header">
            <span className="section-step">Step 1</span>
            <h4>Select Route to Analyze</h4>
          </div>
          <p className="section-description">
            Routes have pre-defined characteristics (length, priority, surface type) that affect gritting requirements.
          </p>
          <div className="form-group">
            <label>Route</label>
            <select
              value={selectedRoute}
              onChange={(e) => {
                const routeId = e.target.value;
                setSelectedRoute(routeId);
                const route = routes.find(r => r.route_id === routeId);
                if (route) {
                  setLatitude(route.latitude);
                  setLongitude(route.longitude);
                }
              }}
              required
            >
              {routes.map((route) => (
                <option key={route.route_id} value={route.route_id}>
                  {route.route_name} — Priority {route.priority}, {route.length_km} km
                </option>
              ))}
            </select>
          </div>
        </div>

        {/* Step 2: Location */}
        <div className="input-section">
          <div className="section-header">
            <span className="section-step">Step 2</span>
            <h4>Provide Location for Weather Data</h4>
          </div>
          <p className="section-description">
            We'll fetch current weather conditions from <strong>Open-Meteo</strong> (free, no API key needed) 
            for this location to use in the prediction.
          </p>
          <div className="location-section">
            <div className="location-header">
              <label>📍 Coordinates</label>
              <button
                type="button"
                className="location-button"
                onClick={handleGetLocation}
                disabled={locating}
              >
                {locating ? '📡 Locating...' : '📍 Use My Location'}
              </button>
            </div>
            <div className="weather-grid">
              <div className="form-group">
                <label>Latitude</label>
                <input
                  type="number"
                  step="0.0001"
                  min="-90"
                  max="90"
                  value={latitude}
                  onChange={(e) => setLatitude(parseFloat(e.target.value) || 0)}
                  required
                />
              </div>
              <div className="form-group">
                <label>Longitude</label>
                <input
                  type="number"
                  step="0.0001"
                  min="-180"
                  max="180"
                  value={longitude}
                  onChange={(e) => setLongitude(parseFloat(e.target.value) || 0)}
                  required
                />
              </div>
            </div>
          </div>
        </div>

        {error && <div className="error">⚠️ {error}</div>}

        <form onSubmit={handleSubmit}>
          <button type="submit" disabled={loading || !selectedRoute}>
            {loading ? '⏳ Fetching Weather & Running ML Model...' : '🔮 Get Gritting Prediction'}
          </button>
        </form>

        {/* Results with Weather Data */}
        {result && result.success && (
          <>
            {/* Weather Data Card - Model Input */}
            <div className="weather-result-card" style={{ marginTop: '1.5rem' }}>
              <div className="weather-result-header">
                <h3>📥 Weather Data (ML Model Input)</h3>
                <span className="weather-source-tag">From Open-Meteo API</span>
              </div>
              <p className="model-input-explanation">
                These 8 weather parameters are fed into the ML model along with route characteristics to make the prediction:
              </p>
              <div className="weather-data-grid">
                <div className="weather-data-item primary">
                  <span className="weather-data-icon">🌡️</span>
                  <div className="weather-data-content">
                    <span className="weather-data-value">{result.weather.temperature_c.toFixed(1)}°C</span>
                    <span className="weather-data-label">Temperature</span>
                  </div>
                </div>
                <div className="weather-data-item">
                  <span className="weather-data-icon">🤒</span>
                  <div className="weather-data-content">
                    <span className="weather-data-value">{result.weather.feels_like_c.toFixed(1)}°C</span>
                    <span className="weather-data-label">Feels Like</span>
                  </div>
                </div>
                <div className="weather-data-item">
                  <span className="weather-data-icon">💧</span>
                  <div className="weather-data-content">
                    <span className="weather-data-value">{result.weather.humidity_pct}%</span>
                    <span className="weather-data-label">Humidity</span>
                  </div>
                </div>
                <div className="weather-data-item">
                  <span className="weather-data-icon">💨</span>
                  <div className="weather-data-content">
                    <span className="weather-data-value">{result.weather.wind_speed_kmh.toFixed(1)} km/h</span>
                    <span className="weather-data-label">Wind Speed</span>
                  </div>
                </div>
                <div className="weather-data-item">
                  <span className="weather-data-icon">{getPrecipitationIcon(result.weather.precipitation_type)}</span>
                  <div className="weather-data-content">
                    <span className="weather-data-value">{getPrecipitationLabel(result.weather.precipitation_type)}</span>
                    <span className="weather-data-label">Precipitation Type</span>
                  </div>
                </div>
                <div className="weather-data-item">
                  <span className="weather-data-icon">📊</span>
                  <div className="weather-data-content">
                    <span className="weather-data-value">{result.weather.precipitation_prob_pct}%</span>
                    <span className="weather-data-label">Precipitation Chance</span>
                  </div>
                </div>
                <div className="weather-data-item">
                  <span className="weather-data-icon">🛣️</span>
                  <div className="weather-data-content">
                    <span className="weather-data-value">{result.weather.road_surface_temp_c.toFixed(1)}°C</span>
                    <span className="weather-data-label">Road Surface Temp</span>
                  </div>
                </div>
                <div className="weather-data-item">
                  <span className="weather-data-icon">📉</span>
                  <div className="weather-data-content">
                    <span className="weather-data-value">{result.weather.forecast_min_temp_c.toFixed(1)}°C</span>
                    <span className="weather-data-label">Forecast Min</span>
                  </div>
                </div>
              </div>
            </div>

            {/* Prediction Result Card - Model Output */}
            <div className="result-card" style={{ marginTop: '1rem' }}>
              <div className={`result-header gritting-${result.prediction.gritting_decision}`}>
                <div className="decision-icon">
                  {result.prediction.gritting_decision === 'yes' ? '✅' : '⏸️'}
                </div>
                <h2>
                  {result.prediction.gritting_decision === 'yes' 
                    ? 'Gritting Required' 
                    : 'No Gritting Needed'}
                </h2>
                <p>
                  {result.prediction.route_name} • {(result.prediction.decision_confidence * 100).toFixed(0)}% confidence
                </p>
                <span className="model-output-tag">📤 ML Model Output</span>
              </div>
              
              <div className="result-body">
                <div className="result-grid">
                  <div className="result-item highlight">
                    <label>Salt Amount</label>
                    <div className="value">{result.prediction.salt_amount_kg} kg</div>
                  </div>
                  <div className="result-item highlight">
                    <label>Spread Rate</label>
                    <div className="value">{result.prediction.spread_rate_g_m2} g/m²</div>
                  </div>
                  <div className="result-item">
                    <label>Est. Duration</label>
                    <div className="value">{result.prediction.estimated_duration_min} min</div>
                  </div>
                  <div className="result-item">
                    <label>Risk Assessment</label>
                    <div className="risk-badges">
                      <span className={`risk-badge ${result.prediction.ice_risk}`}>
                        🧊 Ice: {result.prediction.ice_risk}
                      </span>
                      <span className={`risk-badge ${result.prediction.snow_risk}`}>
                        ❄️ Snow: {result.prediction.snow_risk}
                      </span>
                    </div>
                  </div>
                </div>
                
                <div className="recommendation-box">
                  <label>💡 Recommendation</label>
                  <p>{result.prediction.recommendation}</p>
                </div>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
