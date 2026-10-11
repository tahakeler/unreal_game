// HARVEST PANIC - command-line hooks for headless QA (screenshots via -RenderOffscreen, auto-quit).
#pragma once

#include "CoreMinimal.h"

class APlayerController;

namespace HPFrontendTestHooks
{
	/** -HPShot=<abs path.png> -HPShotDelay=<s>: captures the game viewport incl. UI, then quits 2 s later.
	 *  -HPQuitAfter=<s>: quits after N seconds (LAN tests). */
	void Arm(APlayerController* PC);
}
