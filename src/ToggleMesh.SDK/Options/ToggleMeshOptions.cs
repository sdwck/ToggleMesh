using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace ToggleMesh.SDK.Options;

public class ToggleMeshOptions
{
    [Required]
    public string EndpointUrl { get; set; } = string.Empty;
}