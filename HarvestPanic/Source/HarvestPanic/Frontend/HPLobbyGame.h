// HARVEST PANIC - Lobby Yard rules: roster, ready-up (porch or R), dinner bell, chat, kick, start.
#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "GameFramework/Actor.h"
#include "HPGameState.h"
#include "HPPlayerState.h"
#include "HarvestPanicPlayerController.h"
#include "HPLobbyGame.generated.h"

class UBoxComponent;
class UStaticMeshComponent;
class UTextRenderComponent;
class UPointLightComponent;

UCLASS()
class HARVESTPANIC_API AHPLobbyPlayerState : public AHPPlayerState
{
	GENERATED_BODY()
public:
	UPROPERTY(ReplicatedUsing=OnRep_Ready, BlueprintReadOnly, Category="Lobby") bool bReady = false;
	UPROPERTY(Replicated, BlueprintReadOnly, Category="Lobby") bool bOnPorch = false;

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& Out) const override;
	virtual void BeginPlay() override;
	virtual void OnRep_PlayerName() override;
	UFUNCTION() void OnRep_Ready();
	/** True for the listen-server host's player state (the only one with HasAuthority on its own PC). */
	bool IsHost() const;
	UPROPERTY(Replicated) bool bIsHost = false;
};

UCLASS()
class HARVESTPANIC_API AHPLobbyGameState : public AHPGameState
{
	GENERATED_BODY()
public:
	static constexpr int32 MaxChatLines = 6;
	UPROPERTY(ReplicatedUsing=OnRep_Lobby) TArray<FString> ChatLines;
	/** 0 = no countdown; otherwise seconds left (3,2,1). */
	UPROPERTY(ReplicatedUsing=OnRep_Lobby) int32 CountdownSeconds = 0;

	virtual void GetLifetimeReplicatedProps(TArray<FLifetimeProperty>& Out) const override;
	virtual void AddPlayerState(APlayerState* PS) override;
	virtual void RemovePlayerState(APlayerState* PS) override;
	UFUNCTION() void OnRep_Lobby();

	void AddChat(const FString& Line);
	void GetReadyCounts(int32& OutReady, int32& OutTotal) const;
	/** Pokes local UI on this machine (clients via OnRep, server directly). */
	void NotifyLocalUI() const;
};

UCLASS()
class HARVESTPANIC_API AHPLobbyGameMode : public AGameModeBase
{
	GENERATED_BODY()
public:
	AHPLobbyGameMode();

	/** Tuning knobs from frontend-lobby GDD. */
	UPROPERTY(EditDefaultsOnly, Category="Lobby") float ReadyHoldTime = 1.f;
	UPROPERTY(EditDefaultsOnly, Category="Lobby") int32 StartCountdown = 3;
	UPROPERTY(EditDefaultsOnly, Category="Lobby") float StartAnywayWindow = 3.f;

	virtual void PreLogin(const FString& Options, const FString& Address, const FUniqueNetIdRepl& UniqueId, FString& ErrorMessage) override;
	virtual void PostLogin(APlayerController* NewPlayer) override;
	virtual void Logout(AController* Exiting) override;

	void SetReady(APlayerController* PC, bool bReady);
	void RingBell(APlayerController* Ringer);
	void Chat(APlayerController* From, const FString& Text);
	void Kick(APlayerController* Requester, APlayerState* Target);
	void CancelCountdown(bool bSad);
	void StartDay();

protected:
	void TickCountdown();
	AHPLobbyGameState* LobbyGS() const;
	FTimerHandle CountdownTimer;
	double StartAnywayArmedAt = -100.0;
};

UCLASS()
class HARVESTPANIC_API AHPLobbyPlayerController : public AHarvestPanicPlayerController
{
	GENERATED_BODY()
public:
	UFUNCTION(Server, Reliable) void ServerSetReady(bool bNewReady);
	UFUNCTION(Server, Reliable) void ServerRingBell();
	UFUNCTION(Server, Reliable) void ServerChat(const FString& Text);
	UFUNCTION(Server, Reliable) void ServerKick(APlayerState* Target);
	UFUNCTION(Server, Reliable) void ServerSetDisplayName(const FString& Name);
	UFUNCTION(Client, Reliable) void ClientStartAnywayPrompt(int32 Ready, int32 Total);

	void ToggleReady();
	virtual void ClientWasKicked_Implementation(const FText& KickReason) override;

protected:
	virtual void BeginPlay() override;
	virtual void SetupInputComponent() override;
	void OnPausePressed();
	void OnGamepadBack();
	void OnTabPressed();
	void OnBellPressed();
	void AutoTest();
};

/** Porch ready zone: stand here for ReadyHoldTime to ready up; step off to un-ready. Server-side. */
UCLASS()
class HARVESTPANIC_API AHPPorchReadyZone : public AActor
{
	GENERATED_BODY()
public:
	AHPPorchReadyZone();
	virtual void Tick(float DeltaSeconds) override;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UBoxComponent> Zone;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UTextRenderComponent> Sign;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UPointLightComponent> PorchLight;
private:
	TMap<TWeakObjectPtr<APlayerState>, float> TimeOnPorch;
};

/** Dinner bell: host presses B (or the lobby 'Ring the dinner bell' button) to start. Mostly a sign + mesh. */
UCLASS()
class HARVESTPANIC_API AHPDinnerBell : public AActor
{
	GENERATED_BODY()
public:
	AHPDinnerBell();
	UPROPERTY(VisibleAnywhere) TObjectPtr<UStaticMeshComponent> Mesh;
	UPROPERTY(VisibleAnywhere) TObjectPtr<UTextRenderComponent> Sign;
};
