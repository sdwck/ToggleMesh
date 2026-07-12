#include "ToggleMeshSubsystem.h"
#include "ToggleMeshSettings.h"
#include "HttpModule.h"
#include "JsonUtilities.h"
#include "Misc/Guid.h"
#include "Engine/World.h"
#include "TimerManager.h"

void UToggleMeshSubsystem::Initialize(FSubsystemCollectionBase& Collection)
{
	Super::Initialize(Collection);

	CurrentUserId = FGuid::NewGuid().ToString();
	
	const UToggleMeshSettings* Settings = GetDefault<UToggleMeshSettings>();
	if (Settings->RefreshInterval > 0)
	{
		GetWorld()->GetTimerManager().SetTimer(
			RefreshTimerHandle, 
			this, 
			&UToggleMeshSubsystem::FetchFlags, 
			Settings->RefreshInterval, 
			true
		);
	}

	FetchFlags();
}

void UToggleMeshSubsystem::Deinitialize()
{
	FlushEvents();
	FlushMetrics();

	if (GetWorld())
	{
		GetWorld()->GetTimerManager().ClearTimer(RefreshTimerHandle);
	}

	Super::Deinitialize();
}

void UToggleMeshSubsystem::Identify(const FString& InUserId, const TMap<FString, FString>& InContext)
{
	CurrentUserId = InUserId;
	CurrentContext = InContext;
	FetchFlags();
}

bool UToggleMeshSubsystem::GetBoolFlag(const FString& FlagKey, bool bDefaultValue) const
{
	bool bResult = bDefaultValue;
	FString VariationId = TEXT("");
	bool bIsExperiment = false;

	if (const FToggleMeshFlagState* Found = FlagsCache.Find(FlagKey))
	{
		bResult = Found->VariationValue.Equals(TEXT("true"), ESearchCase::IgnoreCase);
		VariationId = Found->VariationId;
		bIsExperiment = Found->bIsExperimentActive;
	}

	TrackMetric(FlagKey, VariationId);
	TrackExposureEvent(FlagKey, VariationId, bIsExperiment);
	return bResult;
}

FString UToggleMeshSubsystem::GetStringFlag(const FString& FlagKey, const FString& DefaultValue) const
{
	FString Result = DefaultValue;
	FString VariationId = TEXT("");
	bool bIsExperiment = false;

	if (const FToggleMeshFlagState* Found = FlagsCache.Find(FlagKey))
	{
		Result = Found->VariationValue;
		VariationId = Found->VariationId;
		bIsExperiment = Found->bIsExperimentActive;
	}

	TrackMetric(FlagKey, VariationId);
	TrackExposureEvent(FlagKey, VariationId, bIsExperiment);
	return Result;
}

FString UToggleMeshSubsystem::GetJsonFlag(const FString& FlagKey, const FString& DefaultValue) const
{
	FString Result = DefaultValue;
	FString VariationId = TEXT("");
	bool bIsExperiment = false;

	if (const FToggleMeshFlagState* Found = FlagsCache.Find(FlagKey))
	{
		Result = Found->VariationValue;
		VariationId = Found->VariationId;
		bIsExperiment = Found->bIsExperimentActive;
	}

	TrackMetric(FlagKey, VariationId);
	TrackExposureEvent(FlagKey, VariationId, bIsExperiment);
	return Result;
}

void UToggleMeshSubsystem::TrackMetric(const FString& FlagKey, const FString& VariationId) const
{
	const UToggleMeshSettings* Settings = GetDefault<UToggleMeshSettings>();
	if (!Settings->bIsMetricsEnabled || VariationId.IsEmpty()) return;

	if (!MetricsBuffer.Contains(FlagKey))
	{
		if (MetricsBuffer.Num() < Settings->MetricsBufferCapacity)
		{
			MetricsBuffer.Add(FlagKey, FToggleMeshMetricCounts());
		}
	}
	if (MetricsBuffer.Contains(FlagKey))
	{
		int32& Count = MetricsBuffer[FlagKey].VariationCounts.FindOrAdd(VariationId);
		Count++;
	}
}

void UToggleMeshSubsystem::TrackExposureEvent(const FString& FlagKey, const FString& VariationId, bool bIsExperiment) const
{
	const UToggleMeshSettings* Settings = GetDefault<UToggleMeshSettings>();
	if (!bIsExperiment || CurrentUserId.IsEmpty() || VariationId.IsEmpty()) return;

	if (Settings->bIsMetricsEnabled && EventBuffer.Num() < Settings->AnalyticsChannelCapacity)
	{
		TSharedRef<FJsonObject> EventObj = MakeShared<FJsonObject>();
		EventObj->SetNumberField(TEXT("Type"), 0);
		EventObj->SetNumberField(TEXT("Timestamp"), FDateTime::UtcNow().ToUnixTimestamp() * 1000);
		EventObj->SetStringField(TEXT("Identity"), CurrentUserId);
		EventObj->SetStringField(TEXT("FlagKey"), FlagKey);
		EventObj->SetStringField(TEXT("VariationId"), VariationId);

		TSharedRef<FJsonObject> ContextObj = MakeShared<FJsonObject>();
		for (const TPair<FString, FString>& Pair : CurrentContext)
		{
			ContextObj->SetStringField(Pair.Key, Pair.Value);
		}
		EventObj->SetObjectField(TEXT("Properties"), ContextObj);

		EventBuffer.Add(EventObj);
	}
}

void UToggleMeshSubsystem::FetchFlags()
{
	FlushEvents();
	FlushMetrics();

	const UToggleMeshSettings* Settings = GetDefault<UToggleMeshSettings>();
	if (Settings->ClientKey.IsEmpty() || Settings->BaseUrl.IsEmpty())
	{
		return;
	}

	TSharedRef<FJsonObject> PayloadObj = MakeShared<FJsonObject>();
	PayloadObj->SetStringField(TEXT("identity"), CurrentUserId);

	TSharedRef<FJsonObject> ContextObj = MakeShared<FJsonObject>();
	for (const TPair<FString, FString>& Pair : CurrentContext)
	{
		ContextObj->SetStringField(Pair.Key, Pair.Value);
	}
	PayloadObj->SetObjectField(TEXT("context"), ContextObj);

	FString PayloadString;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&PayloadString);
	FJsonSerializer::Serialize(PayloadObj, Writer);

	FString Url = Settings->BaseUrl;
	if (!Url.EndsWith(TEXT("/")))
	{
		Url += TEXT("/");
	}
	Url += TEXT("api/v1/sdk/evaluate");

	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
	Request->OnProcessRequestComplete().BindUObject(this, &UToggleMeshSubsystem::OnFlagsResponse);
	Request->SetURL(Url);
	Request->SetVerb(TEXT("POST"));
	Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));
	Request->SetHeader(TEXT("x-api-key"), Settings->ClientKey);
	Request->SetHeader(TEXT("x-sdk-version"), TEXT("unreal-1.1.0"));
	Request->SetContentAsString(PayloadString);
	Request->ProcessRequest();
}

void UToggleMeshSubsystem::OnFlagsResponse(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bConnectedSuccessfully)
{
	if (bConnectedSuccessfully && Response.IsValid() && Response->GetResponseCode() == 200)
	{
		TArray<TSharedPtr<FJsonValue>> JsonArray;
		TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(Response->GetContentAsString());
		if (FJsonSerializer::Deserialize(Reader, JsonArray))
		{
			FlagsCache.Empty();
			for (const TSharedPtr<FJsonValue>& FlagVal : JsonArray)
			{
				TSharedPtr<FJsonObject> FlagObj = FlagVal->AsObject();
				if (FlagObj.IsValid())
				{
					FString Key;
					FToggleMeshFlagState State;
					if (FlagObj->TryGetStringField(TEXT("key"), Key))
					{
						FlagObj->TryGetStringField(TEXT("variationId"), State.VariationId);
						FlagObj->TryGetStringField(TEXT("variationValue"), State.VariationValue);
						FlagObj->TryGetBoolField(TEXT("isExperimentActive"), State.bIsExperimentActive);
						FlagsCache.Add(Key, State);
					}
				}
			}
			OnFlagsUpdated.Broadcast();
		}
	}
}

void UToggleMeshSubsystem::TrackEvent(const FString& EventName, float Value, bool bHasValue)
{
	const UToggleMeshSettings* Settings = GetDefault<UToggleMeshSettings>();
	if (Settings->ClientKey.IsEmpty() || Settings->BaseUrl.IsEmpty())
	{
		return;
	}

	if (CurrentUserId.IsEmpty() || !Settings->bIsMetricsEnabled)
	{
		return;
	}

	if (EventBuffer.Num() >= Settings->AnalyticsChannelCapacity)
	{
		return;
	}

	TSharedRef<FJsonObject> EventObj = MakeShared<FJsonObject>();
	EventObj->SetNumberField(TEXT("Type"), 1);
	EventObj->SetNumberField(TEXT("Timestamp"), FDateTime::UtcNow().ToUnixTimestamp() * 1000);
	EventObj->SetStringField(TEXT("Identity"), CurrentUserId);
	EventObj->SetStringField(TEXT("EventName"), EventName);
	if (bHasValue)
	{
		EventObj->SetNumberField(TEXT("Value"), Value);
	}

	TSharedRef<FJsonObject> ContextObj = MakeShared<FJsonObject>();
	for (const TPair<FString, FString>& Pair : CurrentContext)
	{
		ContextObj->SetStringField(Pair.Key, Pair.Value);
	}
	EventObj->SetObjectField(TEXT("Properties"), ContextObj);

	EventBuffer.Add(EventObj);
}

void UToggleMeshSubsystem::FlushEvents()
{
	if (EventBuffer.Num() == 0)
	{
		return;
	}

	const UToggleMeshSettings* Settings = GetDefault<UToggleMeshSettings>();
	if (Settings->ClientKey.IsEmpty() || Settings->BaseUrl.IsEmpty())
	{
		return;
	}

	int32 BatchSize = FMath::Min(Settings->MaxBatchSize, EventBuffer.Num());
	TArray<TSharedPtr<FJsonValue>> PayloadArray;
	for (int32 i = 0; i < BatchSize; ++i)
	{
		PayloadArray.Add(MakeShared<FJsonValueObject>(EventBuffer[i]));
	}
	EventBuffer.RemoveAt(0, BatchSize);

	FString PayloadString;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&PayloadString);
	
	TSharedRef<FJsonObject> RootObj = MakeShared<FJsonObject>();
	RootObj->SetArrayField(TEXT("Events"), PayloadArray);
	FJsonSerializer::Serialize(RootObj, Writer);

	FString Url = Settings->BaseUrl;
	if (!Url.EndsWith(TEXT("/")))
	{
		Url += TEXT("/");
	}
	Url += TEXT("api/v1/sdk/events");

	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
	Request->OnProcessRequestComplete().BindUObject(this, &UToggleMeshSubsystem::OnTrackResponse);
	Request->SetURL(Url);
	Request->SetVerb(TEXT("POST"));
	Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));
	Request->SetHeader(TEXT("x-api-key"), Settings->ClientKey);
	Request->SetHeader(TEXT("x-sdk-version"), TEXT("unreal-1.1.0"));
	Request->SetContentAsString(PayloadString);
	Request->ProcessRequest();
}

void UToggleMeshSubsystem::FlushMetrics()
{
	if (MetricsBuffer.Num() == 0)
	{
		return;
	}

	const UToggleMeshSettings* Settings = GetDefault<UToggleMeshSettings>();
	if (Settings->ClientKey.IsEmpty() || Settings->BaseUrl.IsEmpty() || !Settings->bIsMetricsEnabled)
	{
		return;
	}

	TArray<TSharedPtr<FJsonValue>> PayloadArray;
	for (const TPair<FString, FToggleMeshMetricCounts>& Pair : MetricsBuffer)
	{
		if (Pair.Value.VariationCounts.Num() > 0)
		{
			TSharedRef<FJsonObject> MetricObj = MakeShared<FJsonObject>();
			MetricObj->SetStringField(TEXT("Key"), Pair.Key);

			TArray<TSharedPtr<FJsonValue>> VariationsArray;
			for (const TPair<FString, int32>& VarPair : Pair.Value.VariationCounts)
			{
				TSharedRef<FJsonObject> VarObj = MakeShared<FJsonObject>();
				VarObj->SetStringField(TEXT("VariationId"), VarPair.Key);
				VarObj->SetNumberField(TEXT("Count"), VarPair.Value);
				VariationsArray.Add(MakeShared<FJsonValueObject>(VarObj));
			}
			MetricObj->SetArrayField(TEXT("Variations"), VariationsArray);
			PayloadArray.Add(MakeShared<FJsonValueObject>(MetricObj));
		}
	}
	MetricsBuffer.Empty();

	if (PayloadArray.Num() == 0)
	{
		return;
	}

	FString PayloadString;
	TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&PayloadString);
	FJsonSerializer::Serialize(PayloadArray, Writer);

	FString Url = Settings->BaseUrl;
	if (!Url.EndsWith(TEXT("/")))
	{
		Url += TEXT("/");
	}
	Url += TEXT("api/v1/sdk/metrics");

	TSharedRef<IHttpRequest, ESPMode::ThreadSafe> Request = FHttpModule::Get().CreateRequest();
	Request->OnProcessRequestComplete().BindUObject(this, &UToggleMeshSubsystem::OnMetricsResponse);
	Request->SetURL(Url);
	Request->SetVerb(TEXT("POST"));
	Request->SetHeader(TEXT("Content-Type"), TEXT("application/json"));
	Request->SetHeader(TEXT("x-api-key"), Settings->ClientKey);
	Request->SetHeader(TEXT("x-sdk-version"), TEXT("unreal-1.1.0"));
	Request->SetContentAsString(PayloadString);
	Request->ProcessRequest();
}

void UToggleMeshSubsystem::OnMetricsResponse(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bConnectedSuccessfully)
{

}

void UToggleMeshSubsystem::OnTrackResponse(FHttpRequestPtr Request, FHttpResponsePtr Response, bool bConnectedSuccessfully)
{
}
