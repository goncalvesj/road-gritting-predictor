import { useState, useEffect } from 'react';
import type { Route } from './types';

const API_URL = import.meta.env.VITE_API_URL || '/api';

interface RoutesPageProps {
  onBack: () => void;
}

export function RoutesPage({ onBack }: RoutesPageProps) {
  const [routes, setRoutes] = useState<Route[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    fetchRoutes();
  }, []);

  const fetchRoutes = async () => {
    try {
      setLoading(true);
      const response = await fetch(`${API_URL}/routes`);
      if (!response.ok) throw new Error('Failed to fetch routes');
      const data = await response.json();
      setRoutes(data.routes);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to fetch routes');
    } finally {
      setLoading(false);
    }
  };

  const getPriorityLabel = (priority: number) => {
    switch (priority) {
      case 1: return { label: 'Critical', className: 'priority-critical', description: 'Emergency services, hospitals' };
      case 2: return { label: 'High', className: 'priority-high', description: 'Major arterial roads' };
      case 3: return { label: 'Medium', className: 'priority-medium', description: 'Secondary roads' };
      default: return { label: 'Low', className: 'priority-low', description: 'Residential areas' };
    }
  };

  return (
    <div className="card">
      <div className="page-header-bar">
        <button className="back-button" onClick={onBack}>← Back</button>
        <h2>🛣️ Route Database</h2>
      </div>

      <div className="card-body">
        {/* Page Description */}
        <div className="page-description">
          <p>
            <strong>What is this?</strong> Routes are predefined road segments with known characteristics. 
            The ML model uses route data (length, priority, traffic patterns) along with weather conditions 
            to determine gritting requirements.
          </p>
        </div>

        {/* Route Characteristics Explanation */}
        <div className="info-grid">
          <div className="info-item">
            <span className="info-item-icon">📏</span>
            <div className="info-item-content">
              <strong>Length (km)</strong>
              <span>Affects salt quantity and estimated gritting duration</span>
            </div>
          </div>
          <div className="info-item">
            <span className="info-item-icon">⚡</span>
            <div className="info-item-content">
              <strong>Priority Level</strong>
              <span>Determines gritting order and decision threshold</span>
            </div>
          </div>
        </div>

        {error && <div className="error">⚠️ {error}</div>}

        {/* Summary Stats */}
        <div className="routes-summary">
          <div className="summary-card">
            <span className="summary-icon">📊</span>
            <div className="summary-content">
              <span className="summary-value">{routes.length}</span>
              <span className="summary-label">Total Routes</span>
            </div>
          </div>
          <div className="summary-card">
            <span className="summary-icon">📏</span>
            <div className="summary-content">
              <span className="summary-value">
                {routes.reduce((sum, r) => sum + r.length_km, 0).toFixed(1)} km
              </span>
              <span className="summary-label">Total Coverage</span>
            </div>
          </div>
          <div className="summary-card">
            <span className="summary-icon">🚨</span>
            <div className="summary-content">
              <span className="summary-value">
                {routes.filter(r => r.priority === 1).length}
              </span>
              <span className="summary-label">Critical Routes</span>
            </div>
          </div>
        </div>

        {loading ? (
          <div className="loading-state">
            <span className="spinner">⏳</span>
            <p>Loading routes from database...</p>
          </div>
        ) : (
          <>
            {/* Priority Legend */}
            <div className="priority-legend">
              <span className="legend-title">Priority Levels:</span>
              <div className="legend-items">
                <span className="legend-item">
                  <span className="priority-dot critical"></span>
                  Critical (1)
                </span>
                <span className="legend-item">
                  <span className="priority-dot high"></span>
                  High (2)
                </span>
                <span className="legend-item">
                  <span className="priority-dot medium"></span>
                  Medium (3)
                </span>
                <span className="legend-item">
                  <span className="priority-dot low"></span>
                  Low (4+)
                </span>
              </div>
            </div>

            {/* Routes Grid */}
            <div className="routes-grid">
              {routes.map((route) => {
                const priority = getPriorityLabel(route.priority);
                return (
                  <div key={route.route_id} className="route-card">
                    <div className="route-card-header">
                      <span className="route-id">{route.route_id}</span>
                      <span className={`priority-badge ${priority.className}`}>
                        {priority.label}
                      </span>
                    </div>
                    <h3 className="route-name">{route.route_name}</h3>
                    <div className="route-stats">
                      <div className="route-stat">
                        <span className="stat-icon">📏</span>
                        <span className="stat-value">{route.length_km} km</span>
                      </div>
                      <div className="route-stat">
                        <span className="stat-icon">⚡</span>
                        <span className="stat-value">Priority {route.priority}</span>
                      </div>
                    </div>
                    <div className="route-priority-description">
                      {priority.description}
                    </div>
                  </div>
                );
              })}
            </div>
          </>
        )}

        {!loading && routes.length === 0 && !error && (
          <div className="empty-state">
            <span className="empty-icon">🛣️</span>
            <p>No routes found in the database</p>
          </div>
        )}
      </div>
    </div>
  );
}
