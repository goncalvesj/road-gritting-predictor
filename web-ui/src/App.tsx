import { useState } from 'react';
import type { Page } from './types';
import { RoutesPage } from './RoutesPage';
import { HistoryPage } from './HistoryPage';
import { AutoWeatherPage } from './AutoWeatherPage';
import { ManualPredictorPage } from './ManualPredictorPage';
import './index.css';

function App() {
  const [currentPage, setCurrentPage] = useState<Page>('auto-weather');

  // Sub-pages with back navigation
  if (currentPage === 'routes') {
    return (
      <div className="app">
        <header className="header">
          <h1>🚛 Road Gritting Predictor</h1>
          <p>ML-powered winter road maintenance decisions</p>
        </header>
        <div className="container">
          <RoutesPage onBack={() => setCurrentPage('auto-weather')} />
        </div>
      </div>
    );
  }

  if (currentPage === 'history') {
    return (
      <div className="app">
        <header className="header">
          <h1>🚛 Road Gritting Predictor</h1>
          <p>ML-powered winter road maintenance decisions</p>
        </header>
        <div className="container">
          <HistoryPage onBack={() => setCurrentPage('auto-weather')} />
        </div>
      </div>
    );
  }

  if (currentPage === 'predictor') {
    return (
      <div className="app">
        <header className="header">
          <h1>🚛 Road Gritting Predictor</h1>
          <p>ML-powered winter road maintenance decisions</p>
        </header>
        <div className="container">
          <ManualPredictorPage onBack={() => setCurrentPage('auto-weather')} />
        </div>
      </div>
    );
  }

  // Main view: Auto Weather (default)
  return (
    <div className="app">
      <header className="header">
        <h1>🚛 Road Gritting Predictor</h1>
        <p>ML-powered winter road maintenance decisions</p>
        <div className="header-subtitle">
          Predicts whether roads need gritting based on real-time weather conditions and route characteristics
        </div>
      </header>

      <div className="container">
        {/* How it Works Section */}
        <div className="how-it-works-card">
          <h3>How It Works</h3>
          <div className="workflow-steps">
            <div className="workflow-step">
              <div className="step-number">1</div>
              <div className="step-content">
                <strong>Select Route</strong>
                <span>Choose from predefined road routes with known characteristics (length, priority, traffic)</span>
              </div>
            </div>
            <div className="workflow-arrow">→</div>
            <div className="workflow-step">
              <div className="step-number">2</div>
              <div className="step-content">
                <strong>Get Weather</strong>
                <span>Real-time weather fetched from Open-Meteo API for your location</span>
              </div>
            </div>
            <div className="workflow-arrow">→</div>
            <div className="workflow-step">
              <div className="step-number">3</div>
              <div className="step-content">
                <strong>ML Prediction</strong>
                <span>Model analyzes weather + route data to predict gritting needs</span>
              </div>
            </div>
          </div>
        </div>

        {/* Navigation Cards */}
        <div className="nav-cards">
          <button className="nav-card active" onClick={() => setCurrentPage('auto-weather')}>
            <span className="nav-icon">🌤️</span>
            <span className="nav-label">Auto Weather</span>
          </button>
          <button className="nav-card" onClick={() => setCurrentPage('predictor')}>
            <span className="nav-icon">🌡️</span>
            <span className="nav-label">Manual Input</span>
          </button>
          <button className="nav-card" onClick={() => setCurrentPage('routes')}>
            <span className="nav-icon">🛣️</span>
            <span className="nav-label">View Routes</span>
          </button>
          <button className="nav-card" onClick={() => setCurrentPage('history')}>
            <span className="nav-icon">📜</span>
            <span className="nav-label">History</span>
          </button>
        </div>

        <AutoWeatherPage />
      </div>
    </div>
  );
}

export default App;
