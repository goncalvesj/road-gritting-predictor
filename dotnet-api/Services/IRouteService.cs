using GrittingApi.Models;

namespace GrittingApi.Services;

/// <summary>
/// Interface for route data access
/// </summary>
public interface IRouteService
{
    List<RouteInfo> GetRoutes();
    RouteInfo? GetRoute(string routeId);
    bool RouteExists(string routeId);
}
