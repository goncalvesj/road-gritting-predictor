import { useState, useEffect } from 'react';
import type { HistoricalDecision } from './types';

interface HistoryPageProps {
  onBack: () => void;
}

// Constants for mock data generation
const MS_PER_HOUR = 3600000;
const STARTING_DECISION_ID = 1000;

// Mock historical data - in a real app this would come from an API
const generateMockHistory = (): HistoricalDecision[] => {
  const routes = [
    { id: 'R001', name: 'Queensferry Road' },
    { id: 'R002', name: 'Leith Walk' },
    { id: 'R003', name: 'Morningside Road' },
    { id: 'R004', name: 'Gorgie Road' },
    { id: 'R005', name: 'Dalry Road' },
  ];

  const precipTypes = ['none', 'snow', 'rain', 'sleet'];
  const risks = ['low', 'medium', 'high'];
  const decisions = ['yes', 'no'];

  const history: HistoricalDecision[] = [];
  const now = new Date();

  for (let i = 0; i < 20; i++) {
    const route = routes[Math.floor(Math.random() * routes.length)];
    const decision = decisions[Math.floor(Math.random() * decisions.length)];
    const timestamp = new Date(now.getTime() - i * MS_PER_HOUR * (1 + Math.random() * 5));
    
    history.push({
      id: `DEC-${String(STARTING_DECISION_ID - i).padStart(4, '0')}`,
      timestamp: timestamp.toISOString(),
      route_id: route.id,
      route_name: route.name,
      gritting_decision: decision,
      decision_confidence: 0.75 + Math.random() * 0.25,
      salt_amount_kg: decision === 'yes' ? Math.floor(500 + Math.random() * 1000) : 0,
      ice_risk: risks[Math.floor(Math.random() * risks.length)],
      snow_risk: risks[Math.floor(Math.random() * risks.length)],
      temperature_c: -10 + Math.random() * 15,
      precipitation_type: precipTypes[Math.floor(Math.random() * precipTypes.length)],
    });
  }

  return history.sort((a, b) => 
    new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
  );
};

export function HistoryPage({ onBack }: HistoryPageProps) {
  const [history, setHistory] = useState<HistoricalDecision[]>([]);
  const [loading, setLoading] = useState(true);
  const [filter, setFilter] = useState<'all' | 'yes' | 'no'>('all');

  useEffect(() => {
    // Simulate API loading
    setTimeout(() => {
      setHistory(generateMockHistory());
      setLoading(false);
    }, 500);
  }, []);

  const formatDate = (isoString: string) => {
    const date = new Date(isoString);
    return date.toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    });
  };

  const filteredHistory = history.filter(h => 
    filter === 'all' || h.gritting_decision === filter
  );

  const stats = {
    total: history.length,
    gritted: history.filter(h => h.gritting_decision === 'yes').length,
    notGritted: history.filter(h => h.gritting_decision === 'no').length,
  };

  return (
    <div className="page-content">
      <div className="page-header-bar">
        <button className="back-button" onClick={onBack}>
          ← Back
        </button>
        <h2>📜 Decision History</h2>
      </div>

      {/* Stats Overview */}
      <div className="history-stats">
        <div className="stat-pill">
          <span className="stat-number">{stats.total}</span>
          <span className="stat-text">Total</span>
        </div>
        <div className="stat-pill gritted">
          <span className="stat-number">{stats.gritted}</span>
          <span className="stat-text">Gritted</span>
        </div>
        <div className="stat-pill not-gritted">
          <span className="stat-number">{stats.notGritted}</span>
          <span className="stat-text">Not Gritted</span>
        </div>
      </div>

      {/* Filter Tabs */}
      <div className="filter-tabs">
        <button 
          className={`filter-tab ${filter === 'all' ? 'active' : ''}`}
          onClick={() => setFilter('all')}
        >
          All Decisions
        </button>
        <button 
          className={`filter-tab ${filter === 'yes' ? 'active' : ''}`}
          onClick={() => setFilter('yes')}
        >
          ✅ Gritted
        </button>
        <button 
          className={`filter-tab ${filter === 'no' ? 'active' : ''}`}
          onClick={() => setFilter('no')}
        >
          ⏸️ Not Gritted
        </button>
      </div>

      {loading ? (
        <div className="loading-state">
          <span className="spinner">⏳</span>
          <p>Loading history...</p>
        </div>
      ) : (
        <div className="history-list">
          {filteredHistory.map((decision) => (
            <div key={decision.id} className={`history-item decision-${decision.gritting_decision}`}>
              <div className="history-item-header">
                <span className="decision-badge">
                  {decision.gritting_decision === 'yes' ? '✅ Gritted' : '⏸️ Not Gritted'}
                </span>
                <span className="history-id">{decision.id}</span>
              </div>
              
              <div className="history-item-body">
                <div className="history-route">
                  <span className="route-icon">🛣️</span>
                  <span>{decision.route_name}</span>
                </div>
                <div className="history-timestamp">
                  <span className="time-icon">🕐</span>
                  <span>{formatDate(decision.timestamp)}</span>
                </div>
              </div>

              <div className="history-details">
                <div className="detail-item">
                  <span className="detail-label">Temperature</span>
                  <span className="detail-value">{decision.temperature_c.toFixed(1)}°C</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Precipitation</span>
                  <span className="detail-value">{decision.precipitation_type}</span>
                </div>
                <div className="detail-item">
                  <span className="detail-label">Confidence</span>
                  <span className="detail-value">{(decision.decision_confidence * 100).toFixed(0)}%</span>
                </div>
                {decision.gritting_decision === 'yes' && (
                  <div className="detail-item highlight">
                    <span className="detail-label">Salt Used</span>
                    <span className="detail-value">{decision.salt_amount_kg} kg</span>
                  </div>
                )}
              </div>

              <div className="history-risks">
                <span className={`mini-badge risk-${decision.ice_risk}`}>
                  🧊 {decision.ice_risk}
                </span>
                <span className={`mini-badge risk-${decision.snow_risk}`}>
                  ❄️ {decision.snow_risk}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}

      {!loading && filteredHistory.length === 0 && (
        <div className="empty-state">
          <span className="empty-icon">📜</span>
          <p>No decisions found</p>
        </div>
      )}
    </div>
  );
}
