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
    totalSalt: history.reduce((sum, h) => sum + h.salt_amount_kg, 0),
  };

  const getPrecipIcon = (type: string) => {
    switch (type.toLowerCase()) {
      case 'snow': return '❄️';
      case 'rain': return '🌧️';
      case 'sleet': return '🌨️';
      default: return '☀️';
    }
  };

  return (
    <div className="card">
      <div className="page-header-bar">
        <button className="back-button" onClick={onBack}>← Back</button>
        <h2>📜 Prediction History</h2>
      </div>

      <div className="card-body">
        {/* Page Description */}
        <div className="page-description">
          <p>
            <strong>What is this?</strong> A log of all gritting predictions made by the ML model. 
            Each entry shows the input weather conditions and the model's output decision. 
            Use this to review past decisions and analyze patterns.
          </p>
        </div>

        {/* Data Flow Reminder */}
        <div className="history-data-flow">
          <div className="data-flow-item">
            <span className="data-flow-label">📥 Input</span>
            <span className="data-flow-text">Route + Weather</span>
          </div>
          <span className="data-flow-arrow">→</span>
          <div className="data-flow-item">
            <span className="data-flow-label">🤖 Model</span>
            <span className="data-flow-text">ML Analysis</span>
          </div>
          <span className="data-flow-arrow">→</span>
          <div className="data-flow-item">
            <span className="data-flow-label">📤 Output</span>
            <span className="data-flow-text">Decision + Details</span>
          </div>
        </div>

        {/* Stats Overview */}
        <div className="history-stats-grid">
          <div className="history-stat-card">
            <span className="stat-icon-large">📊</span>
            <div className="stat-info">
              <span className="stat-value-large">{stats.total}</span>
              <span className="stat-label">Total Predictions</span>
            </div>
          </div>
          <div className="history-stat-card gritted">
            <span className="stat-icon-large">✅</span>
            <div className="stat-info">
              <span className="stat-value-large">{stats.gritted}</span>
              <span className="stat-label">Gritting Required</span>
            </div>
          </div>
          <div className="history-stat-card not-gritted">
            <span className="stat-icon-large">⏸️</span>
            <div className="stat-info">
              <span className="stat-value-large">{stats.notGritted}</span>
              <span className="stat-label">No Gritting Needed</span>
            </div>
          </div>
          <div className="history-stat-card salt">
            <span className="stat-icon-large">🧂</span>
            <div className="stat-info">
              <span className="stat-value-large">{(stats.totalSalt / 1000).toFixed(1)}t</span>
              <span className="stat-label">Total Salt Used</span>
            </div>
          </div>
        </div>

        {/* Filter Tabs */}
        <div className="filter-tabs">
          <button 
            className={`filter-tab ${filter === 'all' ? 'active' : ''}`}
            onClick={() => setFilter('all')}
          >
            All ({stats.total})
          </button>
          <button 
            className={`filter-tab ${filter === 'yes' ? 'active' : ''}`}
            onClick={() => setFilter('yes')}
          >
            ✅ Gritted ({stats.gritted})
          </button>
          <button 
            className={`filter-tab ${filter === 'no' ? 'active' : ''}`}
            onClick={() => setFilter('no')}
          >
            ⏸️ Not Gritted ({stats.notGritted})
          </button>
        </div>

        {loading ? (
          <div className="loading-state">
            <span className="spinner">⏳</span>
            <p>Loading prediction history...</p>
          </div>
        ) : (
          <div className="history-list">
            {filteredHistory.map((decision) => (
              <div key={decision.id} className={`history-item decision-${decision.gritting_decision}`}>
                <div className="history-item-header">
                  <span className={`decision-badge-large ${decision.gritting_decision === 'yes' ? 'gritted' : 'not-gritted'}`}>
                    {decision.gritting_decision === 'yes' ? '✅ Gritting Required' : '⏸️ No Gritting'}
                  </span>
                  <span className="history-id">{decision.id}</span>
                </div>
                
                <div className="history-item-meta">
                  <div className="meta-item">
                    <span className="meta-icon">🛣️</span>
                    <span className="meta-text">{decision.route_name}</span>
                  </div>
                  <div className="meta-item">
                    <span className="meta-icon">🕐</span>
                    <span className="meta-text">{formatDate(decision.timestamp)}</span>
                  </div>
                </div>

                <div className="history-item-sections">
                  {/* Input Section */}
                  <div className="history-section input">
                    <span className="section-tag">📥 Input Weather</span>
                    <div className="section-data">
                      <div className="data-chip">
                        <span>🌡️</span>
                        <span>{decision.temperature_c.toFixed(1)}°C</span>
                      </div>
                      <div className="data-chip">
                        <span>{getPrecipIcon(decision.precipitation_type)}</span>
                        <span>{decision.precipitation_type}</span>
                      </div>
                    </div>
                  </div>

                  {/* Output Section */}
                  <div className="history-section output">
                    <span className="section-tag">📤 Model Output</span>
                    <div className="section-data">
                      <div className="data-chip">
                        <span>🎯</span>
                        <span>{(decision.decision_confidence * 100).toFixed(0)}% conf</span>
                      </div>
                      {decision.gritting_decision === 'yes' && (
                        <div className="data-chip highlight">
                          <span>🧂</span>
                          <span>{decision.salt_amount_kg} kg</span>
                        </div>
                      )}
                    </div>
                  </div>
                </div>

                <div className="history-risks">
                  <span className={`mini-badge risk-${decision.ice_risk}`}>
                    🧊 Ice: {decision.ice_risk}
                  </span>
                  <span className={`mini-badge risk-${decision.snow_risk}`}>
                    ❄️ Snow: {decision.snow_risk}
                  </span>
                </div>
              </div>
            ))}
          </div>
        )}

        {!loading && filteredHistory.length === 0 && (
          <div className="empty-state">
            <span className="empty-icon">📜</span>
            <p>No predictions match the current filter</p>
          </div>
        )}
      </div>
    </div>
  );
}
