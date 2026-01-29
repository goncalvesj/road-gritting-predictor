using GrittingApi.Models;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace GrittingApi.Services;

/// <summary>
/// CSV-based route service for backwards compatibility
/// </summary>
public class CsvRouteService : IRouteService
{
    private Dictionary<string, RouteInfo> _routeLookup = new();
    private readonly ILogger<CsvRouteService> _logger;

    public CsvRouteService(ILogger<CsvRouteService> logger)
    {
        _logger = logger;
    }

    public void LoadRoutes(string routesPath)
    {
        using var reader = new StreamReader(routesPath);
        using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture));
        
        var routes = csv.GetRecords<RouteInfo>().ToList();
        _routeLookup = routes.ToDictionary(r => r.route_id);
        
        _logger.LogInformation("Loaded {Count} routes from CSV", _routeLookup.Count);
    }

    public List<RouteInfo> GetRoutes() => _routeLookup.Values.ToList();

    public RouteInfo? GetRoute(string routeId) => 
        _routeLookup.TryGetValue(routeId, out var route) ? route : null;

    public bool RouteExists(string routeId) => _routeLookup.ContainsKey(routeId);
}
