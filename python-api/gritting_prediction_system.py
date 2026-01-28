import pandas as pd
import numpy as np
from sklearn.ensemble import RandomForestClassifier, RandomForestRegressor
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import LabelEncoder
import pickle
import json
import sqlite3
from datetime import datetime

class GrittingPredictionSystem:
    """
    Multi-output prediction system for road gritting decisions.
    Predicts both gritting decision (yes/no) and salt amount.
    """
    
    def __init__(self):
        self.decision_model = None
        self.amount_model = None
        self.routes_db = None
        self.label_encoders = {}
        self.feature_cols = None
        
    def load_route_database(self, db_path):
        """
        Load route metadata database from SQLite.
        
        Args:
            db_path: Path to the SQLite database file containing the routes table.
        """
        conn = sqlite3.connect(db_path)
        try:
            self.routes_db = pd.read_sql_query("SELECT * FROM routes", conn)
        finally:
            conn.close()
        # Create route lookup dictionary
        self.route_lookup = self.routes_db.set_index('route_id').to_dict('index')
        
    def prepare_features(self, route_id, weather_data):
        """
        Prepare features from route ID and weather data
        
        Args:
            route_id: str - Route identifier (e.g., 'R001')
            weather_data: dict - Weather conditions from API
                {
                    'temperature_c': -2.5,
                    'feels_like_c': -6.0,
                    'humidity_pct': 85,
                    'wind_speed_kmh': 18,
                    'precipitation_type': 'snow',
                    'precipitation_prob_pct': 80,
                    'road_surface_temp_c': -3.0,
                    'forecast_min_temp_c': -4.5
                }
        
        Returns:
            dict: Complete feature set for prediction
        """
        # Get route metadata
        if route_id not in self.route_lookup:
            raise ValueError(f"Route {route_id} not found in database")
        
        route = self.route_lookup[route_id]
        
        # Calculate risk levels
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
        
        # Combine all features
        features = {
            # Route features
            'route_id': route_id,
            'route_name': route['route_name'],
            'priority': route['priority'],
            'road_type': route['road_type'],
            'route_length_km': route['route_length_km'],
            
            # Weather features
            'temperature_c': weather_data['temperature_c'],
            'feels_like_c': weather_data['feels_like_c'],
            'humidity_pct': weather_data['humidity_pct'],
            'wind_speed_kmh': weather_data['wind_speed_kmh'],
            'precipitation_type': weather_data['precipitation_type'],
            'precipitation_prob_pct': weather_data['precipitation_prob_pct'],
            'road_surface_temp_c': weather_data['road_surface_temp_c'],
            'forecast_min_temp_c': weather_data['forecast_min_temp_c'],
            
            # Calculated risk features
            'ice_risk': ice_risk,
            'snow_risk': snow_risk,
            
            # Engineered features
            'temp_below_zero': 1 if weather_data['temperature_c'] < 0 else 0,
            'surface_temp_below_zero': 1 if weather_data['road_surface_temp_c'] < 0 else 0,
            'high_precip_prob': 1 if weather_data['precipitation_prob_pct'] > 60 else 0,
        }
        
        return features
    
    def _calculate_ice_risk(self, road_temp, air_temp, precip_prob):
        """Calculate ice risk level"""
        if road_temp <= -2 and precip_prob > 60:
            return 'high'
        elif road_temp <= 0 and precip_prob > 40:
            return 'high'
        elif road_temp <= 1 and precip_prob > 50:
            return 'medium'
        elif air_temp <= 0:
            return 'medium'
        else:
            return 'low'
    
    def _calculate_snow_risk(self, temp, precip_type, precip_prob):
        """Calculate snow risk level"""
        if precip_type == 'snow' and precip_prob > 70:
            return 'high'
        elif precip_type == 'sleet' and precip_prob > 60:
            return 'medium'
        elif precip_type == 'snow' and precip_prob > 40:
            return 'medium'
        else:
            return 'low'
    
    def train(self, db_path):
        """
        Train both decision and amount prediction models.
        
        Args:
            db_path: Path to the SQLite database file containing the training_data table.
        """
        print("Loading training data...")
        conn = sqlite3.connect(db_path)
        try:
            df = pd.read_sql_query("SELECT * FROM training_data", conn)
        finally:
            conn.close()
        
        print("Engineering features...")
        # Feature engineering
        df['temp_below_zero'] = (df['temperature_c'] < 0).astype(int)
        df['surface_temp_below_zero'] = (df['road_surface_temp_c'] < 0).astype(int)
        df['high_precip_prob'] = (df['precipitation_prob_pct'] > 60).astype(int)
        
        # Encode categorical variables
        self.label_encoders['precipitation_type'] = LabelEncoder()
        df['precipitation_type_encoded'] = self.label_encoders['precipitation_type'].fit_transform(
            df['precipitation_type']
        )
        
        risk_mapping = {'low': 0, 'medium': 1, 'high': 2}
        df['ice_risk_encoded'] = df['ice_risk'].map(risk_mapping)
        df['snow_risk_encoded'] = df['snow_risk'].map(risk_mapping)
        
        # Select features for modeling
        self.feature_cols = [
            'priority',
            'temperature_c',
            'feels_like_c',
            'humidity_pct',
            'wind_speed_kmh',
            'precipitation_type_encoded',
            'precipitation_prob_pct',
            'road_surface_temp_c',
            'forecast_min_temp_c',
            'ice_risk_encoded',
            'snow_risk_encoded',
            'route_length_km',
            'temp_below_zero',
            'surface_temp_below_zero',
            'high_precip_prob'
        ]
        
        X = df[self.feature_cols]
        
        # Train decision classifier
        print("\nTraining decision classifier...")
        y_decision = (df['gritting_decision'] == 'gritted').astype(int)
        
        X_train, X_test, y_train, y_test = train_test_split(
            X, y_decision, test_size=0.2, random_state=42, stratify=y_decision
        )
        
        self.decision_model = RandomForestClassifier(
            n_estimators=100,
            max_depth=10,
            min_samples_split=5,
            random_state=42
        )
        self.decision_model.fit(X_train, y_train)
        
        decision_score = self.decision_model.score(X_test, y_test)
        print(f"Decision model accuracy: {decision_score:.3f}")
        
        # Train amount regressor (only on gritted instances)
        print("\nTraining salt amount regressor...")
        gritted_data = df[df['gritting_decision'] == 'gritted'].copy()
        X_gritted = gritted_data[self.feature_cols]
        y_amount = gritted_data['salt_amount_kg']
        
        X_train_amt, X_test_amt, y_train_amt, y_test_amt = train_test_split(
            X_gritted, y_amount, test_size=0.2, random_state=42
        )
        
        self.amount_model = RandomForestRegressor(
            n_estimators=100,
            max_depth=10,
            min_samples_split=5,
            random_state=42
        )
        self.amount_model.fit(X_train_amt, y_train_amt)
        
        amount_score = self.amount_model.score(X_test_amt, y_test_amt)
        print(f"Amount model R² score: {amount_score:.3f}")
        
        # Feature importance
        print("\nTop 10 features for decision:")
        importance_df = pd.DataFrame({
            'feature': self.feature_cols,
            'importance': self.decision_model.feature_importances_
        }).sort_values('importance', ascending=False)
        print(importance_df.head(10))
        
        print("\nModels trained successfully!")
        
    def _sanitize_precipitation_type(self, precip_type):
        """
        Sanitize precipitation type to handle unknown values.
        Maps unknown types to the closest known category.
        
        Known types from training data: ['none', 'rain', 'sleet', 'snow']
        """
        known_types = ['none', 'rain', 'sleet', 'snow']
        
        # Handle None or non-string values
        if precip_type is None or not isinstance(precip_type, str):
            return 'none'
        
        if precip_type in known_types:
            return precip_type
        
        # Map common unknown types to closest known type
        precip_lower = precip_type.lower()
        
        # Snow-like conditions
        if any(term in precip_lower for term in ['snow', 'blizzard', 'flurr']):
            return 'snow'
        
        # Sleet-like conditions  
        if any(term in precip_lower for term in ['sleet', 'ice', 'hail', 'freez']):
            return 'sleet'
        
        # Rain-like conditions
        if any(term in precip_lower for term in ['rain', 'drizzle', 'shower', 'storm']):
            return 'rain'
        
        # Default to 'none' for unknown conditions
        return 'none'
    
    def predict(self, route_id, weather_data):
        """
        Make prediction for a specific route and weather conditions
        
        Args:
            route_id: str - Route identifier
            weather_data: dict - Current/forecast weather data
            
        Returns:
            dict: {
                'route_id': 'R001',
                'route_name': 'Queensferry Road',
                'gritting_decision': 'yes',
                'decision_confidence': 0.95,
                'salt_amount_kg': 850,
                'spread_rate_g_m2': 25,
                'estimated_duration_min': 45,
                'recommendation': 'High priority - ice risk detected'
            }
        """
        if self.decision_model is None or self.amount_model is None:
            raise RuntimeError("Models not trained. Call train() first.")
        
        # Sanitize precipitation type to handle unknown values
        weather_data = weather_data.copy()  # Don't modify original
        weather_data['precipitation_type'] = self._sanitize_precipitation_type(
            weather_data['precipitation_type']
        )
        
        # Prepare features
        features = self.prepare_features(route_id, weather_data)
        
        # Encode categorical features
        features_encoded = features.copy()
        features_encoded['precipitation_type_encoded'] = self.label_encoders['precipitation_type'].transform(
            [features['precipitation_type']]
        )[0]
        
        risk_mapping = {'low': 0, 'medium': 1, 'high': 2}
        features_encoded['ice_risk_encoded'] = risk_mapping[features['ice_risk']]
        features_encoded['snow_risk_encoded'] = risk_mapping[features['snow_risk']]
        
        # Create feature vector
        X_pred = pd.DataFrame([{
            col: features_encoded[col] for col in self.feature_cols
        }])
        
        # Predict decision
        decision_proba = self.decision_model.predict_proba(X_pred)[0]
        decision = decision_proba[1] > 0.5  # Threshold at 50%
        decision_confidence = decision_proba[1] if decision else decision_proba[0]
        
        # Predict amount if gritting is recommended
        salt_amount = 0
        spread_rate = 0
        estimated_duration = 0
        
        if decision:
            salt_amount = int(self.amount_model.predict(X_pred)[0])
            route_length = features['route_length_km']
            spread_rate = int(salt_amount / (route_length * 1000) * 1000)
            # Estimate duration: ~3km per 10 minutes for gritter truck
            estimated_duration = int(route_length / 3 * 10) + 5  # +5 min setup
        
        # Generate recommendation
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
    
    def _generate_recommendation(self, features, decision):
        """Generate human-readable recommendation"""
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
        else:
            return f"{priority_text} - preventive gritting recommended"
    
    def save_models(self, path_prefix='models/gritting'):
        """Save trained models to disk"""
        import os
        os.makedirs(os.path.dirname(path_prefix), exist_ok=True)
        
        with open(f'{path_prefix}_decision_model.pkl', 'wb') as f:
            pickle.dump(self.decision_model, f)
        
        with open(f'{path_prefix}_amount_model.pkl', 'wb') as f:
            pickle.dump(self.amount_model, f)
        
        with open(f'{path_prefix}_encoders.pkl', 'wb') as f:
            pickle.dump(self.label_encoders, f)
        
        with open(f'{path_prefix}_feature_cols.pkl', 'wb') as f:
            pickle.dump(self.feature_cols, f)
        
        with open(f'{path_prefix}_route_lookup.pkl', 'wb') as f:
            pickle.dump(self.route_lookup, f)
        
        print(f"Models saved to {path_prefix}_*.pkl")
    
    def load_models(self, path_prefix='models/gritting'):
        """Load trained models from disk"""
        with open(f'{path_prefix}_decision_model.pkl', 'rb') as f:
            self.decision_model = pickle.load(f)
        
        with open(f'{path_prefix}_amount_model.pkl', 'rb') as f:
            self.amount_model = pickle.load(f)
        
        with open(f'{path_prefix}_encoders.pkl', 'rb') as f:
            self.label_encoders = pickle.load(f)
        
        with open(f'{path_prefix}_feature_cols.pkl', 'rb') as f:
            self.feature_cols = pickle.load(f)
        
        with open(f'{path_prefix}_route_lookup.pkl', 'rb') as f:
            self.route_lookup = pickle.load(f)
        
        print("Models loaded successfully")


# ============================================
# USAGE EXAMPLES
# ============================================

if __name__ == "__main__":
    
    # Initialize system
    system = GrittingPredictionSystem()
    
    # Load route database from SQLite
    system.load_route_database('../data/gritting_data.db')
    
    # Train models using SQLite database
    system.train('../data/gritting_data.db')
    
    # Save models
    system.save_models()
    
    print("\n" + "="*60)
    print("PREDICTION EXAMPLES")
    print("="*60)
    
    # Example 1: High risk scenario
    print("\n--- Example 1: Severe winter conditions ---")
    weather1 = {
        'temperature_c': -4.5,
        'feels_like_c': -9.0,
        'humidity_pct': 90,
        'wind_speed_kmh': 25,
        'precipitation_type': 'snow',
        'precipitation_prob_pct': 90,
        'road_surface_temp_c': -5.5,
        'forecast_min_temp_c': -6.5
    }
    
    result1 = system.predict('R001', weather1)
    print(json.dumps(result1, indent=2))
    
    # Example 2: Low risk scenario
    print("\n--- Example 2: Mild conditions ---")
    weather2 = {
        'temperature_c': 3.5,
        'feels_like_c': 1.0,
        'humidity_pct': 65,
        'wind_speed_kmh': 10,
        'precipitation_type': 'none',
        'precipitation_prob_pct': 10,
        'road_surface_temp_c': 4.5,
        'forecast_min_temp_c': 2.0
    }
    
    result2 = system.predict('R001', weather2)
    print(json.dumps(result2, indent=2))
    
    # Example 3: Borderline case
    print("\n--- Example 3: Borderline conditions ---")
    weather3 = {
        'temperature_c': -0.5,
        'feels_like_c': -3.5,
        'humidity_pct': 80,
        'wind_speed_kmh': 15,
        'precipitation_type': 'rain',
        'precipitation_prob_pct': 65,
        'road_surface_temp_c': 0.2,
        'forecast_min_temp_c': -1.5
    }
    
    result3 = system.predict('R002', weather3)
    print(json.dumps(result3, indent=2))