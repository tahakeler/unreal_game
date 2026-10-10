// Copyright HARVEST PANIC team. All Rights Reserved.
#include "HPCritterAIController.h"
#include "HPCritter.h"
#include "HPCritterPersonality.h"
#include "HPCritterLogic.h"
#include "HPCritterMoverComponent.h"
#include "HPNoiseSubsystem.h"
#include "GameFramework/CharacterMovementComponent.h"
#include "Engine/World.h"
#include "EngineUtils.h"
#include "DrawDebugHelpers.h"
#include "HAL/IConsoleManager.h"

static TAutoConsoleVariable<int32> CVarCritterDebug(
	TEXT("hp.CritterDebug"), 0,
	TEXT("1 = draw critter AI debug (state, threat, alert radius, flee goal, path)."),
	ECVF_Cheat);

namespace HPCritterConst
{
	constexpr float NoiseDecayPerSec = 0.25f;   // critter-ai 4
	constexpr float DamageValue = 0.6f;
	constexpr float DamageDecayPerSec = 0.3f;
	constexpr float FleeQueryInterval = 0.25f;  // critter-ai 3.4 / 7
	constexpr float AlertDecayTime = 3.f;
	constexpr float LureBreakThreat = 0.8f;
	constexpr float LureRangeM = 15.f;
	constexpr float LureLifetime = 10.f;
	constexpr float CorneredPlayerRadiusCm = 400.f;
	constexpr int32 CorneredMinPlayers = 2;
	constexpr float CorneredCostMult = 3.f;
	constexpr float StuckRequery = 3.f;
	constexpr float BrainInterval = 0.1f;       // 10 Hz
}

AHPCritterAIController::AHPCritterAIController()
{
	PrimaryActorTick.bCanEverTick = true;
	bWantsPlayerState = false;
}

AHPCritter* AHPCritterAIController::Critter() const { return Cast<AHPCritter>(GetPawn()); }

const UHPCritterPersonality* AHPCritterAIController::Personality() const
{
	const AHPCritter* C = Critter();
	return C ? C->GetPersonality() : nullptr;
}

double AHPCritterAIController::Now() const { return GetWorld() ? GetWorld()->GetTimeSeconds() : 0.0; }

void AHPCritterAIController::OnPossess(APawn* InPawn)
{
	Super::OnPossess(InPawn);
	if (UHPNoiseSubsystem* Noise = GetWorld()->GetSubsystem<UHPNoiseSubsystem>())
	{
		NoiseHandle = Noise->OnNoise.AddUObject(this, &AHPCritterAIController::HandleNoise);
	}
	IdleWait = FMath::FRandRange(0.5f, 2.5f);
	StateEnterTime = Now();
}

void AHPCritterAIController::OnUnPossess()
{
	if (UHPNoiseSubsystem* Noise = GetWorld() ? GetWorld()->GetSubsystem<UHPNoiseSubsystem>() : nullptr)
	{
		Noise->OnNoise.Remove(NoiseHandle);
	}
	Super::OnUnPossess();
}

void AHPCritterAIController::SetBrainState(ECritterState NewState)
{
	if (AHPCritter* C = Critter())
	{
		if (C->GetCritterState() != NewState)
		{
			C->SetCritterState(NewState);
			StateEnterTime = Now();
			LowThreatTime = 0.f;
		}
	}
}

void AHPCritterAIController::HandleNoise(const FNoiseEvent& Event)
{
	AHPCritter* C = Critter();
	const UHPCritterPersonality* P = Personality();
	if (!C || !P || Event.Instigator == C)
	{
		return;
	}
	const float DistM = FVector::Dist(C->GetActorLocation(), Event.Location) / 100.f;
	if (DistM > Event.Radius)
	{
		return;
	}

	FGameplayTag Tag;
	FHPNoiseTagResponse Resp;
	float Value = 0.f;
	if (P->FindNoiseTagResponse(Event.Tags, Tag, Resp))
	{
		switch (Resp.Response)
		{
		case ECritterNoiseResponse::Lure:
			SetLure(Event.Location, Resp.Strength);
			return; // Lure/Rally tags add no threat (critter-ai 4)
		case ECritterNoiseResponse::Rally:
		case ECritterNoiseResponse::Ignore:
			return;
		case ECritterNoiseResponse::Flee:
			Value = FMath::Max(Resp.Strength * (1.f - DistM / FMath::Max(1.f, Event.Radius)),
				FHPCritterLogic::PerceivedNoise(Event.Loudness, P->NoiseSensitivity, P->GetNoiseSourceMult(Event.Source), DistM, Event.Radius));
			Value = FMath::Max(Value, P->FleeThreshold); // a Flee-tagged noise always sends it running
			break;
		}
	}
	else
	{
		Value = FHPCritterLogic::PerceivedNoise(Event.Loudness, P->NoiseSensitivity, P->GetNoiseSourceMult(Event.Source), DistM, Event.Radius);
	}
	if (Value <= 0.f)
	{
		return;
	}
	for (FHPNoiseThreat& T : NoiseThreats)
	{
		if (T.Instigator.IsValid() && T.Instigator == Event.Instigator)
		{
			T.Value = FMath::Max(T.Value, Value);
			T.Location = Event.Location;
			return;
		}
	}
	FHPNoiseThreat& New = NoiseThreats.AddDefaulted_GetRef();
	New.Location = Event.Location;
	New.Instigator = Event.Instigator;
	New.Value = Value;
}

void AHPCritterAIController::SetLure(const FVector& Location, float Affinity)
{
	bLureActive = true;
	LureLocation = Location;
	LureAffinityValue = Affinity;
	LureExpireTime = Now() + HPCritterConst::LureLifetime;
}

void AHPCritterAIController::NotifyDamaged(AActor* Source)
{
	DamageThreat = HPCritterConst::DamageValue;
	DamageSource = Source;
}

void AHPCritterAIController::EnterStunned(float Seconds)
{
	if (AHPCritter* C = Critter())
	{
		C->GetMover()->Stop();
	}
	SetBrainState(ECritterState::Stunned);
	StateEndTime = Now() + Seconds;
}

void AHPCritterAIController::Scatter(const FVector& FlockCenter, const FVector& ThreatLocation)
{
	AHPCritter* C = Critter();
	if (!C || C->IsCarried())
	{
		return;
	}
	FVector Dir = C->GetActorLocation() - FlockCenter;
	Dir.Z = 0.f;
	if (Dir.SizeSquared() < 100.f)
	{
		Dir = C->GetActorLocation() - ThreatLocation;
		Dir.Z = 0.f;
	}
	ScatterDir = Dir.GetSafeNormal().RotateAngleAxis(FMath::FRandRange(-35.f, 35.f), FVector::UpVector);
	bScatterPending = true;
	C->HomeLocation = FlockCenter; // regroup point
	FHPNoiseThreat& T = NoiseThreats.AddDefaulted_GetRef();
	T.Location = ThreatLocation;
	T.Value = Personality() ? Personality()->FleeThreshold + 0.1f : 0.6f;
	SetBrainState(ECritterState::Flee);
	NextFleeQueryTime = 0.0;
}

float AHPCritterAIController::EvaluateThreat(float Dt)
{
	AHPCritter* C = Critter();
	const UHPCritterPersonality* P = Personality();
	ThreatLocs.Reset();
	ThreatWeights.Reset();
	PlayerLocs.Reset();
	for (FHPNoiseThreat& T : NoiseThreats)
	{
		T.Value = FHPCritterLogic::Decay(T.Value, HPCritterConst::NoiseDecayPerSec, Dt);
	}
	NoiseThreats.RemoveAll([](const FHPNoiseThreat& T) { return T.Value <= 0.f; });
	DamageThreat = FHPCritterLogic::Decay(DamageThreat, HPCritterConst::DamageDecayPerSec, Dt);

	const FVector MyLoc = C->GetActorLocation();
	const FVector Fwd = C->GetActorForwardVector().GetSafeNormal2D();
	const float HalfConeCos = FMath::Cos(FMath::DegreesToRadians(FMath::Min(P->SightCone, 360.f) * 0.5f));
	TArray<bool> NoiseUsed;
	NoiseUsed.SetNumZeroed(NoiseThreats.Num());

	for (TActorIterator<APawn> It(GetWorld()); It; ++It)
	{
		APawn* Other = *It;
		if (!Other || Other == C || !Other->IsPlayerControlled())
		{
			continue;
		}
		const FVector OL = Other->GetActorLocation();
		PlayerLocs.Add(OL);
		const float DistM = FVector::Dist(MyLoc, OL) / 100.f;
		const bool bSprinting = Other->GetVelocity().Size2D() > P->SprintDetectSpeed;
		bool bInSight = false;
		if (DistM < P->AlertRadius)
		{
			const FVector To = (OL - MyLoc).GetSafeNormal2D();
			if (FVector::DotProduct(Fwd, To) >= HalfConeCos)
			{
				bInSight = LineOfSightTo(Other);
			}
		}
		float N = 0.f;
		for (int32 i = 0; i < NoiseThreats.Num(); ++i)
		{
			if (NoiseThreats[i].Instigator == Other)
			{
				N = FMath::Max(N, NoiseThreats[i].Value);
				NoiseUsed[i] = true;
			}
		}
		const float D = (DamageSource == Other) ? DamageThreat : 0.f;
		const float T = FHPCritterLogic::SourceThreat(DistM, P->AlertRadius, bSprinting, bInSight, N, D, P->ThreatWeights);
		if (T > 0.f)
		{
			ThreatLocs.Add(OL);
			ThreatWeights.Add(T);
		}
	}
	for (int32 i = 0; i < NoiseThreats.Num(); ++i)
	{
		if (!NoiseUsed[i])
		{
			ThreatLocs.Add(NoiseThreats[i].Location);
			ThreatWeights.Add(FMath::Clamp(NoiseThreats[i].Value, 0.f, 1.f));
		}
	}
	return FHPCritterLogic::TotalThreat(ThreatWeights, P->ThreatWeights.OtherSources);
}

int32 AHPCritterAIController::CountThreatsWithin(float RadiusCm) const
{
	const AHPCritter* C = Critter();
	int32 N = 0;
	for (const FVector& L : PlayerLocs)
	{
		N += FVector::Dist2D(L, C->GetActorLocation()) <= RadiusCm ? 1 : 0;
	}
	return N;
}

bool AHPCritterAIController::PickFleePoint(bool& bOutCornered)
{
	AHPCritter* C = Critter();
	const UHPCritterPersonality* P = Personality();
	bOutCornered = false;
	const FVector From = C->GetActorLocation();
	bool bAmbiguous = false;
	FVector Away = bScatterPending ? ScatterDir : FHPCritterLogic::FleeDirection(From, ThreatLocs, ThreatWeights, &bAmbiguous);
	bScatterPending = false;
	if (Away.IsNearlyZero())
	{
		Away = C->GetActorForwardVector().GetSafeNormal2D();
	}
	const float Dist = P->FleeDistance * 100.f;

	// EQS-free ring sampling: 12 headings, prefer those aligned with Away.
	float BestScore = -FLT_MAX;
	FVector Best = FVector::ZeroVector;
	float BestDot = -1.f;
	for (int32 i = 0; i < 12; ++i)
	{
		const float Angle = (i % 2 == 0 ? 1.f : -1.f) * 30.f * ((i + 1) / 2);
		const FVector Dir = Away.RotateAngleAxis(Angle, FVector::UpVector);
		FVector Point;
		if (!C->GetMover()->IsValidDestination(From, From + Dir * Dist, Point)
			&& !C->GetMover()->IsValidDestination(From, From + Dir * Dist * 0.5f, Point))
		{
			continue;
		}
		const float Dot = FVector::DotProduct(Dir, Away);
		const float Score = Dot + FMath::FRandRange(0.f, 0.15f);
		if (Score > BestScore)
		{
			BestScore = Score;
			Best = Point;
			BestDot = Dot;
		}
	}

	const bool bCrowded = CountThreatsWithin(HPCritterConst::CorneredPlayerRadiusCm) >= HPCritterConst::CorneredMinPlayers;
	if (BestScore == -FLT_MAX)
	{
		bOutCornered = bCrowded || bAmbiguous;
		return false;
	}
	if (bCrowded)
	{
		const float Straight = FVector::Dist(From, Best);
		const float Cost = C->GetMover()->RouteCost(From, Best);
		if (BestDot < 0.3f || Cost < 0.f || Cost > HPCritterConst::CorneredCostMult * Straight || bAmbiguous)
		{
			bOutCornered = true;
			return false;
		}
	}
	FleeGoal = Best;
	return C->GetMover()->MoveTo(Best, P->SprintSpeed, 80.f);
}

bool AHPCritterAIController::PickWanderPoint()
{
	AHPCritter* C = Critter();
	const UHPCritterPersonality* P = Personality();
	const float R = P->WanderRadius * 100.f;
	for (int32 Try = 0; Try < 4; ++Try)
	{
		const FVector2D Rand = FMath::RandPointInCircle(R);
		FVector Point;
		if (C->GetMover()->IsValidDestination(C->GetActorLocation(), C->HomeLocation + FVector(Rand, 0.f), Point))
		{
			return C->GetMover()->MoveTo(Point, P->MoveSpeed * 0.6f, 50.f);
		}
	}
	return false;
}

void AHPCritterAIController::TriggerFlockScatter()
{
	AHPCritter* C = Critter();
	const UHPCritterPersonality* P = Personality();
	if (P->HerdMode != ECritterHerdMode::Flock)
	{
		return;
	}
	TArray<AHPCritter*> Mates;
	FVector Center = C->GetActorLocation();
	const float R = P->FlockRadius * 100.f;
	for (TActorIterator<AHPCritter> It(GetWorld()); It; ++It)
	{
		AHPCritter* O = *It;
		if (O != C && O->GetPersonality() && O->GetPersonality()->Id == P->Id && FVector::Dist(O->GetActorLocation(), C->GetActorLocation()) < R)
		{
			Mates.Add(O);
			Center += O->GetActorLocation();
		}
	}
	Center /= (Mates.Num() + 1);
	const FVector ThreatLoc = ThreatLocs.Num() ? ThreatLocs[0] : Center;
	for (AHPCritter* M : Mates)
	{
		AHPCritterAIController* AI = Cast<AHPCritterAIController>(M->GetController());
		const ECritterState S = M->GetCritterState();
		if (AI && S != ECritterState::Flee && S != ECritterState::Carried && S != ECritterState::Stunned)
		{
			AI->Scatter(Center, ThreatLoc);
		}
	}
	C->HomeLocation = Center;
	FVector Dir = C->GetActorLocation() - Center;
	ScatterDir = (Dir.SizeSquared2D() > 100.f ? Dir.GetSafeNormal2D() : (C->GetActorLocation() - ThreatLoc).GetSafeNormal2D());
	bScatterPending = true;
}

void AHPCritterAIController::StartCornered()
{
	const UHPCritterPersonality* P = Personality();
	AHPCritter* C = Critter();
	SetBrainState(ECritterState::Cornered);
	StateEndTime = Now() + FMath::FRandRange(2.f, 5.f);
	C->GetMover()->Stop();
	switch (P->CorneredResponse)
	{
	case ECritterCorneredResponse::Juke:
	{
		// Dash through the widest gap between threats (ignores walls; that's the point of a juke).
		float BestGap = -1.f;
		FVector BestDir = C->GetActorForwardVector();
		for (int32 i = 0; i < 16; ++i)
		{
			const FVector Dir = FVector::ForwardVector.RotateAngleAxis(i * 22.5f, FVector::UpVector);
			float MinAngle = 180.f;
			for (const FVector& L : ThreatLocs)
			{
				const FVector To = (L - C->GetActorLocation()).GetSafeNormal2D();
				MinAngle = FMath::Min(MinAngle, FMath::RadiansToDegrees(FMath::Acos(FMath::Clamp(FVector::DotProduct(Dir, To), -1.f, 1.f))));
			}
			FVector Unused;
			const bool bOpen = C->GetMover()->IsValidDestination(C->GetActorLocation(), C->GetActorLocation() + Dir * 300.f, Unused);
			const float Gap = MinAngle + (bOpen ? 45.f : 0.f);
			if (Gap > BestGap)
			{
				BestGap = Gap;
				BestDir = Dir;
			}
		}
		const float JukeSpeed = P->SprintSpeed * (1.f + 0.06f * P->Agility);
		C->GetMover()->MoveTo(C->GetActorLocation() + BestDir * 500.f, JukeSpeed, 60.f);
		StateEndTime = Now() + 0.8f;
		break;
	}
	case ECritterCorneredResponse::Charge:
		if (ThreatLocs.Num())
		{
			C->GetMover()->MoveTo(ThreatLocs[0], P->SprintSpeed, 30.f);
		}
		break;
	default: // Freeze / PlayDead / Burrow / Teleport: hold still (Burrow/Teleport not in VS)
		break;
	}
}

void AHPCritterAIController::TickCornered()
{
	if (Now() >= StateEndTime)
	{
		SetBrainState(ECritterState::Flee);
		NextFleeQueryTime = 0.0;
	}
}

void AHPCritterAIController::Tick(float DeltaSeconds)
{
	Super::Tick(DeltaSeconds);
	AHPCritter* C = Critter();
	const UHPCritterPersonality* P = Personality();
	if (!C || !P || !HasAuthority())
	{
		return;
	}
	AccumDt += DeltaSeconds;
	if (AccumDt < HPCritterConst::BrainInterval)
	{
		return;
	}
	const float Dt = AccumDt;
	AccumDt = 0.f;

	CurrentThreat = EvaluateThreat(Dt);
	const float T = CurrentThreat;
	const double TimeNow = Now();
	const double InState = TimeNow - StateEnterTime;
	UHPCritterMoverComponent* Mover = C->GetMover();
	if (bLureActive && TimeNow > LureExpireTime && C->GetCritterState() != ECritterState::Lured)
	{
		bLureActive = false;
	}
	const float LureDistM = FVector::Dist(C->GetActorLocation(), LureLocation) / 100.f;
	const bool bLureWins = bLureActive && FHPCritterLogic::LureWins(LureAffinityValue, LureDistM, T, HPCritterConst::LureRangeM);

	switch (C->GetCritterState())
	{
	case ECritterState::Carried:
	case ECritterState::Escaped:
		break;

	case ECritterState::Stunned:
		if (TimeNow >= StateEndTime && C->GetCharacterMovement()->IsMovingOnGround())
		{
			SetBrainState(T >= P->AlertThreshold ? ECritterState::Flee : ECritterState::Calm);
			NextFleeQueryTime = 0.0;
		}
		break;

	case ECritterState::Idle:
	case ECritterState::Wander:
	case ECritterState::Calm:
		if (bLureWins)
		{
			SetBrainState(ECritterState::Lured);
			Mover->MoveTo(LureLocation, P->SprintSpeed * 0.8f, 90.f);
			EatTimer = 0.f;
			break;
		}
		if (T >= P->AlertThreshold)
		{
			Mover->Stop();
			SetBrainState(ECritterState::Alert);
			break;
		}
		if (C->GetCritterState() == ECritterState::Calm && InState >= P->CalmTime)
		{
			SetBrainState(ECritterState::Idle);
		}
		if (!Mover->IsMoving())
		{
			if (C->GetCritterState() == ECritterState::Wander)
			{
				SetBrainState(ECritterState::Idle);
				IdleWait = FMath::FRandRange(1.f, 3.f);
			}
			else if (C->GetCritterState() == ECritterState::Idle && InState >= IdleWait && PickWanderPoint())
			{
				SetBrainState(ECritterState::Wander);
			}
		}
		break;

	case ECritterState::Alert:
	{
		if (ThreatLocs.Num())
		{
			const FVector To = (ThreatLocs[0] - C->GetActorLocation()).GetSafeNormal2D();
			C->SetActorRotation(FMath::RInterpTo(C->GetActorRotation(), To.Rotation(), Dt, 10.f));
		}
		if (bLureWins)
		{
			SetBrainState(ECritterState::Lured);
			Mover->MoveTo(LureLocation, P->SprintSpeed * 0.8f, 90.f);
			break;
		}
		if (InState >= P->AlertTellTime && T >= P->FleeThreshold)
		{
			SetBrainState(ECritterState::Flee);
			NextFleeQueryTime = 0.0;
			TriggerFlockScatter();
			break;
		}
		LowThreatTime = (T < P->AlertThreshold) ? LowThreatTime + Dt : 0.f;
		if (LowThreatTime >= HPCritterConst::AlertDecayTime)
		{
			SetBrainState(ECritterState::Idle);
		}
		break;
	}

	case ECritterState::Flee:
	{
		if (bLureWins)
		{
			SetBrainState(ECritterState::Lured);
			Mover->MoveTo(LureLocation, P->SprintSpeed * 0.8f, 90.f);
			break;
		}
		LowThreatTime = (T < P->CalmThreshold) ? LowThreatTime + Dt : 0.f;
		if (LowThreatTime >= P->CalmTime)
		{
			Mover->Stop();
			SetBrainState(ECritterState::Calm);
			break;
		}
		const bool bNeedQuery = !Mover->IsMoving() || Mover->GetStuckTime() > HPCritterConst::StuckRequery
			|| (T >= P->CalmThreshold && FVector::Dist2D(C->GetActorLocation(), FleeGoal) < 250.f);
		if (TimeNow >= NextFleeQueryTime && (bNeedQuery || bScatterPending))
		{
			NextFleeQueryTime = TimeNow + HPCritterConst::FleeQueryInterval;
			bool bCornered = false;
			if (!PickFleePoint(bCornered) && bCornered)
			{
				StartCornered();
			}
		}
		break;
	}

	case ECritterState::Lured:
	{
		if (T > HPCritterConst::LureBreakThreat || !bLureActive)
		{
			bLureActive = false;
			SetBrainState(T >= P->FleeThreshold ? ECritterState::Flee : ECritterState::Idle);
			NextFleeQueryTime = 0.0;
			break;
		}
		if (LureDistM * 100.f < 120.f)
		{
			Mover->Stop();
			EatTimer += Dt;
			if (EatTimer >= P->EatTime)
			{
				bLureActive = false;
				SetBrainState(ECritterState::Calm);
			}
		}
		else if (!Mover->IsMoving())
		{
			Mover->MoveTo(LureLocation, P->SprintSpeed * 0.8f, 90.f);
		}
		break;
	}

	case ECritterState::Cornered:
		TickCornered();
		break;
	}

	if (CVarCritterDebug.GetValueOnGameThread() > 0)
	{
		DrawDebug();
	}
}

void AHPCritterAIController::DrawDebug() const
{
	const AHPCritter* C = Critter();
	const UHPCritterPersonality* P = Personality();
	UWorld* W = GetWorld();
	const FVector L = C->GetActorLocation();
	const float Life = HPCritterConst::BrainInterval + 0.02f;
	FColor Col = FColor::Green;
	switch (C->GetCritterState())
	{
	case ECritterState::Alert: Col = FColor::Yellow; break;
	case ECritterState::Flee: Col = FColor::Red; break;
	case ECritterState::Lured: Col = FColor::Cyan; break;
	case ECritterState::Cornered: Col = FColor::Magenta; break;
	case ECritterState::Carried: Col = FColor::Blue; break;
	default: break;
	}
	DrawDebugString(W, L + FVector(0, 0, 90.f), FString::Printf(TEXT("%s T=%.2f"), LexCritterState(C->GetCritterState()), CurrentThreat), nullptr, Col, Life, true);
	DrawDebugCircle(W, L - FVector(0, 0, C->GetSimpleCollisionHalfHeight() - 2.f), P->AlertRadius * 100.f, 32, Col, false, Life, 0, 2.f, FVector(1, 0, 0), FVector(0, 1, 0), false);
	if (C->GetMover()->IsMoving())
	{
		const TArray<FVector>& Pts = C->GetMover()->GetPathPoints();
		for (int32 i = 1; i < Pts.Num(); ++i)
		{
			DrawDebugLine(W, Pts[i - 1], Pts[i], Col, false, Life, 0, 3.f);
		}
		DrawDebugSphere(W, C->GetMover()->GetGoal(), 20.f, 8, Col, false, Life);
	}
	for (int32 i = 0; i < ThreatLocs.Num(); ++i)
	{
		DrawDebugLine(W, L, ThreatLocs[i], FColor::Orange, false, Life, 0, ThreatWeights[i] * 4.f);
	}
	if (bLureActive)
	{
		DrawDebugSphere(W, LureLocation, 40.f, 8, FColor::Cyan, false, Life);
	}
}
