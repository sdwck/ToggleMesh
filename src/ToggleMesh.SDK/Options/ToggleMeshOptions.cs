using System.ComponentModel.DataAnnotations;

namespace ToggleMesh.SDK.Options;

public class ToggleMeshOptions
{
    /// <summary>
    /// Base URL of the ToggleMesh API.
    /// </summary>
    [Required]
    public string EndpointUrl { get; set; } = string.Empty;
    /// <summary>
    /// API key for authenticating with the ToggleMesh API.
    /// This should be the API key associated with the environment you want to fetch flags for.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Path to the local JSON fallback file.
    /// If not provided, a default path in the application's base directory will be used.
    /// </summary>
    public string? FallbackFilePath { get; set; }
    /// <summary>
    /// Whether to use the fallback file when the API is unreachable.
    /// </summary>
    public bool UseFallbackFile { get; set; } = true;
    /// <summary>
    /// Identity keys to include in the context when evaluating flags.
    /// </summary>
    public IEnumerable<string> IdentityKeys { get; set; } = [];
    /// <summary>
    /// Whether to enable metrics collection for flag evaluations.
    /// </summary>
    public bool IsMetricsEnabled { get; set; } = true;
}