"""
Script to convert CSV files to SQLite database.
Run this script to create the gritting_data.db file.
"""
import sqlite3
import pandas as pd
import os

def convert_csv_to_sqlite():
    """Convert CSV files to SQLite database."""
    script_dir = os.path.dirname(os.path.abspath(__file__))
    db_path = os.path.join(script_dir, 'gritting_data.db')
    
    # Remove existing database if it exists
    if os.path.exists(db_path):
        os.remove(db_path)
    
    conn = sqlite3.connect(db_path)
    
    # Convert routes_database.csv
    routes_csv = os.path.join(script_dir, 'routes_database.csv')
    routes_df = pd.read_csv(routes_csv)
    routes_df.to_sql('routes', conn, index=False, if_exists='replace')
    print(f"Converted routes_database.csv: {len(routes_df)} rows")
    
    # Convert training dataset
    training_csv = os.path.join(script_dir, 'edinburgh_gritting_training_dataset.csv')
    training_df = pd.read_csv(training_csv)
    training_df.to_sql('training_data', conn, index=False, if_exists='replace')
    print(f"Converted edinburgh_gritting_training_dataset.csv: {len(training_df)} rows")
    
    conn.close()
    print(f"\nSQLite database created: {db_path}")

if __name__ == '__main__':
    convert_csv_to_sqlite()
