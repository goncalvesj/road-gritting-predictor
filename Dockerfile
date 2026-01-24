# Use Python 3.11 slim image for smaller size
FROM python:3.11-slim

# Set working directory
WORKDIR /app

# Set environment variables
ENV PYTHONDONTWRITEBYTECODE=1 \
    PYTHONUNBUFFERED=1 \
    FLASK_DEBUG=0

# Copy requirements first for better caching
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Copy application code
COPY gritting_prediction_system.py .
COPY gritting_api.py .
COPY edinburgh_gritting_training_dataset.csv .
COPY routes_database.csv .

# Create models directory
RUN mkdir -p models

# Train the models during build (optional - can be done at runtime)
RUN python gritting_prediction_system.py

# Expose the API port
EXPOSE 5000

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD python -c "import requests; exit(0 if requests.get('http://localhost:5000/health', timeout=5).status_code == 200 else 1)"

# Run the Flask API
CMD ["python", "gritting_api.py"]
