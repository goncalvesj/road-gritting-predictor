using GrittingApi.Models;
using Microsoft.Data.Sqlite;

namespace GrittingApi.Services;

/// <summary>
/// SQLite-based route service (default)
/// </summary>
public class SqliteRouteService : IRouteService
{
    private Dictionary<string, RouteInfo> _routeLookup = new();
    private readonly ILogger<SqliteRouteService> _logger;

    public SqliteRouteService(ILogger<SqliteRouteService> logger)
    {
        _logger = logger;
    }

    public void LoadRoutes(string dbPath)
    {
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT route_id, route_name, priority, road_type, route_length_km FROM routes";

        using var reader = command.ExecuteReader();
        var routes = new List<RouteInfo>();
        
        while (reader.Read())
        {
            routes.Add(new RouteInfo
            {
                route_id = reader.GetString(0),
                route_name = reader.GetString(1),
                priority = reader.GetInt32(2),
                road_type = reader.GetString(3),
                route_length_km = reader.GetFloat(4)
            });
        }

        _routeLookup = routes.ToDictionary(r => r.route_id);
        _logger.LogInformation("Loaded {Count} routes from SQLite database", _routeLookup.Count);
    }

    public List<RouteInfo> GetRoutes() => _routeLookup.Values.ToList();

    public RouteInfo? GetRoute(string routeId) => 
        _routeLookup.TryGetValue(routeId, out var route) ? route : null;

    public bool RouteExists(string routeId) => _routeLookup.ContainsKey(routeId);
}
