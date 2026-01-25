import { useState, useEffect, useMemo } from 'react';
import type { Route, WeatherData, PredictResponse } from './types';
import './index.css';

const API_URL = import.meta.env.VITE_API_URL || '/api';

// Wind chill calculation (simplified formula)
function calculateFeelsLike(tempC: number, windKmh: number): number {
  if (tempC > 10 || windKmh < 4.8) return tempC;
  const feelsLike = 13.12 + 0.6215 * tempC - 11.37 * Math.pow(windKmh, 0.16) + 0.3965 * tempC * Math.pow(windKmh, 0.16);
  return Math.round(feelsLike * 10) / 10;
}

// Road surface temp estimate (typically 1-2°C below air temp at night)
function calculateRoadSurfaceTemp(tempC: number): number {
  return Math.round((tempC - 1.5) * 10) / 10;
}

function App() {
  const [routes, setRoutes] = useState<Route[]>([]);
  const [selectedRoute, setSelectedRoute] = useState('');
  const [showAdvanced, setShowAdvanced] = useState(false);
  
  // Core weather inputs (simplified)
  const [temperature, setTemperature] = useState(-3.5);
  const [humidity, setHumidity] = useState(88);
  const [windSpeed, setWindSpeed] = useState(18);
  const [precipType, setPrecipType] = useState('snow');
  const [precipProb, setPrecipProb] = useState(85);
  const [forecastMin, setForecastMin] = useState(-5.0);
  
  // Advanced overrides
  const [manualFeelsLike, setManualFeelsLike] = useState<number | null>(null);
  const [manualRoadTemp, setManualRoadTemp] = useState<number | null>(null);

  const [prediction, setPrediction] = useState<PredictResponse | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  // Auto-calculated values
  const feelsLike = useMemo(() => 
    manualFeelsLike ?? calculateFeelsLike(temperature, windSpeed),
    [temperature, windSpeed, manualFeelsLike]
  );
  
  const roadSurfaceTemp = useMemo(() => 
    manualRoadTemp ?? calculateRoadSurfaceTemp(temperature),
    [temperature, manualRoadTemp]
  );

  // Construct full weather object for API
  const weather: WeatherData = useMemo(() => ({
    temperature_c: temperature,
    feels_like_c: feelsLike,
    humidity_pct: humidity,
    wind_speed_kmh: windSpeed,
    precipitation_type: precipType,
    precipitation_prob_pct: precipProb,
    road_surface_temp_c: roadSurfaceTemp,
    forecast_min_temp_c: forecastMin,
  }), [temperature, feelsLike, humidity, windSpeed, precipType, precipProb, roadSurfaceTemp, forecastMin]);

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

  return (
    <div className="app">
      <header className="header">
        <h1>🚛 Road Gritting Predictor</h1>
        <p>ML-powered winter road maintenance decisions</p>
      </header>

      <div className="container">
        {error && <div className="error">⚠️ {error}</div>}

        <form onSubmit={handleSubmit}>
          {/* Route Selection Card */}
          <div className="card">
            <div className="card-header">
              <span className="card-icon">🛣️</span>
              <h2>Select Route</h2>
            </div>
            <div className="card-body">
              <div className="form-group">
                <label>Route</label>
                <select
                  value={selectedRoute}
                  onChange={(e) => setSelectedRoute(e.target.value)}
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
          </div>

          {/* Weather Conditions Card */}
          <div className="card">
            <div className="card-header">
              <span className="card-icon">🌡️</span>
              <h2>Weather Conditions</h2>
            </div>
            <div className="card-body">
              <div className="weather-grid">
                <div className="form-group">
                  <label>Temperature (°C)</label>
                  <input
                    type="number"
                    step="0.1"
                    value={temperature}
                    onChange={(e) => setTemperature(parseFloat(e.target.value) || 0)}
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Forecast Min (°C)</label>
                  <input
                    type="number"
                    step="0.1"
                    value={forecastMin}
                    onChange={(e) => setForecastMin(parseFloat(e.target.value) || 0)}
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Humidity (%)</label>
                  <input
                    type="number"
                    min="0"
                    max="100"
                    value={humidity}
                    onChange={(e) => setHumidity(parseInt(e.target.value) || 0)}
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Wind Speed (km/h)</label>
                  <input
                    type="number"
                    step="0.1"
                    min="0"
                    value={windSpeed}
                    onChange={(e) => setWindSpeed(parseFloat(e.target.value) || 0)}
                    required
                  />
                </div>

                <div className="form-group">
                  <label>Precipitation Type</label>
                  <select
                    value={precipType}
                    onChange={(e) => setPrecipType(e.target.value)}
                    required
                  >
                    <option value="none">☀️ None</option>
                    <option value="rain">🌧️ Rain</option>
                    <option value="snow">❄️ Snow</option>
                    <option value="sleet">🌨️ Sleet</option>
                  </select>
                </div>

                <div className="form-group">
                  <label>Precipitation Chance (%)</label>
                  <input
                    type="number"
                    min="0"
                    max="100"
                    value={precipProb}
                    onChange={(e) => setPrecipProb(parseInt(e.target.value) || 0)}
                    required
                  />
                </div>
              </div>

              {/* Advanced Options Toggle */}
              <div 
                className="advanced-toggle"
                onClick={() => setShowAdvanced(!showAdvanced)}
              >
                <span>{showAdvanced ? '▼' : '▶'}</span>
                <span>Advanced Options (auto-calculated values)</span>
              </div>

              {showAdvanced && (
                <div className="advanced-fields">
                  <div className="weather-grid">
                    <div className="form-group">
                      <label>Feels Like (°C)</label>
                      <input
                        type="number"
                        step="0.1"
                        value={manualFeelsLike ?? feelsLike}
                        onChange={(e) => setManualFeelsLike(e.target.value === '' ? null : parseFloat(e.target.value))}
                      />
                      <div className="auto-calc-note">
                        Auto: {calculateFeelsLike(temperature, windSpeed).toFixed(1)}°C
                      </div>
                    </div>

                    <div className="form-group">
                      <label>Road Surface Temp (°C)</label>
                      <input
                        type="number"
                        step="0.1"
                        value={manualRoadTemp ?? roadSurfaceTemp}
                        onChange={(e) => setManualRoadTemp(e.target.value === '' ? null : parseFloat(e.target.value))}
                      />
                      <div className="auto-calc-note">
                        Auto: {calculateRoadSurfaceTemp(temperature).toFixed(1)}°C
                      </div>
                    </div>
                  </div>
                </div>
              )}
            </div>
          </div>

          <button type="submit" disabled={loading || !selectedRoute}>
            {loading ? '⏳ Analyzing...' : '🔮 Get Prediction'}
          </button>
        </form>

        {/* Results */}
        {prediction && prediction.success && (
          <div className="result-card" style={{ marginTop: '1.5rem' }}>
            <div className={`result-header gritting-${prediction.prediction.gritting_decision}`}>
              <div className="decision-icon">
                {prediction.prediction.gritting_decision === 'yes' ? '✅' : '⏸️'}
              </div>
              <h2>
                {prediction.prediction.gritting_decision === 'yes' 
                  ? 'Gritting Required' 
                  : 'No Gritting Needed'}
              </h2>
              <p>{prediction.prediction.route_name} • {(prediction.prediction.decision_confidence * 100).toFixed(0)}% confidence</p>
            </div>
            
            <div className="result-body">
              <div className="result-grid">
                <div className="result-item highlight">
                  <label>Salt Amount</label>
                  <div className="value">{prediction.prediction.salt_amount_kg} kg</div>
                </div>
                <div className="result-item highlight">
                  <label>Spread Rate</label>
                  <div className="value">{prediction.prediction.spread_rate_g_m2} g/m²</div>
                </div>
                <div className="result-item">
                  <label>Est. Duration</label>
                  <div className="value">{prediction.prediction.estimated_duration_min} min</div>
                </div>
                <div className="result-item">
                  <label>Risk Assessment</label>
                  <div className="risk-badges">
                    <span className={`risk-badge ${prediction.prediction.ice_risk}`}>
                      🧊 Ice: {prediction.prediction.ice_risk}
                    </span>
                    <span className={`risk-badge ${prediction.prediction.snow_risk}`}>
                      ❄️ Snow: {prediction.prediction.snow_risk}
                    </span>
                  </div>
                </div>
              </div>
              
              <div className="recommendation-box">
                <label>💡 Recommendation</label>
                <p>{prediction.prediction.recommendation}</p>
              </div>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

export default App;
