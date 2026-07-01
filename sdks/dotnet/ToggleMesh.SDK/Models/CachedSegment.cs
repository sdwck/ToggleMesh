using ToggleMesh.Common.Rules;

namespace ToggleMesh.SDK.Models;

internal class CachedSegment
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CompiledRuleGroup[] Groups { get; set; } = [];
}
