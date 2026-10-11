// HARVEST PANIC - title / play / browser / connecting / error / customize / pause screens.
#pragma once

#include "CoreMinimal.h"
#include "Frontend/UI/HPWidgets.h"
#include "HPMenuScreens.generated.h"

class UTextBlock;
class UVerticalBox;
class UEditableTextBox;
struct FHPServerEntry;

UCLASS()
class HARVESTPANIC_API UHPTitleWidget : public UHPMenuPanel
{
	GENERATED_BODY()
protected:
	virtual void BuildTree() override;
};

UCLASS()
class HARVESTPANIC_API UHPPlayChoiceWidget : public UHPMenuPanel
{
	GENERATED_BODY()
protected:
	virtual void BuildTree() override;
	void StartHost(bool bLAN);
};

UCLASS()
class HARVESTPANIC_API UHPServerBrowserWidget : public UHPMenuPanel
{
	GENERATED_BODY()
protected:
	virtual void BuildTree() override;
	virtual void NativeConstruct() override;
	virtual void NativeDestruct() override;
	void Refresh();
	void HandleResults(const TArray<FHPServerEntry>& Entries);

	UPROPERTY() TObjectPtr<UVerticalBox> ListBox;
	UPROPERTY() TObjectPtr<UTextBlock> StatusText;
	UPROPERTY() TObjectPtr<UEditableTextBox> AddressBox;
	bool bLAN = true;
	FDelegateHandle ResultsHandle;
};

UCLASS()
class HARVESTPANIC_API UHPConnectingWidget : public UHPMenuPanel
{
	GENERATED_BODY()
protected:
	virtual void BuildTree() override;
	virtual void NativeConstruct() override;
	virtual void NativeDestruct() override;
	void HandleResult(bool bOk, const FText& Error);
	FDelegateHandle HostHandle, JoinHandle;
};

UCLASS()
class HARVESTPANIC_API UHPErrorPopupWidget : public UHPMenuPanel
{
	GENERATED_BODY()
public:
	FText Message;
protected:
	virtual void BuildTree() override;
};

/** Hosts the character-customization widget if that module is merged, otherwise "Coming soon". */
UCLASS()
class HARVESTPANIC_API UHPCustomizeHostWidget : public UHPMenuPanel
{
	GENERATED_BODY()
protected:
	virtual void BuildTree() override;
};

/** In-game / lobby overlay. Never pauses the world. */
UCLASS()
class HARVESTPANIC_API UHPPauseWidget : public UHPMenuPanel
{
	GENERATED_BODY()
protected:
	virtual void BuildTree() override;
};
