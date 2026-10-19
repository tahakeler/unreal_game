# Unity Engine — Version Reference

| Field | Value |
|-------|-------|
| **Engine Version** | Unity 6.3 LTS |
| **Pinned Editor** | `6000.3.26f1` (Apple Silicon) at `/Applications/Unity/Hub/Editor/6000.3.26f1` |
| **Installed at pin time** | Installing at pin time (2026-10-19) — confirm with `Unity -version` before trusting version-qualified claims. |
| **Release Date** | December 2025 (6.3 LTS line); `26f1` is a later patch release on that line |
| **Render Pipeline** | URP |
| **Project Pinned** | 2026-10-19 |
| **Last Docs Verified** | 2026-10-19 (release-level facts only — module docs below date from 2026-02-13) |
| **LLM Knowledge Cutoff** | mid-2026 (current model); older agents/models may stop at ~May 2025 |

## Knowledge Gap Warning

The current model's training likely covers Unity 6.0–6.3 at a release-notes level,
but patch-level behaviour of `6000.3.x` and package versions shipped with it
(URP 17.3, Netcode for GameObjects 2.x, Unity Transport 2.x) must be verified
against the editor's Package Manager and the docs links below. Older models may
know only up to ~2022 LTS. Always cross-reference this directory before
suggesting Unity API calls, and prefer the docs for `6000.3` over recall.

## Installed-Version Gap Warning

This reference can sit **ahead of or behind the installed editor**. Check the
`Installed at pin time` row above before trusting a version-qualified claim.

## Version Timeline

| Version | Release | Risk Level | Key Theme |
|---------|---------|------------|-----------|
| 6.0 LTS (6000.0) | Oct 2024 | LOW–MEDIUM | Unity 6 rebrand; Render Graph in URP, GPU Resident Drawer, Entities 1.3 |
| 6.1 (6000.1) | Apr 2025 | MEDIUM | First "update release" (supported until 6.2) |
| 6.2 (6000.2) | 2025 (date not verified here) | MEDIUM | Update release |
| 6.3 LTS (6000.3) | Dec 2025 | HIGH | First LTS since 6.0 — this project's pin |

6.0 and 6.1 dates are sourced below; the 6.2 date was not found and is
deliberately left unstated rather than recalled.

## Unity 6.3 LTS — Changes Relevant to HARVEST PANIC

- **Shader Graph**: custom lighting, terrain shader support, 8 UV sets, template
  browser; **Shader Build Settings** strip/convert keywords to cut compile time.
- **Accessibility**: native screen-reader APIs across Windows, macOS, Android, iOS.
- **Platform Toolkit**: unified API for accounts, save data, achievements across
  platforms (evaluate vs. Steamworks for our Steam-only ship before adopting).
- **Editor**: customizable toolbar presets; legacy Animation evaluation up to ~30% faster.
- **2D (not used by this project)**: Box2D v3 low-level 2D physics, 3D renderers in 2D URP.
- **Support**: LTS — 2 years of support (3 for Enterprise/Industry).
- **Upgrade**: read the "Upgrade to Unity 6.3 LTS" guide before moving packages.

Carried from Unity 6.0+ and still load-bearing here: URP uses **Render Graph**
for custom render passes (write `ScriptableRenderPass.RecordRenderGraph`, not the
legacy `Execute` path); the **Input System** package is the default input path;
UI Toolkit is production-ready for runtime UI; Netcode for GameObjects is the
first-party GameObject netcode.

## Verified Sources

- New in Unity 6.3 LTS (Unity docs): https://docs.unity.com/en-us/engine/6000.5/manual/whats-new/unity63
- Unity 6.3 LTS announcement: https://unity.com/blog/unity-6-3-lts-is-now-available
- CG Channel, "Unity 6.3 LTS is out" (2025-12-05): https://www.cgchannel.com/2025/12/unity-6-3-lts-is-out-see-5-key-features-for-cg-artists/
- GameFromScratch, "Unity 6.3 Released": https://gamefromscratch.com/unity-6-3-released/
- Unity 6.1 announcement (2025-04-24): https://unity.com/blog/unity-6-1-is-now-available
- Unity 6 release: https://unity.com/releases/unity-6
- C# API reference: https://docs.unity3d.com/6000.3/Documentation/ScriptReference/index.html
