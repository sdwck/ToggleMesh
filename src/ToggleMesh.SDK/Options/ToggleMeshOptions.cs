using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

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
    public bool UseFallbackFile { get; set; } = true;
    public IEnumerable<string> IdentityKeys { get; set; } = [];
}