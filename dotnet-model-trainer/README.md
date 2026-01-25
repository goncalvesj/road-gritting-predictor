# Road Gritting ML.NET Model Trainer

A standalone .NET console application for training ML.NET models used by the Road Gritting API.

## Overview

This tool trains machine learning models from the Edinburgh gritting training dataset and outputs them in ML.NET's native `.zip` format. The trained models can then be loaded by the `dotnet-api` service for predictions.

## Features

- Trains FastTree binary classification model for gritting decision (yes/no)
- Trains FastTree regression model for salt amount prediction
- Validates model format and compatibility with ML.NET
- Outputs models in portable `.zip` format
- Generates precipitation type encoding file

## Prerequisites

- .NET 10 Preview SDK

## Usage

### Basic Usage

```bash
# From dotnet-model-trainer directory
dotnet run

# Uses default paths:
# - Training data: ../edinburgh_gritting_training_dataset.csv
# - Output: ../dotnet-api/models/
```

### Custom Paths

```bash
dotnet run <training_data_path> <output_dir>

# Example:
dotnet run ../edinburgh_gritting_training_dataset.csv ../dotnet-api/models
```

## Output Files

The trainer generates three files in the output directory:

1. `decision_model.zip` - Binary classification model for gritting decision
2. `amount_model.zip` - Regression model for salt amount prediction
3. `precip_encoding.json` - Precipitation type encoding mapping

## Model Details

### Features (15 total)

- Route metadata: priority, route_length_km
- Weather conditions: temperature_c, feels_like_c, humidity_pct, wind_speed_kmh, precipitation_prob_pct, road_surface_temp_c, forecast_min_temp_c
- Encoded: precipitation_type_encoded, ice_risk_encoded, snow_risk_encoded
- Engineered: temp_below_zero, surface_temp_below_zero, high_precip_prob

### Training Process

1. Load training data from CSV
2. Build precipitation type encoding
3. Engineer features
4. Train decision model (FastTree binary classifier)
5. Train amount model (FastTree regression)
6. Validate models can be loaded and used
7. Save models and encoding

### Validation

The trainer automatically validates:
- Models can be loaded from disk
- Prediction engines can be created
- Test predictions execute successfully

## Integration with API

After training, copy the models to the API's `models/` directory:

```bash
# The default output path already targets the API models folder
dotnet run

# Or manually copy if using custom output path
cp -r <output_dir>/* ../dotnet-api/models/
```

## Docker Integration

The Dockerfile in the repository root builds and runs the trainer as part of the multi-stage build process. See the main Dockerfile for details.

## Dependencies

- **Microsoft.ML**: ML.NET framework
- **Microsoft.ML.FastTree**: FastTree algorithms
- **CsvHelper**: CSV parsing

## License

MIT License - see parent repository for details.
