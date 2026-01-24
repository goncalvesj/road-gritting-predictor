# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY dotnet-api/GrittingApi.csproj ./dotnet-api/
WORKDIR /src/dotnet-api
RUN dotnet restore

# Copy source code
COPY dotnet-api/ .

# Build the application
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Copy published app
COPY --from=publish /app/publish .

# Copy data files
COPY routes_database.csv .
COPY edinburgh_gritting_training_dataset.csv .

# Expose port
EXPOSE 5000

# Set environment
ENV ASPNETCORE_URLS=http://+:5000

# Run the application
ENTRYPOINT ["dotnet", "GrittingApi.dll"]
