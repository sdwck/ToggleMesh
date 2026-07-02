CREATE TABLE IF NOT EXISTS default.AnalyticsExposures
(
    Id UUID,
    EnvironmentId UUID,
    FlagKey String,
    Identity String,
    Variant Bool,
    Timestamp DateTime64(3, 'UTC')
)
ENGINE = MergeTree()
ORDER BY (EnvironmentId, FlagKey, Timestamp);

CREATE TABLE IF NOT EXISTS default.AnalyticsTracks
(
    Id UUID,
    EnvironmentId UUID,
    Identity String,
    EventName String,
    Value Nullable(Float64),
    Properties String,
    Timestamp DateTime64(3, 'UTC')
)
ENGINE = MergeTree()
ORDER BY (EnvironmentId, EventName, Timestamp);
