#include "ToggleMeshSettings.h"

UToggleMeshSettings::UToggleMeshSettings()
{
	BaseUrl = TEXT("https://api.togglemesh.dev");
	ClientKey = TEXT("");
	RefreshInterval = 60;
	bIsMetricsEnabled = true;
	AnalyticsChannelCapacity = 10000;
	MetricsBufferCapacity = 10000;
	MaxBatchSize = 2000;
}
