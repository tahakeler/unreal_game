# ADR: Proximity Voice Chat (Steam voice first, PCM codec fallback, text last)

> **Status**: Accepted for early beta (2026-10-21) · **Author**: network-programmer · **Branch**: `feature/voice-chat`
> **Resolves**: GDD 17.4.6 / 17.12 item 1 ("Proximity voice provider - ADR before Alpha"), beta DoD item 7.
> **Design sources**: `design/gdd/proximity-voice.md`, `design/HARVEST-PANIC-GDD.md` 12.4, 12.5 (Megaphone), 17.4.4 (budget).

## 1. Context
HARVEST PANIC is 2-4 player first-person co-op on NGO (host = listen server) over Facepunch Steam transport
(ship) or Unity Transport (LAN/dev). The GDD wants 3D positional voice whose loudness also feeds the Noise
Event system. Candidates were Steam Voice through Facepunch (no new dependency, we place and compress audio
ourselves) and Unity Vivox (positional channels built in, extra service and account). The prototype-era GDD
text that names EOS RTC and UE VoIP talkers is superseded by this ADR.

## 2. Decision
One voice pipeline, three backends chosen per session, all carried as **NGO custom named messages** on
`NetworkDelivery.Unreliable` through the **host relay** (no peer-to-peer):

| Priority | When | Capture | Codec on the wire | Payload bitrate |
|---|---|---|---|---|
| 1. **Steam voice** | transport is Facepunch and `SteamClient.IsValid` | `SteamUser.VoiceRecord`, `ReadVoiceData` | Steam's own compressed voice (`DecompressVoice` on receivers, 16 kHz out) | ~10-20 kbps + 10 B/40 ms framing |
| 2. **PCM fallback** | LAN / Unity Transport, or Steam voice unavailable | Unity `Microphone` 16 kHz mono | IMA ADPCM at 8 kHz: 4-bit on LAN, 3-bit over the internet | 4-bit 34.2 kbps (LAN only), 3-bit 26.2 kbps |
| 3. **Text + pings** | no mic, muted, or voice broken | - | existing lobby chat (`ISessionSocial`), Holler, Point ping | - |

Why not µ-law: G.711 µ-law is 8 bits/sample, i.e. 64 kbps at 8 kHz and 128 kbps at 16 kHz, 2-4x the
32 kbps budget. IMA ADPCM is the same family of "simple, no dependency, no licence" codec at 3-4 bits/sample.
Why not Vivox: an extra account/service and SDK for a 4-player game whose Steam dependency already exists.
Why the host relay rather than Steam P2P per pair: range culling, mute lists and the Noise Event all need the
server; with <= 4 players the host upload stays small (see 5).

## 3. Pipeline
```
owner client                              host (server)                         listening client
mic/Steam -> 40 ms frame -> RMS/loudness  "HP_VoiceUp" (unreliable)              "HP_VoiceDown" (unreliable)
 -> VAD (open mic) or PTT gate (V)   ---> validate sender, size, token bucket ---> mute check -> jitter buffer
 -> encode (Steam / ADPCM)                range cull per listener (+2 m margin)     -> decode -> 16 kHz ring
                                          skip listeners that muted the speaker      -> 3D AudioSource on the
                                          voice Noise Event (>= 0.35, 0.5 s)           speaker's HeadPivot
                                          host: plays locally, no loopback send    speaking icon over head
```
* Code: `Assets/Scripts/Voice/` (asmdef `HarvestPanic.Voice`: Core, Netcode, Collections, InputSystem, Facepunch).
  `Logic/` is pure C# (EditMode tested); `Runtime/` holds the MonoBehaviours and the Steam/Microphone adapters.
* Contract: `HarvestPanic.Core.IVoiceChat` (add-only, registered in `Services`) so UI can mute players and set
  mode/threshold without referencing the Voice assembly.
* Capture is behind `IVoicePcmSource`; tests inject `SyntheticPcmSource`. **The microphone is never opened in
  batchmode** unless `-hpVoiceMic` is passed.

## 4. Wire format (versioned, `VoicePacket.Version = 1`)
| Message | Delivery | Layout |
|---|---|---|
| `HP_VoiceUp` client->host | Unreliable | `ver u8, codec u8, seq u16, flags u8, loudness u8, len u16, payload` (8 B header) |
| `HP_VoiceDown` host->client | Unreliable | `ver u8, speaker u64, codec u8, seq u16, flags u8, loudness u8, range u8 (0.5 m), len u16, payload` (17 B header) |
| `HP_VoiceMute` client->host | ReliableSequenced | `ver u8, count u8, ids u64[count]` |

Unknown versions or codecs are dropped and counted (rate-limited log). `flags` bit 0 = first frame of a talk
spurt. ADPCM payloads start with the 3-byte decoder state (`int16 predictor, u8 step index`) so every packet
decodes on its own (loss never desynchronises the decoder). Steam payloads are `u16 len + bytes` blocks, one per
`ReadVoiceData` result inside the 40 ms frame.

## 5. Bandwidth budget (target <= 32 kbps per talker on internet links)
Frame = 40 ms (25 packets/s while talking, 0 when silent: open-mic VAD and Steam both suppress silence).

| Codec | Codec bytes/frame | + header | Up per talker | Down per talker per listener |
|---|---|---|---|---|
| ADPCM 3-bit 8 kHz (internet fallback) | 120 + 3 state | 8 / 17 | **26.2 kbps** | **28.0 kbps** |
| ADPCM 4-bit 8 kHz (LAN default) | 160 + 3 state | 8 / 17 | 34.2 kbps | 36.0 kbps (LAN, budget n/a) |
| Steam voice (typical) | ~50-100 | 8+2 / 17+2 | ~12-23 kbps | ~14-25 kbps |

Worst case host upload, 4 players all talking in range, 3-bit fallback: each of 3 clients receives 2 remote
talkers (the host's own voice is the third) = 3 x 3 x 28 kbps = 252 kbps; typical is 1-2 talkers at a time
(~28-56 kbps per client). This is on top of the 96-128 kbps gameplay budget (GDD 17.4.4 says "voice excluded").
UDP/IP + transport headers add ~1 kbps per 25 pps stream at the socket level and are not counted above.
Server enforcement: per-sender token bucket of 60 packets/s (burst 20) and 8000 B/s (2x budget); max payload
600 B; violations are dropped and logged at most once per 5 s per sender.

## 6. Playback, jitter and loss
* **Jitter buffer** per speaker (`VoiceJitterBuffer`, 16 slots, u16 sequence with wrap): pre-fills 80 ms (2 frames)
  before playing, plays in sequence order, drops duplicates and late packets, resets on a jump > 16 frames.
* **Loss**: a missing frame while later frames are buffered is concealed by repeating the last decoded frame at
  half gain once, then silence; 3 consecutive empty pulls end the spurt and the buffer re-prefills. Speaker
  times out after 0.5 s without packets (indicator off).
* **Playout clock**: the main thread pops frames at real-time rate into a 1 s, 16 kHz ring buffer; the audio
  thread reads the ring in `OnAudioFilterRead` with linear resampling to the output rate and multiplies it into
  a constant-1 clip played by a 3D `AudioSource` (so Unity applies the 3D pan and the distance curve).
* **Attenuation** (`VoiceMath.Gain`): 1 within 3 m, `3/d` (log, -20·log10(d/3) dB) beyond, faded to 0 over the
  last 3 m before the range; baked into a Custom rolloff curve. **Range = 25 m x `Noise.VoiceRadiusMult`**
  (Megaphone upgrade x2 -> 50 m, Quiet Hours x1.25), clamped to x0.5..x3, resolved by the host and sent in each
  `HP_VoiceDown` packet. Occlusion: -9 dB per blocking wall (max 2) on the Default layer, re-cast at 10 Hz.
* **Volume**: `AudioSource.volume = Master x Voice bus x occlusion` read from `IAudioSettingsSink`; the voice
  bus is the existing "Voice chat" slider. While any speaker (or the local talker) is within 15 m the client calls
  `IAudioService.ReportVoiceActivity(true)` so Audio's existing ducking kicks in.

## 7. Controls and settings
Push-to-talk (default **V**, `VoiceConfig.PushToTalkBinding`) or open mic with a voice-activity threshold
(loudness 0..1, default 0.2, 50 ms attack / 300 ms release, 0.4 s hangover). Settings > Audio already had
"Voice chat" volume and "Push to talk"; this change adds "Open mic sensitivity". Per-player mute is in the Lobby
crew list (and `IVoiceChat.SetMuted` for any other UI). Muting is local and also sent to the host so it stops
relaying that speaker to you. While `InputModal.IsBlocking` (menus) push-to-talk is ignored.

## 8. Security (agreed boundary for co-op; review with security-engineer before EA)
Server-authoritative: sender id comes from the transport, never the packet; range and mute decisions happen on
the host; malformed/oversized/flooding packets are dropped. Client-reported loudness is clamped to 0..1 and only
drives a rate-limited (0.5 s) Noise Event: the GDD accepts this ("low cheat stakes in co-op"). Compromise if
stricter checking is wanted later: for ADPCM the host can decode and require reported loudness <= measured + 0.15.

## 9. Consequences / open items
* Real Steam voice and real microphone capture cannot be exercised in batchmode; they need an owner test on two
  Steam accounts (see `docs/qa/voice-progress.md`).
* GDD deltas (flagged to game-designer): range is 25 m per this beta brief vs 35 m in `proximity-voice.md`;
  Megaphone is x2 through the existing upgrade modifier vs x2.5 in GDD 12.5; KO muffle, ghost voice, radio
  channel, mic calibration and Camcorder voice capture are not built yet.
* macOS builds need a microphone usage description in Player Settings (Networking owns ProjectSettings).
