"""
Route service for loading route data from different sources.
Supports SQLite (default) and CSV (fallback) data sources.
"""
import sqlite3
import csv
from abc import ABC, abstractmethod


class RouteService(ABC):
    """Abstract base class for route data services."""
    
    @abstractmethod
    def get_routes(self):
        """Returns list of all routes as dicts."""
        pass
    
    @abstractmethod
    def get_route(self, route_id):
        """Returns route dict by ID or None if not found."""
        pass
    
    @abstractmethod
    def route_exists(self, route_id):
        """Returns True if route exists."""
        pass
    
    @property
    @abstractmethod
    def route_lookup(self):
        """Returns route lookup dict for compatibility with GrittingPredictionSystem."""
        pass


class SqliteRouteService(RouteService):
    """SQLite-based route service (default)."""
    
    def __init__(self, db_path):
        self._route_lookup = {}
        self._load_routes(db_path)
    
    def _load_routes(self, db_path):
        conn = sqlite3.connect(db_path)
        conn.row_factory = sqlite3.Row
        try:
            cursor = conn.execute("SELECT route_id, route_name, priority, road_type, route_length_km, latitude, longitude FROM routes")
            for row in cursor:
                self._route_lookup[row['route_id']] = {
                    'route_name': row['route_name'],
                    'priority': row['priority'],
                    'road_type': row['road_type'],
                    'route_length_km': row['route_length_km'],
                    'latitude': row['latitude'],
                    'longitude': row['longitude']
                }
        finally:
            conn.close()
    
    def get_routes(self):
        return [{'route_id': rid, **info} for rid, info in self._route_lookup.items()]
    
    def get_route(self, route_id):
        info = self._route_lookup.get(route_id)
        return {'route_id': route_id, **info} if info else None
    
    def route_exists(self, route_id):
        return route_id in self._route_lookup
    
    @property
    def route_lookup(self):
        return self._route_lookup


class CsvRouteService(RouteService):
    """CSV-based route service (fallback for backwards compatibility)."""
    
    def __init__(self, csv_path):
        self._route_lookup = {}
        self._load_routes(csv_path)
    
    def _load_routes(self, csv_path):
        with open(csv_path, 'r') as f:
            reader = csv.DictReader(f)
            for row in reader:
                self._route_lookup[row['route_id']] = {
                    'route_name': row['route_name'],
                    'priority': int(row['priority']),
                    'road_type': row['road_type'],
                    'route_length_km': float(row['route_length_km']),
                    'latitude': float(row['latitude']),
                    'longitude': float(row['longitude'])
                }
    
    def get_routes(self):
        return [{'route_id': rid, **info} for rid, info in self._route_lookup.items()]
    
    def get_route(self, route_id):
        info = self._route_lookup.get(route_id)
        return {'route_id': route_id, **info} if info else None
    
    def route_exists(self, route_id):
        return route_id in self._route_lookup
    
    @property
    def route_lookup(self):
        return self._route_lookup


def create_route_service(sqlite_path='../data/gritting_data.db', csv_path='../data/routes_database.csv'):
    """
    Factory function to create appropriate route service.
    Prefers SQLite, falls back to CSV.
    """
    import os
    
    if os.path.exists(sqlite_path):
        return SqliteRouteService(sqlite_path)
    elif os.path.exists(csv_path):
        return CsvRouteService(csv_path)
    else:
        raise FileNotFoundError("No route data source found (tried SQLite and CSV)")
