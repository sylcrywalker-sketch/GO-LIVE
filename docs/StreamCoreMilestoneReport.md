# Stream core milestone: desktop capture, viewer simulation, real voice

Branch `claude/sharp-wozniak-iakuny` (current branch). No merge to main.

## 0. Verification status — read first

This milestone was implemented in a **Linux cloud container without the Unity Editor, without a GPU and without a microphone**. Everything below separates what was actually executed here from what must still be run on the Windows Unity 6000.6.0f1 machine.

| Check | Status |
|---|---|
| Full project compile (runtime, Editor scripts, all tests, whisper.unity package) | **Done here**, 0 errors — against Unity 2021.3 reference assemblies + UGUI/TMP/Input System/Test Framework sources, with harness-only shims for a few Unity 6 APIs (`Rigidbody.linearVelocity`, `CharacterController.includeLayers`, one `FindObjectsByType` overload, `GetEntityId`, URP `Volume`). Baseline compiled with the same 0 errors before any change. |
| Plain C# test suites (stream, audience, completion, readiness, accounts, wallet, desktop state/windows, voice pipeline, speech feed) | **Done here**: 173 / 173 passed on .NET 8 + NUnit 3.14 (a minimal fake `ScriptableObject` stands in for PC component specs). Repeated 3× for the threaded voice tests: stable. |
| Real STT backend (whisper.unity 1.4.0 native whisper.cpp + bundled model + this project's VAD/worker/recognizer code) | **Done here** on WAV input through the production classes (Linux native libraries of the same package). Results in D. **Not a microphone.** |
| Unity EditMode/PlayMode suite (baseline 743) | **Not run** — no Unity here. Expected count after this milestone: 743 + 71 = **814** (+1 `[Explicit]` real-microphone test). |
| 1920×1080 Game View screenshots | **Not captured** — no Unity/GPU here. The PlayMode tests listed in F produce them. |
| C15 real-microphone acceptance | **Not performed** — no microphone here. Run `RealMicrophoneRussianAndEnglishReachTheStreamOnlyWhileLive` (D). |

Nothing in this report is a fabricated screenshot, fabricated test result or fabricated recognition.

## A. Stream capture

**Previous path.** `DesktopSceneAuthoring.BuildPreviewCamera` authored a disabled "Streamly room preview camera" (a copy of the player camera placed at the monitor, FOV 65, depth −2). `StreamlyView` enabled it while open and rendered it into its own 960×540 RenderTexture. Label: «Источник: камера комнаты».

**Audit.** The desktop is a Screen Space – Overlay canvas ("Desktop Canvas", sort 100, 1920×1080 reference). It has no camera, RenderTexture or surface texture; the physical monitor shows a compact projection (`MonitorDesktopView`), not a pixel copy. The only authoritative rendered desktop output is the composed screen while the player is focused on the PC.

**New source.** `DesktopCaptureSource` (on "Desktop System") owns the single RenderTexture of the displayed desktop. At end of frame (`WaitForEndOfFrame`, instance cached) it calls `ScreenCapture.CaptureScreenshotIntoRenderTexture` — only while (a) a consumer needs it and (b) `PcSession.Usage == Focused` (which implies a running PC with the monitor on, i.e. the desktop is on screen). Otherwise it captures nothing and holds the last desktop frame. It never substitutes a camera. The texture is screen-sized, sRGB (project is linear), with mips regenerated after each copy and trilinear sampling for a clean 708×399 preview. `FrameUv` flips V on `graphicsUVStartsAtTop` platforms (back-buffer copies are top-down there).

**Ownership / lifecycle.**
- Consumers: `StreamlyView.OnEnable/OnDisable` → `AddPreviewViewer/RemovePreviewViewer`; a broadcast (`StreamSession.State != Offline`) is the second consumer.
- Created lazily on the first needed capture, recreated on resolution change (raises `Changed`), released and destroyed as soon as no consumer remains (immediately on the last preview release, at end of frame when a broadcast ends) and on disable.
- Preview and LIVE use the **same instance**; closing Streamly during LIVE keeps capturing; reopening shows the same texture.
- Failure: capture exceptions log once and switch to a stable unavailable state. Label «Источник сигнала недоступен» / «Capture source unavailable». Normal label «Источник: экран» / «Source: screen».
- `StreamlyView` now owns no camera or render resource. The GL scene camera object was removed; authoring scripts updated.

**Webcam.** Unchanged equipment state; no fake feed; the webcam never affects the capture source (structural test + journey with webcam connected/disconnected).

**Leak prevention.** One named texture ("Desktop broadcast capture"); the journey opens/closes Streamly 5× and asserts 0 or 1 such textures at every step.

## B. Viewer simulation

**Owner.** `AudienceSimulation` (plain C#, `Scripts/Desktop/Audience`). One instance per broadcast, created by `StreamSession.Start`, `GoLive()` on Starting→Live, advanced only while Live, frozen by `Finish()`. `StreamSession` stays the lifecycle authority and applies only the outcomes it owns (donation receipts, visible chat). The previous scripted values (viewers = ⌊t/5⌋+2, a follow every 15 s, $1 every 30 s, round-robin chat) are gone; `StreamSession.Viewers/Followers` were removed. The LIVE HUD reads `Audience.CurrentViewers/Follows`.

**Tick.** Fixed 1-second simulated steps with a carried remainder (any frame split gives identical results). One call simulates at most 3600 steps; a longer gap is integrated at the current audience without drawing outcomes.

**Inputs** (sampled every tick): game-clock minute (explicit `Tick(delta, minuteOfDay)` from `GameClockBehaviour`), channel followers, first-stream eligibility (`Trich.CompletedStreams == 0`), selected quality, game-owned upload Mbps, dedicated GPU capability, in-game microphone present, webcam present. There is no CPU tier in `PcCapabilities`, so CPU affects the model only as "encoding without a dedicated GPU".

**Model.** Birth–death (M/M/∞-style) process:
- potential = (DiscoveryViewers + FollowerTurnout·followers) · daily(t) · qualityAttraction · (1 − e^(−t/RampSeconds)) + firstStreamBoost·e^(−t/BoostSeconds)
- retention = stability(upload headroom) · encoding(GPU vs processor) · audio(in-game mic)
- target = potential · retention · engagement(webcam)
- arrivals ~ Poisson(target / stay), each viewer leaves with p = 1 − e^(−1/stay), stay = MeanWatchSeconds · retention ⇒ equilibrium = target.
- per viewer-second: follows, paid subscriptions, donations (weighted $1–$20), chat volume ~ Poisson(rate · viewers · engagement).

**Prime time.** Daily curve keypoints every 3 h: 00 .45, 03 .25, 06 .30, 09 .50, 12 .65, 15 .95, 18 1.00, 21 .90, 24 .45 (interpolated; peak inside 15:00–21:00; night weak but non-zero).

**Hardware/quality.** Attraction Low .85 / Medium 1.0 / High 1.12 (High still requires the GPU capability — existing rule). Stability .78 at the minimum upload of the quality, 1.0 at 2×. Processor-only Medium encoding .9. Missing in-game microphone .75 audio retention (stream continues). Webcam ×1.08 engagement. A GPU never adds viewers directly.

**Randomness.** `AudienceRandom` (SplitMix64) seeded per broadcast from one injectable session stream (`StreamSession`/`DesktopState` constructor parameter); per-message variation is hashed from the seed so chat content does not depend on frame splitting. No `System.Random` anywhere.

**Onboarding.** First stream: 1–3 viewers at go-live plus a fading potential boost (≈2 viewers, τ = 240 s). Not applied after the first completed stream; a start cancelled before Live keeps eligibility.

**Economy, exactly once.**
- Donations: `StreamSession` → `DonationAccount.Receive(id = stream.donation.N)` (id-deduplicated). `DonationAccount.Received` fires once per newly accepted receipt (never for repeats or restored history) → `DonationPayout` → `Wallet.Add`. Support is counted before observers run, so a completion triggered inside a receipt observer still includes it.
- Follows / paid subscriptions: committed once through the existing completion pipeline (`StreamSummary` → `TrichChannel.CompleteStream`, sequence-idempotent). `TrichSnapshot.TotalSubscriptions` is a new optional field (old saves load 0; **no save version bump**). Subscriptions are counted, not paid out.

**Result.** `StreamSummary` now carries duration, peak, average viewers, follows, subscriptions, support. Trich totals and the Outline mail (new key `desktop.mail.stream.body_v2` with duration m:ss, average, peak, follows, subscriptions, support; old mails keep their key) use it. Transient audience state is never saved.

**Calibration** (harness, 40 seeds × 10 min each, default tuning):

| Scenario | avg | peak | follows | subs | support |
|---|---|---|---|---|---|
| First stream 18:00, Medium, 5 Mbps | 2.5 | 5.2 | 0.88 | 0.03 | $1.63 |
| First stream 08:00 | 1.7 | 3.8 | 0.55 | 0.05 | $0.55 |
| 1 follower 18:00 | 1.7 | 4.4 | 0.45 | 0.03 | $0.28 |
| 1 follower 03:00 | 0.4 | 1.6 | 0.13 | 0.03 | $0.08 |
| 1 follower 18:00, no in-game mic | 1.4 | 4.0 | 0.35 | 0.00 | $0.28 |
| 100 followers, High + GPU, 8 Mbps | 5.1 | 10.9 | 2.05 | 0.18 | $3.88 |
| 1000 followers, High + GPU, 12 Mbps | 37.1 | 62.2 | 13.3 | 0.85 | $21.58 |

Sample first-stream series (every 30 s): 3 3 2 2 1 1 1 3 3 3 3 5 5 2 3 3 3 5 6 7.

## C. Real voice / STT

Pipeline: **default OS microphone → `VoiceInputBehaviour` (Unity bridge) → `VoiceActivityDetector` → `SpeechRecognitionWorker` (background thread) → `WhisperSpeechRecognizer` (whisper.cpp) → `RecognizedSpeech` → `VoiceRecognition` (main thread) → `DesktopRuntimeBehaviour` → `StreamSpeechFeed` (Live only)**.

- **Microphone.** `Microphone.Start(null /*default device*/, loop, 4 s, 16 kHz or the device's supported rate)`. Each frame reads every complete 20 ms block since the last read with `AudioClip.GetData` into a preallocated buffer (no per-frame allocation). The microphone is opened only while a broadcast is Starting/Live **and** recognition is enabled; it closes on Stop, on disable, and when no recognizer can be used. Device loss (`IsRecording` false) → status "system microphone unavailable", retry every 3 s, no exception spam.
- **VAD.** Energy-based, 20 ms frames, adaptive noise floor. Speech frame: RMS ≥ 0.012 and ≥ 3× noise. Onset needs 160 ms voiced (clicks/bursts ignored); phrase needs 400 ms voiced; closes after 650 ms silence (keeps a 120 ms tail); hard cap 12 s (continuous speech is split); 250 ms pre-roll. Buffers are allocated once: pre-roll + 12 s phrase + one frame (~200k floats at 16 kHz); only a closed phrase is copied out. Sustained sounds raise the floor slowly (τ ≈ 10 s) so a fan/hum stops opening phrases after a few seconds; gaps between words pull it back immediately.
- **Backend.** `com.whisper.unity` **1.4.0** (git tag `1.4.0`, Macoron/whisper.unity, **MIT**), bundling **whisper.cpp v1.7.5** native builds (Windows x64: `libwhisper.dll`, `ggml*.dll` incl. Vulkan; also Linux/macOS/Android/iOS). Added to `Packages/manifest.json` as a pinned git dependency (Unity resolves it on open; git must be installed; `packages-lock.json` will be updated by Unity — commit it).
- **Model.** `Assets/StreamingAssets/Whisper/ggml-tiny.bin` — official multilingual whisper tiny (OpenAI weights, MIT), **77,691,713 bytes (74.1 MiB)**, SHA-1 `bd577a113a864445d4c299885e0cb97d4ba92b5f` (matches whisper.cpp `models/README.md`). Optional upgrade for better Russian: **GO! LIVE ▸ Voice ▸ Install base speech model (142 MiB)** downloads `ggml-base.bin` from Hugging Face, verifies SHA-1 `465707469ff3a37a2b9b8d8f89f2f99de7299dac`, stores it unversioned (gitignored); `VoiceInputBehaviour` prefers it automatically (`modelFiles` order: base, tiny).
- **Languages.** Russian and English. Default "Auto RU/EN": whisper detects the language; if it picks neither, the phrase is re-recognized in the fallback (game UI language). Player can force RU or EN (halves latency). Detected ISO code is carried in `RecognizedSpeech.Language`.
- **Offline.** Fully local. No network, no keys, no billing. Audio never leaves the process.
- **Threading.** One dedicated background thread (`SpeechRecognitionWorker`, below-normal priority) creates, initializes, uses and disposes the recognizer; whisper uses `min(cores/2, 4)` native threads for inference. Main thread only runs capture, VAD and draining results. Queue bounded to 2 waiting phrases (oldest dropped). Disabling voice stops the worker without blocking (it finishes the current phrase and frees the model itself); scene unload / leaving Play Mode / quitting waits up to 3 s. The native context is freed explicitly on the worker thread (the package wrapper only frees in a finalizer, so it is not used). No `Task`s, no async framework.
- **Contract.** `RecognizedSpeech { Sequence, Text, Timestamp (monotonic SpeechClock seconds at phrase end), Confidence (mean token probability, uncalibrated; null if unavailable), Language }`. No audio, device or Unity references.
- **Stream gating.** `StreamSpeechFeed` (owned by `DesktopState`) admits a phrase only if the stream is Live when it arrives **and** the phrase ended after the broadcast went live; Starting, Offline, Stopping and results finishing after Stop are rejected; duplicate sequences rejected; its recent list (max 20) exists only for the current live broadcast and is never saved.
- **Robustness.** Whisper artifacts are filtered (`WhisperArtifacts`: bracketed sound tags like "[Bell]", music symbols, subtitle-credit hallucinations such as «Редактор субтитров …», "Amara.org"), plus a no-speech/low-confidence guard. Inputs shorter than 1.1 s are padded (whisper ignores < 1 s).
- **Failure handling.** No device, device lost, model missing, native library missing (`DllNotFoundException`/entry point/bad image), corrupt model, per-phrase inference exception → status only (`MicrophoneUnavailable`, `ModelMissing`, `RecognizerUnavailable`); the stream and audience continue (tested).
- **Player control.** Streamly caption row (the preview caption was narrowed; nothing else moved): «Голос: слушаю / включится в эфире / загрузка модели… / распознавание отключено / микрофон системы недоступен / модель распознавания не найдена / распознавание недоступно» (EN equivalents), click = On/Off; language chip «Авто RU/EN / RU / EN». Stored in `PlayerPrefs` (`GoLive.Voice.Enabled`, `GoLive.Voice.Language`), never in save data. The in-game microphone row keeps its own wording («Микрофон подключён/не обнаружен»).
- **Zero PcPeripherals dependency.** `PcPeripherals`, its snapshot and kind never reference `GoLive.Voice`/`Whisper`, and no `GoLive.Voice` type references `GoLive.PcBuilding` or `GoLive.Desktop` (reflection test over fields, properties, methods, events, constructors). The voice path runs with `HasMicrophone == false` (tested, and the PlayMode test removes the desk microphone while listening).

## D. Real microphone test

**No real-microphone test was performed.** This container has no audio input device and no Unity Editor.

To perform C15 on the Windows machine: Unity Test Runner ▸ EditMode ▸ `DesktopFlowPlayModeTests.RealMicrophoneRussianAndEnglishReachTheStreamOnlyWhileLive` ▸ Run Selected. The test starts a real broadcast in GL, logs prompts to the Console («SPEAK RUSSIAN NOW…», «SPEAK ENGLISH NOW…»), waits for each language to arrive through `StreamSpeechFeed`, stops the broadcast, listens 15 s more and asserts nothing more enters the feed. It writes the recognized texts to `Temp/DesktopVisualAcceptance/voice-acceptance.txt` and captures `stream-c-real-mic-ru/en.png`.

Russian spoken: «Привет чат, сегодня попробуем новый стрим.» — Recognized: **pending real-device run**
English spoken: "Hello chat, let's try this again." — Recognized: **pending real-device run**

**Backend evidence that is NOT a microphone** (production classes, whisper.unity 1.4.0 Linux natives, bundled tiny model, Intel Xeon 2.8 GHz, 4 vCPU, AVX2/AVX-512, CPU only; audio from WAV files):

| Input (source) | Recognized (Auto) |
|---|---|
| «Привет чат, сегодня попробуем новый стрим.» — RHVoice *aleksandr* (synthetic TTS) | «Привет, Чат. Сегодня попробуем новый стрем.» (ru) |
| same — RHVoice *anna* (synthetic) | «Привет, чёт, сегодня попробуем новый стрем.» (ru) |
| «Спасибо за подписку, это очень приятно.» — RHVoice *elena* (synthetic) | «Спасибо за подписку, это очень приятно.» (ru) |
| "Hello chat, let's try this again." — RHVoice *slt*/*bdl* (synthetic) | "Hello chat, let's try this again." (en) |
| JFK inaugural sample (real recorded human speech, whisper.cpp `samples/jfk.wav`) | "And so my fellow Americans ask not what your country can do for you, ask what you can do for your country." (en) |
| 60 ms click, 1.2 s white noise, 2 s pink noise, 1.5 s 440 Hz tone | no speech events (tone gave "[Bell]" and pink noise a subtitle-credit hallucination before the artifact filter was added) |

Full pipeline at real-time pace (20 ms blocks, noise between phrases): all phrases segmented and recognized; JFK split at his pauses into 3–4 phrases; end-of-speech → text ≈ 2.5 s in Auto mode on this box; 0 dropped, 0 failed; dispose 30–50 ms. Failure runs: missing model → `ModelMissing`; missing native library → `RecognizerUnavailable`; truncated model → `RecognizerUnavailable`; no exceptions.

Expected on the target PC: faster than above (typical 6–8-core desktop CPUs are 2–4× this 4-vCPU box). Real-microphone quality with *tiny* will be lower than on clean TTS (room noise, distance, accents); if Russian is not reliable enough, install the base model (menu above) and re-run.

## E. Tests

New (Unity counts; `[TestCase]` rows counted individually):
- **Stage A (4):** `StreamCaptureSourceTests` (3: no camera/texture in Streamly, no camera/webcam/peripheral dependency in the source, consumer lifetime) and PlayMode `StreamlyPreviewAndLiveShareTheDisplayedDesktopCapture` (frame vs independent screenshot: content and orientation; content changes with the browser; LIVE keeps the same instance with Streamly closed; webcam on/off keeps the source; leaving the desk freezes the last desktop frame; 5× open/close without texture accumulation; RU/EN captures).
- **Stage B (29):** `AudienceSimulationTests` (18), `StreamAudienceIntegrationTests` (10), PlayMode `LiveHudFollowsTheAuthoritativeAudienceSimulation` (600 s second-by-second HUD = simulation, rises and falls, summary mail, RU/EN captures).
- **Stage C (38 + 1 explicit):** `VoicePipelineTests` (29), `StreamSpeechFeedTests` (8), PlayMode `StreamlyShowsRealVoiceStatusSeparateFromTheInGameMicrophone`, `[Explicit]` `RealMicrophoneRussianAndEnglishReachTheStreamOnlyWhileLive`.

Updated existing tests (behaviour intentionally replaced, assertions kept at least as strict): scripted totals (4 follows, $2.00, one receipt per 30 s) → exact equality with the seeded simulation result plus non-zero checks; `Tick(delta)` → `Tick(delta, minuteOfDay)`; the journey helper `StartBroadcast` now asserts HUD = simulation, chat count = simulated volume, wallet delta = accepted support; `StreamSummary` constructor gained average/subscriptions.

Executed here: 173 / 173 plain C# tests (AudienceSimulation 18, StreamAudienceIntegration 10, VoicePipeline 29, StreamSpeechFeed 8, StreamSession 25, StreamPeripheralReadiness 43, DesktopCompletion 5, DesktopAccount 22, DesktopState 4, DesktopWindows 6, Wallet 3). **Entire Unity suite: not run here** (expected 814 + 1 explicit).

## F. Screenshots

**None captured in this environment.** The PlayMode tests write real 1920×1080 Game View PNGs to `Temp/DesktopVisualAcceptance` (or `GO_LIVE_VISUAL_OUTPUT`):
- `stream-a-{ru,en}-01-preview-desktop`, `-02-preview-with-browser`, `-03-live-desktop-without-webcam`, `-04-live-desktop-with-webcam`
- `stream-b-{ru,en}-01-live-audience-2min`, `-02-live-audience-10min`, `-03-summary-mail`
- `stream-c-{ru,en}-01-voice-ready`, `-02-voice-disabled`, `-03-live-voice-status`; `stream-c-real-mic-{ru,en}` (explicit test)
- existing journeys re-capture `ru-12-streamly-live`, `en-02-streamly-live`, readiness pairs, etc.

Visual review still to be done on those captures: the recursive "screen in screen" preview, caption row fit (the longest label, EN «Voice: system microphone unavailable», must fit its 318 px box; the TMP truncation check in `CaptureApp` asserts it), colours of the voice chip, and absence of any room image in the preview.

## G. Performance

- Capture: one screen-sized GPU copy + mip generation per frame only while the desktop is focused and a preview/broadcast needs it; zero managed allocation per frame; no extra camera render (the removed room camera used to render the scene a second time while Streamly was open).
- Audience: ≤ 1 step per simulated second, constant work per step (a few `Math.Exp`, ≤ 64 Bernoulli draws or a normal approximation), no per-tick allocation except new receipts/chat messages.
- Voice: microphone ring 4 s; 20 ms read blocks (`rate/50` samples); VAD per 20 ms frame; one allocation per closed phrase; recognition queue ≤ 2 waiting phrases; results drained each frame without allocation when empty.
- Measured here (tiny, CPU, 4 threads): model load 210–240 ms; inference ≈1.0–1.2 s with a fixed language, ≈1.9–2.3 s in Auto (language detection runs an extra encoder pass); 1 thread: 3.5 s / 6.8 s. whisper compute buffers ≈190 MB + model ≈75 MB; harness process peak RSS 350 MB. `audio_ctx` shortening cut latency ~7× but visibly degraded Russian, so it is not enabled.

## H. Known limitations

- Capture = display capture: while focused it copies everything composed on screen, including the game's LIVE overlay (viewer counter/chat) and the recursive Streamly preview; away from the desk the broadcast holds the last desktop frame. With one monitor, the preview can only show another app where it is not covered by Streamly. Orientation relies on `graphicsUVStartsAtTop`; the PlayMode test verifies it against a real screenshot — not yet run.
- Audience: tuning is a first pass; `DonationAccount` keeps its pre-existing lifetime cap of 4096 receipt ids (after that, support is rejected); paid subscriptions are counted but pay nothing; no CPU tier exists, so "CPU" means processor-only encoding.
- Voice: energy VAD measures loudness, not voice — music, TV or loud typing near the mic can open phrases (the recognizer then usually returns nothing, costing CPU); very quiet speakers below −38 dBFS RMS are not detected. Windows privacy settings that deny microphone access can yield silence rather than an error (status would say listening but nothing is recognized). *tiny* makes more Russian errors than English (e.g. «стрем» for «стрим»); the base model is recommended for Russian. Vulkan GPU inference is disabled by default (`useGpu`); the Windows native chain loads `vulkan-1.dll` via `ggml-vulkan.dll` (present with any current GPU driver) — on a machine without it the backend reports "recognition unavailable". Auto language doubles latency. Existing PlayMode journeys now open the default microphone during their broadcasts when voice is enabled.
- Tooling: the generated `.csproj` files and `packages-lock.json` were not edited; Unity will regenerate/update them.

## I. Commits

- A — `f860fab` Fix Streamly desktop capture source
- B — `ea091c0` Add authoritative viewer simulation
- C — the commit that adds this report: Add real stream speech recognition

## Definition of done — status

| Item | Status |
|---|---|
| Preview shows the desktop, not the room camera; preview and LIVE share one source | Implemented; room camera removed; PlayMode proof written, **not run here** |
| Viewer Simulation drives audience; HUD uses it; real average/peak; first-stream boost; exactly-once donation/follow/sub | Implemented; **tested here** (plain C#); HUD PlayMode test written, not run |
| Real OS microphone captured; VAD segments; STT recognizes RU and EN; Live-only feed; no in-game mic needed; STT failure keeps the stream | Implemented; backend + VAD + worker + feed **tested here on WAV input**; **real microphone not tested** |
| Full suite green; RU/EN visuals inspected | **Pending on the Unity machine** |
| No merge to main | Done |
