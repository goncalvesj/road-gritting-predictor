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
      case 1: return { label: 'Critical', className: 'priority-critical' };
      case 2: return { label: 'High', className: 'priority-high' };
      case 3: return { label: 'Medium', className: 'priority-medium' };
      default: return { label: 'Low', className: 'priority-low' };
    }
  };

  return (
    <div className="page-content">
      <div className="page-header-bar">
        <button className="back-button" onClick={onBack}>
          ← Back
        </button>
        <h2>🛣️ Route Validation</h2>
      </div>

      {error && <div className="error">⚠️ {error}</div>}

      {loading ? (
        <div className="loading-state">
          <span className="spinner">⏳</span>
          <p>Loading routes...</p>
        </div>
      ) : (
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
                <div className="route-status">
                  <span className="status-indicator active"></span>
                  <span>Active</span>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {!loading && routes.length === 0 && !error && (
        <div className="empty-state">
          <span className="empty-icon">🛣️</span>
          <p>No routes found</p>
        </div>
      )}

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
            <span className="summary-label">Total Distance</span>
          </div>
        </div>
        <div className="summary-card">
          <span className="summary-icon">⚡</span>
          <div className="summary-content">
            <span className="summary-value">
              {routes.filter(r => r.priority === 1).length}
            </span>
            <span className="summary-label">Critical Routes</span>
          </div>
        </div>
      </div>
    </div>
  );
}
