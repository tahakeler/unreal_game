// HARVEST PANIC - Lobby Yard overlay (Tab): crew list, ready, dinner bell, invite, kick, chat.
#pragma once

#include "CoreMinimal.h"
#include "Frontend/UI/HPWidgets.h"
#include "HPLobbyWidget.generated.h"

class UTextBlock;
class UVerticalBox;
class UEditableTextBox;
class UBorder;

UCLASS()
class HARVESTPANIC_API UHPLobbyWidget : public UHPMenuPanel
{
	GENERATED_BODY()
public:
	void RefreshFromState();
	void ShowStartAnyway(int32 Ready, int32 Total);
	/** True while the chat box has focus (so R/B don't fire while typing). */
	bool IsTyping() const;
	virtual bool HandleBackKey() override;

protected:
	virtual void BuildTree() override;
	virtual void NativeConstruct() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	void SendChat();
	class AHPLobbyPlayerController* GetLobbyPC() const;

	UPROPERTY() TObjectPtr<UVerticalBox> RosterBox;
	UPROPERTY() TObjectPtr<UVerticalBox> ChatBox;
	UPROPERTY() TObjectPtr<UTextBlock> StatusText;
	UPROPERTY() TObjectPtr<UTextBlock> PromptText;
	UPROPERTY() TObjectPtr<UHPButton> ReadyButton;
	UPROPERTY() TObjectPtr<UHPButton> BellButton;
	UPROPERTY() TObjectPtr<UEditableTextBox> ChatEntry;
	float PromptTimeLeft = 0.f;
	float PollTimer = 0.f;
};

/** Always-on tiny hint strip in the lobby when the overlay is hidden. */
UCLASS()
class HARVESTPANIC_API UHPLobbyHintWidget : public UUserWidget
{
	GENERATED_BODY()
protected:
	virtual TSharedRef<SWidget> RebuildWidget() override;
	virtual void NativeTick(const FGeometry& MyGeometry, float InDeltaTime) override;
	UPROPERTY() TObjectPtr<UTextBlock> Hint;
};
