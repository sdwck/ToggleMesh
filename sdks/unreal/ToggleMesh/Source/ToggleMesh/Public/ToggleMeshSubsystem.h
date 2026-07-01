#pragma once

#include "CoreMinimal.h"
#include "Subsystems/GameInstanceSubsystem.h"
#include "Engine/TimerHandle.h"
#include "HttpModule.h"
#include "Interfaces/IHttpRequest.h"
#include "Interfaces/IHttpResponse.h"

class FJsonObject;

#include "ToggleMeshSubsystem.generated.h"

USTRUCT()
struct FToggleMeshMetricCounts
{
	GENERATED_BODY()

	int32 TrueCount = 0;
	int32 FalseCount = 0;
};

USTRUCT()
struct FToggleMeshFlagState
{
	GENERATED_BODY()

	bool bIsEnabled = false;
	bool bIsExperimentActive = false;
};

DECLARE_DYNAMIC_MULTICAST_DELEGATE(FOnToggleMeshFlagsUpdated);

UCLASS()
class TOGGLEMESH_API UToggleMeshSubsystem : public UGameInstanceSubsystem
{
	GENERATED_BODY()

public:
	virtual void Initialize(FSubsystemCollectionBase& Collection) override;
	virtual void Deinitialize() override;

	UFUNCTION(BlueprintCallable, Category="ToggleMesh")
	void Identify(const FString& InUserId, const TMap<FString, FString>& InContext);

	UFUNCTION(BlueprintCallable, Category="ToggleMesh", meta=(AdvancedDisplay="Value, bHasValue"))
	void TrackEvent(const FString& EventName, float Value = 0.0f, bool bHasValue = false);

	UFUNCTION(BlueprintPure, Category="ToggleMesh")
	bool GetBoolFlag(const FString& FlagKey, bool bDefaultValue = false) const;

	UFUNCTION(BlueprintCallable, Category="ToggleMesh")
	void FlushEvents();

	UFUNCTION(BlueprintCallable, Category="ToggleMesh")
	void FlushMetrics();

	UPROPERTY(BlueprintAssignable, Category="ToggleMesh")
	FOnToggleMeshFlagsUpdated OnFlagsUpdated;

private:
	void FetchFlags();
	void OnFlagsResponse(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bConnectedSuccessfully);
	void OnTrackResponse(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bConnectedSuccessfully);
	void OnMetricsResponse(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bConnectedSuccessfully);

	FString CurrentUserId;
	TMap<FString, FString> CurrentContext;
	TMap<FString, FToggleMeshFlagState> FlagsCache;

	mutable TMap<FString, FToggleMeshMetricCounts> MetricsBuffer;
	mutable TArray<TSharedPtr<FJsonObject>> EventBuffer;

	FTimerHandle RefreshTimerHandle;
};
