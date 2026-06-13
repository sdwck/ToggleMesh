namespace ToggleMesh.API.Features.Flags.GetAll;

public class GetFlagsRequest
{
    public string Search { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
}