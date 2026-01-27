namespace GrittingApi.Models;

/// <summary>
/// Health check response
/// </summary>
public class HealthResponse
{
    public string status { get; set; } = string.Empty;
    public bool models_loaded { get; set; }
    public string? error { get; set; }
}
