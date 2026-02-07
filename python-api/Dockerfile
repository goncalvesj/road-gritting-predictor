# Use Python 3.11 slim image for smaller size
FROM python:3.11-slim

# Set working directory
WORKDIR /app

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    FLASK_DEBUG=0

# Install curl for health checks and clean up apt cache
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Copy requirements first for better caching
COPY python-api/requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY python-api/gritting_predictor.py .
COPY python-api/gritting_data_service.py .
COPY python-api/model_trainer.py .
COPY python-api/gritting_api.py .
COPY python-api/open_meteo_weather_service.py .

# Copy SQLite database to expected location (scripts reference ../data/)
RUN mkdir -p ../data
COPY data/gritting_data.db ../data/

# Create models directory
RUN mkdir -p models

# Train the models during build
# Note: This increases image size but ensures models are ready at startup.
# If training fails, the build will fail - allowing early detection of issues.
RUN python model_trainer.py

# Expose the API port
EXPOSE 8080

# Health check using curl (lighter weight than Python)
HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Run the Flask API
CMD ["python", "gritting_api.py"]
