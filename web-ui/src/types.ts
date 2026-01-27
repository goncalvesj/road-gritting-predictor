export interface Route {
  route_id: string;
  route_name: string;
  priority: number;
  length_km: number;
}

export interface RoutesResponse {
  routes: Route[];
}

export interface WeatherData {
  temperature_c: number;
  feels_like_c: number;
  humidity_pct: number;
  wind_speed_kmh: number;
  precipitation_type: string;
  precipitation_prob_pct: number;
  road_surface_temp_c: number;
  forecast_min_temp_c: number;
}

export interface PredictRequest {
  route_id: string;
  weather: WeatherData;
}

export interface Prediction {
  route_name: string;
  gritting_decision: string;
  decision_confidence: number;
  salt_amount_kg: number;
  spread_rate_g_m2: number;
  estimated_duration_min: number;
  ice_risk: string;
  snow_risk: string;
  recommendation: string;
}

export interface PredictResponse {
  success: boolean;
  prediction: Prediction;
  error?: string;
}

export interface HistoricalDecision {
  id: string;
  timestamp: string;
  route_id: string;
  route_name: string;
  gritting_decision: string;
  decision_confidence: number;
  salt_amount_kg: number;
  ice_risk: string;
  snow_risk: string;
  temperature_c: number;
  precipitation_type: string;
}

export interface AutoWeatherRequest {
  route_id: string;
  latitude: number;
  longitude: number;
}

export interface AutoWeatherResponse {
  success: boolean;
  prediction: Prediction;
  weather_source: string;
  error?: string;
}

export type Page = 'predictor' | 'routes' | 'history' | 'auto-weather';
