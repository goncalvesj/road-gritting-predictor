namespace GrittingApi.Models;

/// <summary>
/// Route information from database
/// </summary>
public class RouteInfo
{
    public string route_id { get; set; } = string.Empty;
    public string route_name { get; set; } = string.Empty;
    public int priority { get; set; }
    public string road_type { get; set; } = string.Empty;
    public float route_length_km { get; set; }
}

/// <summary>
/// Response for routes endpoint
/// </summary>
public class RoutesResponse
{
    public List<RouteDto> routes { get; set; } = new();
}

/// <summary>
/// Route DTO for API response
/// </summary>
public class RouteDto
{
    public string route_id { get; set; } = string.Empty;
    public string route_name { get; set; } = string.Empty;
    public int priority { get; set; }
    public float length_km { get; set; }
}
