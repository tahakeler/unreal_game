// HARVEST PANIC - settings screen (title + pause). Edits a working copy; Apply saves.
#pragma once

#include "CoreMinimal.h"
#include "Frontend/UI/HPWidgets.h"
#include "HPSettingsWidget.generated.h"

UCLASS()
class HARVESTPANIC_API UHPSettingsWidget : public UHPMenuPanel
{
	GENERATED_BODY()
protected:
	virtual void BuildTree() override;
	virtual void NativeDestruct() override;
	bool bDirty = false;
};
