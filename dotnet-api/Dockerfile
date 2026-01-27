# Build stage for API
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY dotnet-api/GrittingApi.csproj ./dotnet-api/
COPY dotnet-model-trainer/ModelTrainer.csproj ./dotnet-model-trainer/
WORKDIR /src/dotnet-api
RUN dotnet restore

# Also restore ModelTrainer
WORKDIR /src/dotnet-model-trainer
RUN dotnet restore

# Copy all source code
WORKDIR /src
COPY dotnet-api/ ./dotnet-api/
COPY dotnet-model-trainer/ ./dotnet-model-trainer/

# Build the API
WORKDIR /src/dotnet-api
RUN dotnet build -c Release -o /app/build

# Build the ModelTrainer
WORKDIR /src/dotnet-model-trainer
RUN dotnet build -c Release -o /app/trainer

# Publish stage
FROM build AS publish
WORKDIR /src/dotnet-api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Model training stage - trains models before running API
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS trainer
WORKDIR /app

# Copy trainer and data files
COPY --from=build /app/trainer .
COPY edinburgh_gritting_training_dataset.csv .
COPY routes_database.csv .

# Train models
RUN mkdir -p /models && \
    dotnet ModelTrainer.dll edinburgh_gritting_training_dataset.csv /models

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Copy data files
COPY routes_database.csv .

# Copy pre-trained models from trainer stage
COPY --from=trainer /models ./models

# Expose port
EXPOSE 5000

# Set environment
ENV ASPNETCORE_URLS=http://+:5000

# Run the application
ENTRYPOINT ["dotnet", "GrittingApi.dll"]
