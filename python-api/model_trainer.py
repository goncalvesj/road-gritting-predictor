"""
Model trainer for gritting prediction models.
Run this script to train and save models.

Usage:
    python model_trainer.py
"""
import pandas as pd
import numpy as np
from sklearn.ensemble import RandomForestClassifier, RandomForestRegressor
from sklearn.model_selection import train_test_split
from sklearn.preprocessing import LabelEncoder
import pickle
import sqlite3
import os
import json


class GrittingModelTrainer:
    """Trains gritting decision and salt amount prediction models."""
    
    def __init__(self):
        self.decision_model = None
        self.amount_model = None
        self.label_encoders = {}
        self.feature_cols = None
        self.route_lookup = {}
    
    def load_route_database(self, db_path):
        """Load route metadata from SQLite database."""
        conn = sqlite3.connect(db_path)
        try:
            routes_df = pd.read_sql_query("SELECT * FROM routes", conn)
        finally:
            conn.close()
        self.route_lookup = routes_df.set_index('route_id').to_dict('index')
    
    def train(self, db_path):
        """
        Train both decision and amount prediction models.
        
        Args:
            db_path: Path to SQLite database with training_data table.
        """
        print("Loading training data...")
        conn = sqlite3.connect(db_path)
        try:
            df = pd.read_sql_query("SELECT * FROM training_data", conn)
        finally:
            conn.close()
        
        print("Engineering features...")
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
        
        # Feature columns for prediction
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
        
        # Train amount regressor
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
    
    def save_models(self, path_prefix='models/gritting'):
        """Save trained models to disk."""
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


def main():
    """Train and save models."""
    trainer = GrittingModelTrainer()
    
    db_path = '../data/gritting_data.db'
    
    # Load routes
    trainer.load_route_database(db_path)
    
    # Train models
    trainer.train(db_path)
    
    # Save models
    trainer.save_models()
    
    print("\n" + "="*60)
    print("Training complete! Models saved to models/gritting_*.pkl")
    print("="*60)


if __name__ == "__main__":
    main()
