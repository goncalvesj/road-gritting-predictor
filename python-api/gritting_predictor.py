"""
Gritting prediction service - inference only.
Loads pre-trained models and makes predictions.
For model training, use model_trainer.py.
"""
import pickle


class GrittingPredictor:
    """
    Inference-only gritting prediction service.
    Loads pre-trained models and makes predictions without sklearn training dependencies.
    """
    
    def __init__(self, route_lookup=None):
        self.decision_model = None
        self.amount_model = None
        self.label_encoders = {}
        self.feature_cols = None
        self.route_lookup = route_lookup or {}
    
    def load_models(self, path_prefix='models/gritting'):
        """Load trained models from disk."""
        with open(f'{path_prefix}_decision_model.pkl', 'rb') as f:
            self.decision_model = pickle.load(f)
        
        with open(f'{path_prefix}_amount_model.pkl', 'rb') as f:
            self.amount_model = pickle.load(f)
        
        with open(f'{path_prefix}_encoders.pkl', 'rb') as f:
            self.label_encoders = pickle.load(f)
        
        with open(f'{path_prefix}_feature_cols.pkl', 'rb') as f:
            self.feature_cols = pickle.load(f)
        
        # Load route_lookup from models if not already set
        if not self.route_lookup:
            with open(f'{path_prefix}_route_lookup.pkl', 'rb') as f:
                self.route_lookup = pickle.load(f)
    
    def predict(self, route_id, weather_data):
        """
        Make prediction for a specific route and weather conditions.
        
        Args:
            route_id: str - Route identifier
            weather_data: dict - Current/forecast weather data
            
        Returns:
            dict with prediction results
        """
        if self.decision_model is None or self.amount_model is None:
            raise RuntimeError("Models not loaded. Call load_models() first.")
        
        # Sanitize precipitation type
        weather_data = weather_data.copy()
        weather_data['precipitation_type'] = self._sanitize_precipitation_type(
            weather_data['precipitation_type']
        )
        
        # Prepare features
        features = self._prepare_features(route_id, weather_data)
        
        # Encode categorical features
        features_encoded = features.copy()
        features_encoded['precipitation_type_encoded'] = self.label_encoders['precipitation_type'].transform(
            [features['precipitation_type']]
        )[0]
        
        risk_mapping = {'low': 0, 'medium': 1, 'high': 2}
        features_encoded['ice_risk_encoded'] = risk_mapping[features['ice_risk']]
        features_encoded['snow_risk_encoded'] = risk_mapping[features['snow_risk']]
        
        # Create feature vector (pandas-free for inference)
        import pandas as pd
        X_pred = pd.DataFrame([{col: features_encoded[col] for col in self.feature_cols}])
        
        # Predict decision
        decision_proba = self.decision_model.predict_proba(X_pred)[0]
        decision = decision_proba[1] > 0.5
        decision_confidence = decision_proba[1] if decision else decision_proba[0]
        
        # Predict amount if gritting is recommended
        salt_amount = 0
        spread_rate = 0
        estimated_duration = 0
        
        if decision:
            salt_amount = int(self.amount_model.predict(X_pred)[0])
            route_length = features['route_length_km']
            spread_rate = int(salt_amount / (route_length * 1000) * 1000)
            estimated_duration = int(route_length / 3 * 10) + 5
        
        recommendation = self._generate_recommendation(features, decision)
        
        return {
            'route_id': route_id,
            'route_name': features['route_name'],
            'gritting_decision': 'yes' if decision else 'no',
            'decision_confidence': round(decision_confidence, 3),
            'salt_amount_kg': salt_amount,
            'spread_rate_g_m2': spread_rate,
            'estimated_duration_min': estimated_duration,
            'ice_risk': features['ice_risk'],
            'snow_risk': features['snow_risk'],
            'recommendation': recommendation
        }
    
    def _prepare_features(self, route_id, weather_data):
        """Prepare features from route ID and weather data."""
        if route_id not in self.route_lookup:
            raise ValueError(f"Route {route_id} not found in database")
        
        route = self.route_lookup[route_id]
        
        ice_risk = self._calculate_ice_risk(
            weather_data['road_surface_temp_c'],
            weather_data['temperature_c'],
            weather_data['precipitation_prob_pct']
        )
        
        snow_risk = self._calculate_snow_risk(
            weather_data['temperature_c'],
            weather_data['precipitation_type'],
            weather_data['precipitation_prob_pct']
        )
        
        return {
            'route_id': route_id,
            'route_name': route['route_name'],
            'priority': route['priority'],
            'road_type': route['road_type'],
            'route_length_km': route['route_length_km'],
            'temperature_c': weather_data['temperature_c'],
            'feels_like_c': weather_data['feels_like_c'],
            'humidity_pct': weather_data['humidity_pct'],
            'wind_speed_kmh': weather_data['wind_speed_kmh'],
            'precipitation_type': weather_data['precipitation_type'],
            'precipitation_prob_pct': weather_data['precipitation_prob_pct'],
            'road_surface_temp_c': weather_data['road_surface_temp_c'],
            'forecast_min_temp_c': weather_data['forecast_min_temp_c'],
            'ice_risk': ice_risk,
            'snow_risk': snow_risk,
            'temp_below_zero': 1 if weather_data['temperature_c'] < 0 else 0,
            'surface_temp_below_zero': 1 if weather_data['road_surface_temp_c'] < 0 else 0,
            'high_precip_prob': 1 if weather_data['precipitation_prob_pct'] > 60 else 0,
        }
    
    def _calculate_ice_risk(self, road_temp, air_temp, precip_prob):
        """Calculate ice risk level."""
        if road_temp <= -2 and precip_prob > 60:
            return 'high'
        elif road_temp <= 0 and precip_prob > 40:
            return 'high'
        elif road_temp <= 1 and precip_prob > 50:
            return 'medium'
        elif air_temp <= 0:
            return 'medium'
        return 'low'
    
    def _calculate_snow_risk(self, temp, precip_type, precip_prob):
        """Calculate snow risk level."""
        if precip_type == 'snow' and precip_prob > 70:
            return 'high'
        elif precip_type == 'sleet' and precip_prob > 60:
            return 'medium'
        elif precip_type == 'snow' and precip_prob > 40:
            return 'medium'
        return 'low'
    
    def _sanitize_precipitation_type(self, precip_type):
        """Sanitize precipitation type to handle unknown values."""
        known_types = ['none', 'rain', 'sleet', 'snow']
        
        if precip_type is None or not isinstance(precip_type, str):
            return 'none'
        
        if precip_type in known_types:
            return precip_type
        
        precip_lower = precip_type.lower()
        
        if any(term in precip_lower for term in ['snow', 'blizzard', 'flurr']):
            return 'snow'
        if any(term in precip_lower for term in ['sleet', 'ice', 'hail', 'freez']):
            return 'sleet'
        if any(term in precip_lower for term in ['rain', 'drizzle', 'shower', 'storm']):
            return 'rain'
        
        return 'none'
    
    def _generate_recommendation(self, features, decision):
        """Generate human-readable recommendation."""
        if not decision:
            return "No gritting required - conditions safe"
        
        reasons = []
        if features['ice_risk'] == 'high':
            reasons.append("high ice risk")
        if features['snow_risk'] == 'high':
            reasons.append("high snow risk")
        if features['road_surface_temp_c'] < -3:
            reasons.append("very low road temperature")
        if features['precipitation_prob_pct'] > 80:
            reasons.append("high precipitation probability")
        
        priority_text = "High priority" if features['priority'] == 1 else "Medium priority"
        
        if reasons:
            return f"{priority_text} - {', '.join(reasons)}"
        return f"{priority_text} - preventive gritting recommended"
