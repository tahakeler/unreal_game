# ADR: Proximity Voice Chat (Steam voice first, ADPCM fallback, text last)

> **Status**: Accepted for early beta (2026-10-21) · **Author**: network-programmer · **Branch**: `feature/voice-chat`
> **Resolves**: GDD 17.4.6 / 17.12 item 1 ("Proximity voice provider - ADR before Alpha"), beta DoD item 7.
> **Design sources**: `design/gdd/proximity-voice.md`, `design/HARVEST-PANIC-GDD.md` 12.4, 12.5 (Megaphone), 12.8 (mute), 17.4.4 (budget).

## 1. Context
HARVEST PANIC is 2-4 player first-person co-op on NGO (host = listen server) over the Facepunch Steam transport
(ship) or Unity Transport (LAN/dev). The GDD wants 3D positional voice whose loudness also feeds the Noise Event
system. Candidates were Steam Voice through Facepunch (no new dependency; we place and compress audio ourselves)
and Unity Vivox (positional channels built in; an extra service, SDK and account). The prototype-era GDD text that
names EOS RTC and UE VoIP talkers is superseded by this ADR.

## 2. Decision
One voice pipeline with three backends chosen per session. Every backend is carried as **NGO custom named
messages** on `NetworkDelivery.Unreliable` through the **host relay** (no peer-to-peer):

| Priority | When | Capture | Codec on the wire | Payload bitrate per talker |
|---|---|---|---|---|
| 1. **Steam voice** | session transport is Steam (`ISessionPlatform.ActiveTransport`) and `SteamClient.IsValid` | `SteamUser.VoiceRecord` + `ReadVoiceData` | Steam's compressed voice; receivers `DecompressVoice` at 16 kHz | ~10-20 kbps |
| 2. **Microphone (PCM)** | LAN / Unity Transport, Steam voice unavailable or failing, or `-hpVoicePcm` | Unity `Microphone`, 16 kHz mono | **IMA ADPCM 3-bit at 8 kHz** (`VoiceCodec.Adpcm3`) | 24 kbps payload, 26.2 kbps on the wire |
| 3. **Text + pings** | no mic, mic muted, voice broken | - | existing lobby chat (`ISessionSocial`), Holler, Point ping | - |

* **Why not mu-law**: G.711 mu-law is 8 bits/sample = 64 kbps at 8 kHz and 128 kbps at 16 kHz, 2-4x the 32 kbps
  budget. IMA ADPCM is the same family (simple, no dependency, no licence) at 3-4 bits/sample. 3-bit is the default
  everywhere so the budget holds on every link; 4-bit (`VoiceConfig.PcmCodec = Adpcm4`, 34-36 kbps) stays available
  as a LAN-only quality option and is **not** used by default.
* **Why 8 kHz on the wire**: capture is 16 kHz mono (as briefed), then a 15-tap windowed-sinc low-pass (3.5 kHz) and
  2:1 decimation; receivers interpolate back to 16 kHz. Telephone band (300-3400 Hz) keeps speech intelligible at
  this bitrate; a wideband codec would need Opus (native plugin, licence review) and is the upgrade path.
* **Why not Vivox**: an extra account/service and SDK for a 4-player game whose Steam dependency already exists.
* **Why the host relay rather than Steam P2P per pair**: range culling, mute lists and the voice Noise Event all need
  the server; with <= 4 players the host upload stays small (section 5).

## 3. Pipeline
```
owner client                              host (server)                           listening client
mic / Steam -> 40 ms frame -> loudness    HP_VoiceUp (unreliable)                  HP_VoiceDown (unreliable)
 -> open-mic VAD or push-to-talk (V)  --> sender id from transport, size and  --> drop if muted locally
 -> encode (Steam / ADPCM 3-bit)          token-bucket checks; voice Noise Event     -> jitter buffer (80 ms prefill)
                                          (loudness >= 0.35, 1 per 0.5 s);           -> decode -> loss concealment
                                          per listener: skip self, skip if muted,    -> 16 kHz playout ring (<= 240 ms)
                                          skip beyond range + 2 m; plays it for      -> 3D AudioSource at speaker head
                                          the host's own player without a send       -> "<)))" marker over the name tag
```
* Code: `Assets/Scripts/Voice/` (asmdef `HarvestPanic.Voice`: Core, Netcode, Collections, Input System, Facepunch
  transport; `Steamworks` comes from the auto-referenced Facepunch.Steamworks DLLs). `Logic/` is pure C#
  (EditMode-tested); `Runtime/` holds `VoiceChatService`, `VoiceSpeakerPlayback`, `SteamVoice`, the PCM sources,
  `VoiceHeads` and `VoiceSpeakingIndicator`. No other team's assembly is referenced.
* `VoiceChatService` is one instance per NetworkManager: the primary one is auto-created after the first scene
  loads, follows `NetworkManager.Singleton` and registers `IVoiceChat`; PlayMode tests create one per in-process
  host/client manager. Handlers are (re)registered whenever the manager's `CustomMessagingManager` changes, so it
  survives Leave/Host/Join cycles.
* Contract: `HarvestPanic.Core.IVoiceChat` (add-only, `Services`) lets UI mute players, switch mode/threshold and
  show speaking state without referencing the Voice assembly.
* Heads: player object child `HeadPivot` (Player prefab), else `IPlayerHooks.HeadAttachPoint`, else the root
  (`PlayerHeadLocator`, works on clients via `SpawnManager.GetPlayerNetworkObjects`). If a head is unknown (player
  object not spawned yet) the frame is still delivered and plays **2D at 35 %** until the head appears.
* Capture is behind `IVoicePcmSource` / `IVoiceEncoder`. **The real microphone is never opened in batchmode** (tests,
  headless two-process runs) unless `-hpVoiceMic` is passed, and only the primary instance may own it.
  Dev flags: `-hpVoiceSynth` (330 Hz test tone instead of the mic), `-hpVoicePcm` (skip Steam voice), `-hpVoiceOff`.

## 4. Wire format (versioned, `VoicePacket.Version = 1`, little endian)
| Message | Delivery | Layout |
|---|---|---|
| `HP_VoiceUp` client->host | Unreliable | `ver u8, codec u8, seq u16, flags u8, loudness u8, len u16, payload` (8 B header) |
| `HP_VoiceDown` host->client | Unreliable | `ver u8, speaker u64, codec u8, seq u16, flags u8, loudness u8, range u8 (0.5 m), len u16, payload` (17 B) |
| `HP_VoiceMute` client->host | ReliableSequenced | `ver u8, count u8, ids u64[count]` (whole list, max 16) |

Unknown versions or codecs, length mismatches and payloads over 600 B are dropped and counted (rate-limited log).
`flags` bit 0 = first frame of a talk spurt. ADPCM payloads start with the 3-byte decoder state (`int16 predictor,
u8 step index`) so **every packet decodes on its own** (loss never desynchronises the decoder). Steam payloads are
`u16 len + bytes` blocks, one per `ReadVoiceData` result inside the 40 ms window (`SteamVoiceBlocks`).

## 5. Bandwidth budget (target <= 32 kbps per talker)
Frame = 40 ms: 25 packets/s while talking, **0 when silent** (push-to-talk released, open-mic VAD closed, or Steam's
own voice detection).

| Codec | Codec bytes/frame | + header | Up per talker | Down per talker per listener |
|---|---|---|---|---|
| **ADPCM 3-bit 8 kHz (default PCM)** | 120 + 3 state | 8 / 17 | **26.2 kbps** | **28.0 kbps** |
| ADPCM 4-bit 8 kHz (LAN-only option) | 160 + 3 state | 8 / 17 | 34.2 kbps | 36.0 kbps |
| Steam voice (typical) | ~50-100 (+2 per block) | 8 / 17 | ~12-23 kbps | ~14-25 kbps |

The 131 B / 40 ms uplink is asserted in PlayMode (`ClientVoice_ArrivesAtHost...`) and the kbps figures in EditMode
(`Bandwidth_DefaultCodec_WithinBudgetPerTalker`). Worst case host upload, 4 players all talking and all in range on
the PCM backend: each of 3 clients receives the other 2 remote talkers plus the host = 3 x 3 x 28 kbps = 252 kbps;
typical play is 1-2 talkers (28-56 kbps per client). This is on top of the 96-128 kbps gameplay budget (GDD 17.4.4
"voice excluded"). UDP/IP + transport headers add ~10 kbps per 25 pps stream at the socket and are not counted above.

Server enforcement per sender: token bucket 60 packets/s (burst 20) and 8000 B/s (2x budget); violations are dropped
and logged at most once per 5 s.

## 6. Playback, jitter and loss (`VoiceSpeakerStream`, `VoiceSpeakerPlayback`)
* **Jitter buffer** per speaker (`VoiceJitterBuffer`, 16 slots, u16 sequence with wrap): pre-fills 2 frames (80 ms)
  before playing, re-orders, plays in sequence order, drops duplicates and late packets, resets on a jump > 16 frames.
  A frame flagged spurt-start re-anchors the buffer even while the previous spurt is still draining.
* **Loss**: a missing frame while later frames are buffered is concealed by repeating the last decoded frame at
  half gain **once**, then silence frames (playout timing is preserved). 3 consecutive empty pulls end the spurt and
  the buffer re-prefills. The speaking state times out 0.4 s after the last packet.
* **Playout clock**: the main thread pops one frame per 40 ms (unscaled time, so pausing solo play does not stall
  voice) into a 16 kHz ring capped at 240 ms (overflow drops the oldest audio = bounded latency) and pre-buffers one
  extra frame at spurt start. The audio thread reads the ring in `OnAudioFilterRead` with linear resampling to the
  output rate and multiplies it into a looping constant-1 carrier clip, so Unity applies the 3D pan and the distance
  curve to it.
* **Attenuation** (`VoiceMath.Gain`): 1 within 3 m, `3/d` beyond (-6 dB per doubling, i.e. logarithmic rolloff),
  faded to 0 over the last 3 m before the range; baked into a **Custom** rolloff curve with explicit keys at the knee
  and fade start (Unity's built-in Logarithmic mode never reaches silence at maxDistance).
  **Range = 25 m x `DayParams` `Noise.VoiceRadiusMult`** (Megaphone upgrade x2 -> 50 m, Quiet Hours x1.25), clamped
  to x0.5..x3, resolved by the host every 0.5 s and sent in every `HP_VoiceDown` so listeners re-bake their curve.
  Occlusion: -9 dB per blocking collider on the Default layer (max 2), ray-cast at 10 Hz, ignoring both players' own
  hierarchies.
* **Volume**: `AudioSource.volume = Master x Voice bus x occlusion`, read from `IAudioSettingsSink`; the Voice bus is
  the existing "Voice chat" slider. While any speaker within 15 m (or the local talker) is speaking the primary
  service calls `IAudioService.ReportVoiceActivity(true)`, so Audio's existing music/critter ducking kicks in.
* **Latency estimate (LAN)**: 40 ms frame + network (< 5 ms) + 80 ms prefill + up to 40 ms ring + DSP buffer
  (~10-20 ms) = roughly 170-190 ms mouth-to-ear; Steam relay adds its round-trip share.

## 7. Controls and settings
* **Push-to-talk is the default** (`GameSettings.PushToTalk = true`, key **V**, `VoiceConfig.PushToTalkBinding`), so
  nobody transmits by surprise. Open mic uses a voice-activity gate (loudness 0..1, default 0.2, 50 ms attack /
  300 ms release, 0.4 s hangover). Settings > Audio: "Voice chat" volume (existing), "Push to talk (V)" (existing
  toggle, label updated) and the new "Open mic sensitivity" slider (= 1 - threshold). `UiRuntime` pushes both into
  `IVoiceChat`. Push-to-talk is ignored while `InputModal.IsBlocking` (menus, chat typing in the crew overlay).
* **Per-player mute**: Lobby crew overlay (Tab) rows get a "Mute/Unmute" button and a "TALKING" tag; the in-game
  pause menu lists "Mute <name>" buttons above Leave. Muting is local and is also sent to the host so it stops
  relaying that speaker to you (saves your downlink). Mutes are per session (client ids are reused per session).
* **Speaking marker**: `<)` / `<))` / `<)))` (by loudness) parented under the farmer's `NameTag` object, so it
  billboards with it; built-in font material (works in every build). No Customization code was edited.
* **Steam backend**: `VoiceRecord` follows push-to-talk (+0.25 s tail so the last syllable is sent) or stays on for
  open mic; loudness is measured by decoding our own Steam data, so VAD and the voice Noise Event behave the same as
  on the PCM backend. Any Steam API exception switches that session to the Microphone backend.
* The PCM microphone stays open for the whole session while not mic-muted (avoids 50-200 ms device start latency on
  every push-to-talk press); macOS shows its microphone indicator meanwhile. `IVoiceChat.MicMuted` stops capture.

## 8. Security (agreed boundary for co-op; review with security-engineer before EA)
Server-authoritative routing: the speaker id comes from the transport, never the packet; range and mute decisions
happen on the host; clients only accept `HP_VoiceDown` from the server; malformed, oversized and flooding packets
are dropped. Client-reported loudness is clamped to 0..1 and only drives a rate-limited (0.5 s) Noise Event; the GDD
accepts this ("low cheat stakes in co-op"). Stricter compromise if needed later: for ADPCM the host can decode and
require reported loudness <= measured + 0.15.

## 9. Tests
* EditMode `Tests/EditMode/Voice`: ADPCM round-trip SNR (3/4-bit) and packet independence, clipping/silence, budget,
  packet formats and rejects, Steam block framing, decimator/resampler, ring, VAD and gating truth table, encoder
  gating (silence, push-to-talk, mic mute), gain/rolloff/range/occlusion math, relay rules, mute table, noise gate,
  rate limiter, jitter re-ordering/loss/wrap/reset, concealment and spurt re-anchoring, and the full
  encoder -> wire -> stream pipeline.
* PlayMode `Tests/PlayMode/Voice`: in-process host + client NetworkManagers (UTP, random port) with synthetic tones:
  client voice reaches the host and plays on a 3D source at the speaker head (level preserved, ducking reported,
  marker shown, speaking times out); host voice relayed to a client in range; muted speaker neither relayed nor
  played (and unmute restores); host-side mute; out-of-range dropped and the Megaphone multiplier (x2) restores it
  with a re-baked 50 m curve; push-to-talk sends only while held.
* Two-process LAN test (`tools/net/two-process-test.sh`): the client runs with `-hpVoiceSynth` and open mic; the
  host must log `[Voice] heard speaker=` (real UDP between two player processes).

## 10. Consequences / open items
* **Owner verification needed** (cannot run in batchmode): (1) real microphone on macOS: first launch shows the
  permission prompt with the usage text set in Player Settings, push-to-talk and open mic both work, voice is
  positional and fades out at 25 m; (2) Steam voice between two machines/accounts on the Steam transport (needs a
  real App ID, 480 is the Spacewar test id) including mute and the Megaphone; (3) Windows build capture.
* GDD deltas (flagged to game-designer): range is 25 m per this beta brief vs 35 m in `proximity-voice.md`; the
  Megaphone is x2 through the existing upgrade modifier vs x2.5 in GDD 12.5; KO muffle, ghost voice, radio channel,
  mic calibration, a HUD "you are transmitting" icon, rebinding the push-to-talk key, gamepad push-to-talk and
  Camcorder voice capture are not built.
* `microphoneUsageDescription` is set in ProjectSettings (shared iOS/macOS field). Signed/notarised macOS release
  builds with the hardened runtime additionally need the `com.apple.security.device.audio-input` entitlement.
* Wideband upgrade path: Opus via a native plugin (16 kHz at ~16-24 kbps) behind the same `VoiceCodec` byte.
