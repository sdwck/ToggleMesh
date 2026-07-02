#pragma once

#include "CoreMinimal.h"
#include "Engine/DeveloperSettings.h"
#include "ToggleMeshSettings.generated.h"

UCLASS(Config=Game, defaultconfig, meta=(DisplayName="ToggleMesh"))
class TOGGLEMESH_API UToggleMeshSettings : public UDeveloperSettings
{
	GENERATED_BODY()

public:
	UToggleMeshSettings();

	UPROPERTY(Config, EditAnywhere, Category="General")
	FString BaseUrl;

	UPROPERTY(Config, EditAnywhere, Category="General")
	FString ClientKey;

	UPROPERTY(Config, EditAnywhere, Category="General")
	int32 RefreshInterval;

	UPROPERTY(Config, EditAnywhere, Category="Metrics")
	bool bIsMetricsEnabled;

	UPROPERTY(Config, EditAnywhere, Category="Metrics")
	int32 AnalyticsChannelCapacity;

	UPROPERTY(Config, EditAnywhere, Category="Metrics")
	int32 MetricsBufferCapacity;

	UPROPERTY(Config, EditAnywhere, Category="Metrics")
	int32 MaxBatchSize;
};
