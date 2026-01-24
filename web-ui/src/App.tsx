import { useState, useEffect } from 'react';
import type { Route, WeatherData, PredictResponse } from './types';
import './index.css';

const API_URL = import.meta.env.VITE_API_URL || '/api';

function App() {
  const [routes, setRoutes] = useState<Route[]>([]);
  const [selectedRoute, setSelectedRoute] = useState('');
  const [weather, setWeather] = useState<WeatherData>({
    temperature_c: -3.5,
    feels_like_c: -7.2,
    humidity_pct: 88,
    wind_speed_kmh: 18,
    precipitation_type: 'snow',
    precipitation_prob_pct: 85,
    road_surface_temp_c: -4.2,
    forecast_min_temp_c: -5.0,
  });
  const [prediction, setPrediction] = useState<PredictResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

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
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch routes');
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    setPrediction(null);

    try {
      const response = await fetch(`${API_URL}/predict`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          route_id: selectedRoute,
          weather,
        }),
      });

      if (!response.ok) throw new Error('Prediction failed');
      const data = await response.json();
      setPrediction(data);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Prediction failed');
    } finally {
      setLoading(false);
    }
  };

  const handleWeatherChange = (field: keyof WeatherData, value: string) => {
    setWeather((prev) => {
      if (field === 'precipitation_type') {
        return { ...prev, [field]: value };
      }
      // Allow empty string for better UX when clearing fields
      if (value === '') {
        return { ...prev, [field]: 0 };
      }
      const numValue = parseFloat(value);
      if (isNaN(numValue)) return prev;
      return { ...prev, [field]: numValue };
    });
  };

  return (
    <div className="container">
      <h1>Road Gritting Predictor</h1>

      {error && <div className="error">{error}</div>}

      <form onSubmit={handleSubmit}>
        <div className="form-group">
          <label>Route</label>
          <select
            value={selectedRoute}
            onChange={(e) => setSelectedRoute(e.target.value)}
            required
          >
            {routes.map((route) => (
              <option key={route.route_id} value={route.route_id}>
                {route.route_name} (Priority {route.priority}, {route.length_km} km)
              </option>
            ))}
          </select>
        </div>

        <h2>Weather Conditions</h2>

        <div className="weather-grid">
          <div className="form-group">
            <label>Temperature (°C)</label>
            <input
              type="number"
              step="0.1"
              value={weather.temperature_c}
              onChange={(e) => handleWeatherChange('temperature_c', e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label>Feels Like (°C)</label>
            <input
              type="number"
              step="0.1"
              value={weather.feels_like_c}
              onChange={(e) => handleWeatherChange('feels_like_c', e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label>Humidity (%)</label>
            <input
              type="number"
              min="0"
              max="100"
              value={weather.humidity_pct}
              onChange={(e) => handleWeatherChange('humidity_pct', e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label>Wind Speed (km/h)</label>
            <input
              type="number"
              step="0.1"
              value={weather.wind_speed_kmh}
              onChange={(e) => handleWeatherChange('wind_speed_kmh', e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label>Precipitation Type</label>
            <select
              value={weather.precipitation_type}
              onChange={(e) => handleWeatherChange('precipitation_type', e.target.value)}
              required
            >
              <option value="none">None</option>
              <option value="rain">Rain</option>
              <option value="snow">Snow</option>
              <option value="sleet">Sleet</option>
            </select>
          </div>

          <div className="form-group">
            <label>Precipitation Probability (%)</label>
            <input
              type="number"
              min="0"
              max="100"
              value={weather.precipitation_prob_pct}
              onChange={(e) => handleWeatherChange('precipitation_prob_pct', e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label>Road Surface Temp (°C)</label>
            <input
              type="number"
              step="0.1"
              value={weather.road_surface_temp_c}
              onChange={(e) => handleWeatherChange('road_surface_temp_c', e.target.value)}
              required
            />
          </div>

          <div className="form-group">
            <label>Forecast Min Temp (°C)</label>
            <input
              type="number"
              step="0.1"
              value={weather.forecast_min_temp_c}
              onChange={(e) => handleWeatherChange('forecast_min_temp_c', e.target.value)}
              required
            />
          </div>
        </div>

        <button type="submit" disabled={loading || !selectedRoute}>
          {loading ? 'Predicting...' : 'Get Prediction'}
        </button>
      </form>

      {prediction && prediction.success && (
        <div className="prediction-result">
          <h2>Prediction Result</h2>
          <div className="result-grid">
            <div className="result-item">
              <strong>Route:</strong> {prediction.prediction.route_name}
            </div>
            <div className="result-item">
              <strong>Decision:</strong>{' '}
              <span className={`decision ${prediction.prediction.gritting_decision}`}>
                {prediction.prediction.gritting_decision.toUpperCase()}
              </span>
            </div>
            <div className="result-item">
              <strong>Confidence:</strong> {(prediction.prediction.decision_confidence * 100).toFixed(1)}%
            </div>
            <div className="result-item">
              <strong>Salt Amount:</strong> {prediction.prediction.salt_amount_kg} kg
            </div>
            <div className="result-item">
              <strong>Spread Rate:</strong> {prediction.prediction.spread_rate_g_m2} g/m²
            </div>
            <div className="result-item">
              <strong>Est. Duration:</strong> {prediction.prediction.estimated_duration_min} min
            </div>
            <div className="result-item">
              <strong>Ice Risk:</strong>{' '}
              <span className={`risk ${prediction.prediction.ice_risk}`}>
                {prediction.prediction.ice_risk}
              </span>
            </div>
            <div className="result-item">
              <strong>Snow Risk:</strong>{' '}
              <span className={`risk ${prediction.prediction.snow_risk}`}>
                {prediction.prediction.snow_risk}
              </span>
            </div>
            <div className="result-item full-width">
              <strong>Recommendation:</strong> {prediction.prediction.recommendation}
            </div>
          </div>
        </div>
      )}
    </div>
  );
}

export default App;
