# Road Gritting ML Predictor - Solution Review

## Executive Summary

This review evaluates the road gritting ML prediction system for Edinburgh winter road maintenance. The solution demonstrates a well-structured approach to predicting gritting decisions using machine learning, but has several areas that need improvement before production deployment.

**Overall Assessment:** ⭐⭐⭐ (3/5) - Good Proof of Concept, Needs Production Hardening

---

## 1. ML Approach & Architecture Review

### ✅ Strengths

| Aspect | Assessment |
|--------|------------|
| **Multi-output approach** | Excellent choice - using separate models for classification (grit/no-grit) and regression (salt amount) |
| **Algorithm selection** | Random Forest is appropriate for tabular data with mixed features; handles non-linear relationships well |
| **Feature engineering** | Good engineered features: `temp_below_zero`, `surface_temp_below_zero`, `high_precip_prob` |
| **Risk calculation** | Domain-appropriate ice and snow risk calculations following NWSRG guidelines |
| **Model persistence** | Proper model serialization with pickle for deployment |

### ⚠️ Areas for Improvement

| Issue | Severity | Description |
|-------|----------|-------------|
| **Small dataset** | Medium | Only 72 samples is insufficient for robust model training. Recommend minimum 500+ samples |
| **Class imbalance** | Medium | 59 gritted (82%) vs 13 not_gritted (18%) - consider SMOTE or class weights |
| **Route imbalance** | Medium | R001 has 31 samples, R006 only 2 - model may not generalize well to underrepresented routes |
| **Amount model R² = 0.634** | Medium | Regression model has moderate explanatory power; may benefit from hyperparameter tuning |
| **No cross-validation** | Medium | Uses single train/test split; k-fold CV recommended for small datasets |

### Model Performance
```
Decision Model Accuracy: 93.3%
Amount Model R² Score: 0.634 (moderate)

Top Features (by importance):
1. wind_speed_kmh (16.7%)
2. temperature_c (14.4%)
3. feels_like_c (13.1%)
4. precipitation_prob_pct (10.2%)
5. humidity_pct (10.0%)
```

---

## 2. Critical Bugs Found

### 🔴 HIGH: LabelEncoder crashes on unseen precipitation types
**File:** `gritting_prediction_system.py:246-248`

```python
# Current code - will crash with unseen precipitation types
features_encoded['precipitation_type_encoded'] = self.label_encoders['precipitation_type'].transform(
    [features['precipitation_type']]
)[0]
```

**Problem:** Training data only contains `['none', 'rain', 'sleet', 'snow']`. Weather APIs may return 'hail', 'drizzle', 'freezing_rain', etc.

**Verified Error:**
```
ValueError: y contains previously unseen labels: 'hail'
```

**Recommended Fix:**
```python
# Add unknown value handling
known_types = ['none', 'rain', 'sleet', 'snow']
precip_type = features['precipitation_type']
if precip_type not in known_types:
    precip_type = 'rain' if 'rain' in precip_type.lower() else 'none'
```

---

### 🔴 HIGH: API fails at startup if models don't exist
**File:** `gritting_api.py:8-10`

```python
# Current code - runs at import time
system = GrittingPredictionSystem()
system.load_route_database('routes_database.csv')
system.load_models('models/gritting')  # Crashes if files don't exist
```

**Problem:** Fresh deployments or missing model files crash the entire Flask app.

**Recommended Fix:** Lazy-load models on first request with proper error handling.

---

### 🟡 MEDIUM: No input validation in API endpoints
**File:** `gritting_api.py:32-49`

Missing validation for:
- Required fields (`route_id`, `weather`)
- Weather data field types and ranges
- Route existence
- Null/missing JSON body

---

### 🟡 MEDIUM: Spread rate calculation anomaly
**File:** `gritting_prediction_system.py:272`

```python
spread_rate = int(salt_amount / (route_length * 1000) * 1000)
```

**Problem:** Predicted spread rates (e.g., 77 g/m²) can exceed UK NWSRG maximum of 40 g/m². The regression model predicts salt_amount independently, which may not align with route length calculations.

---

### 🟡 MEDIUM: External weather API has no error handling
**File:** `gritting_api.py:104-128`

```python
# No try-except around network call
response = requests.get(url)
data = response.json()
# Direct key access without checking
weather_data = {
    'temperature_c': data['main']['temp'],  # Will crash if API returns error
    ...
}
```

---

## 3. Code Quality Assessment

### ✅ Positive Aspects
- Clean class structure with separation of concerns
- Comprehensive docstrings
- Good use of type hints in comments
- Logical method organization

### ⚠️ Improvements Needed

| Category | Issue |
|----------|-------|
| **Dependencies** | No `requirements.txt` file - deployment will fail |
| **Type hints** | No Python type annotations (PEP 484) |
| **Logging** | Uses `print()` instead of `logging` module |
| **Constants** | Magic numbers scattered (e.g., 0.5 threshold, 60% precip) |
| **Config** | Hardcoded paths and values |

---

## 4. Security Considerations

| Risk | Severity | Description |
|------|----------|-------------|
| **Pickle deserialization** | Medium | Using `pickle.load()` is insecure if model files can be tampered with. Consider `joblib` or `safetensors` |
| **API key exposure** | High | `gritting_api.py:110` has `api_key = "YOUR_API_KEY"` hardcoded (should use env variables) |
| **No rate limiting** | Low | API endpoints have no rate limiting |
| **Debug mode enabled** | Medium | `app.run(debug=True)` should not be used in production |

---

## 5. Missing Components

| Component | Priority | Notes |
|-----------|----------|-------|
| `requirements.txt` | High | Required for deployment |
| `.gitignore` | High | Should exclude `models/`, `__pycache__/` |
| Unit tests | High | No test coverage |
| CI/CD pipeline | Medium | No GitHub Actions workflow |
| Docker support | Medium | Would simplify deployment |
| Model versioning | Medium | No MLflow or similar |

---

## 6. Recommendations

### Immediate (Before Production)

1. **Create `requirements.txt`:**
   ```
   pandas>=1.5.0
   numpy>=1.23.0
   scikit-learn>=1.1.0
   flask>=2.2.0
   requests>=2.28.0
   ```

2. **Fix LabelEncoder issue** - Add unknown value handling

3. **Add input validation** - Validate all API inputs

4. **Environment variables** - Move API keys and configs to env vars

5. **Disable debug mode** - Set `debug=False` for production

### Short-term (Next Sprint)

1. **Expand training dataset** - Target 500+ samples minimum
2. **Add unit tests** - Aim for 80% coverage
3. **Implement k-fold cross-validation**
4. **Add logging framework**
5. **Create Docker deployment**

### Long-term (Roadmap)

1. **Real weather API integration** with proper error handling
2. **Model retraining pipeline** with MLflow
3. **A/B testing framework** for model comparison
4. **Monitoring and alerting** for model drift
5. **Feature store** for consistent feature engineering

---

## 7. Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    Road Gritting ML System                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐  │
│  │ Weather API  │───▶│ Flask API    │───▶│ Prediction       │  │
│  │ (External)   │    │ gritting_api │    │ System           │  │
│  └──────────────┘    └──────────────┘    └──────────────────┘  │
│                              │                    │              │
│                              ▼                    ▼              │
│                      ┌──────────────┐    ┌──────────────────┐  │
│                      │ Routes DB    │    │ ML Models        │  │
│                      │ (.csv)       │    │ (pickle)         │  │
│                      └──────────────┘    └──────────────────┘  │
│                                                  │              │
│                                          ┌──────┴──────┐       │
│                                          ▼             ▼       │
│                                   ┌─────────┐   ┌───────────┐  │
│                                   │Decision │   │Amount     │  │
│                                   │Classifier│  │Regressor  │  │
│                                   │(RF)     │   │(RF)       │  │
│                                   └─────────┘   └───────────┘  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## 8. Conclusion

The Road Gritting ML Predictor is a **solid proof-of-concept** that demonstrates understanding of:
- ML pipeline design for operational decisions
- UK NWSRG winter maintenance standards
- REST API design patterns

However, it requires **significant hardening** before production deployment, particularly around:
- Error handling and input validation
- Model robustness with limited training data
- Security best practices
- Missing infrastructure files

**Recommendation:** Address critical bugs and add `requirements.txt` before any deployment. Plan for expanded training data and testing infrastructure in the next development cycle.

---

*Review conducted: 2026-01-23*
*Reviewer: GitHub Copilot Code Review*
