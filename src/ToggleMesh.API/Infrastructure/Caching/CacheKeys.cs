namespace ToggleMesh.API.Infrastructure.Caching;

public static class CacheKeys
{
    public static string ApiKey(string keyHash) => $"apikey:{keyHash}";
    public static string ProjectMemberState(Guid projectId, Guid userId) => $"project-member-state:{projectId}:{userId}";
    public static string FlagState(Guid environmentId, string flagKey) => $"flags:{environmentId}:{flagKey}";
    public static string SdkCompiledRules(Guid environmentId) => $"sdk:compiled_rules:{environmentId}";
    public static string SdkFlagsStates(Guid environmentId) => $"sdk:flags:states:{environmentId}";
    public static string EventSchemaHasValue(Guid environmentId, string eventName) => $"event_schema:{environmentId}:{eventName}:hasValue";
    public static string FlagSchemaContextKeys(Guid environmentId, string flagKey) => $"flag_schema:{environmentId}:{flagKey}:context_keys";
    public static string UniqueEvents(Guid environmentId) => $"events:{environmentId}";
}
