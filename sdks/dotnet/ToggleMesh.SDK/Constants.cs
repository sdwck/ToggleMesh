namespace ToggleMesh.SDK;

public static class Constants
{
    public static class Endpoints
    {
        public const string GetAll = "/api/v1/sdk/flags";
        public const string Metrics = "/api/v1/sdk/metrics";
        public const string Events = "/api/v1/sdk/events";
        public const string SseStream = "/api/v1/stream";
    }
}