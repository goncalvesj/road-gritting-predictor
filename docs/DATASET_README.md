# Edinburgh Road Gritting ML Training Dataset

## Overview
This synthetic dataset combines real Edinburgh gritting routes with realistic winter weather patterns and gritting decisions based on UK winter maintenance standards.

## Dataset Structure

### Features (Input Variables)

| Column | Type | Description | Range/Values |
|--------|------|-------------|--------------|
| `date` | Date | Date of observation | 2024-11-15 to 2024-12-10 |
| `time` | Time | Time of gritting decision (24h format) | Typically 02:00-07:00, 17:00-22:00 |
| `route_id` | String | Unique route identifier | R001-R007 |
| `route_name` | String | Common name of the road | e.g., "Queensferry Road" |
| `priority` | Integer | Gritting priority level | 1 (highest) or 2 |
| `road_type` | String | Classification of road | A-road, B-road |
| `temperature_c` | Float | Air temperature in Celsius | -5.5 to 3.5 |
| `feels_like_c` | Float | Apparent temperature (wind chill) | -10.5 to 1.2 |
| `humidity_pct` | Integer | Relative humidity percentage | 62-93 |
| `wind_speed_kmh` | Integer | Wind speed in km/h | 7-28 |
| `precipitation_type` | String | Type of precipitation | none, rain, sleet, snow |
| `precipitation_prob_pct` | Integer | Probability of precipitation | 5-95 |
| `road_surface_temp_c` | Float | Road surface temperature | -6.8 to 4.5 |
| `forecast_min_temp_c` | Float | Forecasted minimum temperature | -8.0 to 2.5 |
| `ice_risk` | String | Risk level for ice formation | low, medium, high |
| `snow_risk` | String | Risk level for snow accumulation | low, medium, high |
| `route_length_km` | Float | Length of route in kilometers | 6.0-17.0 |

### Target Variables (Labels)

| Column | Type | Description | Values |
|--------|------|-------------|--------|
| `gritting_decision` | String | Whether gritting was performed | gritted, not_gritted |
| `salt_amount_kg` | Integer | Total salt/grit applied in kg | 0-1360 |
| `spread_rate_g_m2` | Integer | Salt spread rate in grams per m² | 0, 20, 25, 30, 35, 40 |
| `estimated_duration_min` | Integer | Estimated gritting duration in minutes | 0-56 |

## Decision Logic

The gritting decisions follow UK National Winter Service Research Group (NWSRG) guidelines:

### Gritting Triggers
1. **High Priority (Always grit if any condition met):**
   - Road surface temperature ≤ 0°C AND precipitation probability > 60%
   - Air temperature ≤ -2°C AND ice_risk = high
   - Snow risk = high AND precipitation probability > 70%
   - Priority 1 roads with forecast minimum temperature ≤ -3°C

2. **Medium Priority (Grit based on additional factors):**
   - Road surface temperature 0-1°C AND precipitation probability > 50%
   - Temperature dropping rapidly (>2°C drop expected)
   - Priority 1 roads with ice_risk = medium AND precipitation

3. **No Gritting:**
   - Road surface temperature > 2°C
   - Low ice/snow risk AND low precipitation probability
   - Priority 2 roads unless extreme conditions

### Spread Rates (based on NWSRG standards)
- **20 g/m²**: Light frost prevention, road temp 0-1°C
- **25 g/m²**: Standard preventive gritting, temp -1 to 0°C
- **30 g/m²**: Moderate conditions, sleet/light snow
- **35 g/m²**: Severe frost/ice conditions, temp -3 to -4°C
- **40 g/m²**: Heavy snow or extreme ice, temp < -4°C

### Salt Amount Calculation
```
salt_amount_kg = route_length_km × 1000 × spread_rate_g_m2 / 1000
```

## Routes Information

| Route ID | Route Name | Priority | Type | Length (km) |
|----------|------------|----------|------|-------------|
| R001 | Queensferry Road | 1 | A-road | 17.0 |
| R002 | Leith Walk | 1 | A-road | 14.4 |
| R003 | Morningside Road | 1 | B-road | 12.0 |
| R004 | Corstorphine Road | 1 | A-road | 14.9 |
| R005 | Dalkeith Road | 2 | B-road | 6.0 |
| R006 | Portobello Road | 2 | B-road | 8.5 |
| R007 | Ferry Road | 1 | A-road | 10.5 |

## ML Model Recommendations

### Classification Task: Predict Gritting Decision
**Features to use:**
- temperature_c
- road_surface_temp_c
- humidity_pct
- precipitation_type (one-hot encoded)
- precipitation_prob_pct
- ice_risk (ordinal: low=0, medium=1, high=2)
- snow_risk (ordinal: low=0, medium=1, high=2)
- priority
- forecast_min_temp_c

**Target:** `gritting_decision`

**Suggested algorithms:**
- Random Forest Classifier
- Gradient Boosting (XGBoost, LightGBM)
- Neural Network

### Regression Task: Predict Spread Rate
**Features:** Same as above + route_length_km

**Target:** `spread_rate_g_m2`

**Suggested algorithms:**
- Random Forest Regressor
- XGBoost Regressor
- Multi-output model (predict both decision AND spread rate)

## Sample Python Code

```python
import pandas as pd
from sklearn.model_selection import train_test_split
from sklearn.ensemble import RandomForestClassifier
from sklearn.preprocessing import LabelEncoder
from sklearn.metrics import classification_report, confusion_matrix

# Load data
df = pd.read_csv('edinburgh_gritting_training_dataset.csv')

# Feature engineering
df['temp_below_zero'] = (df['temperature_c'] < 0).astype(int)
df['surface_temp_below_zero'] = (df['road_surface_temp_c'] < 0).astype(int)
df['high_precip_prob'] = (df['precipitation_prob_pct'] > 60).astype(int)

# Encode categorical variables
le_precip = LabelEncoder()
df['precipitation_type_encoded'] = le_precip.fit_transform(df['precipitation_type'])

risk_mapping = {'low': 0, 'medium': 1, 'high': 2}
df['ice_risk_encoded'] = df['ice_risk'].map(risk_mapping)
df['snow_risk_encoded'] = df['snow_risk'].map(risk_mapping)

# Select features
feature_cols = [
    'temperature_c', 'feels_like_c', 'humidity_pct', 'wind_speed_kmh',
    'precipitation_type_encoded', 'precipitation_prob_pct',
    'road_surface_temp_c', 'forecast_min_temp_c',
    'ice_risk_encoded', 'snow_risk_encoded', 'priority',
    'temp_below_zero', 'surface_temp_below_zero', 'high_precip_prob'
]

X = df[feature_cols]
y = df['gritting_decision']

# Split data
X_train, X_test, y_train, y_test = train_test_split(
    X, y, test_size=0.2, random_state=42, stratify=y
)

# Train model
model = RandomForestClassifier(n_estimators=100, random_state=42)
model.fit(X_train, y_train)

# Evaluate
y_pred = model.predict(X_test)
print(classification_report(y_test, y_pred))
print("\nConfusion Matrix:")
print(confusion_matrix(y_test, y_pred))

# Feature importance
feature_importance = pd.DataFrame({
    'feature': feature_cols,
    'importance': model.feature_importances_
}).sort_values('importance', ascending=False)
print("\nTop 10 Features:")
print(feature_importance.head(10))
```

## Data Statistics

- **Total records:** 500
- **Gritting events:** 270 (54%)
- **No gritting events:** 230 (46%)
- **Date range:** November 1, 2024 - February 28, 2025
- **Unique routes:** 7
- **Priority 1 routes:** 5
- **Priority 2 routes:** 2
- **Records per route:** ~71-72 (balanced distribution)

## Extending the Dataset

To create more training data, you can:

1. **Add more routes** from Edinburgh Council's open data
2. **Extend date range** to cover full winter season (Nov-Mar)
3. **Integrate real weather API data** (e.g., OpenWeatherMap, Met Office)
4. **Add more features:**
   - Day of week (weekday gritting may differ)
   - Traffic volume
   - Proximity to hospitals/schools (priority routing)
   - Historical salt residue on road
   - UV index (affects ice melting)

## Data Sources & References

- Edinburgh Council Open Data: https://github.com/edinburghcouncil/datasets-transport
- NWSRG Spread Rate Guidelines: https://nwsrg.org/practical-guidance-documents
- UK Met Office Road Weather Information
- Clear Roads Winter Maintenance Research

## License

This is synthetic data generated for machine learning training purposes. 
Real Edinburgh route data is sourced from Edinburgh Council Open Data (Open Government License).

## Contact

For questions or to contribute additional data, please open an issue on GitHub.