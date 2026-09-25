# Stream peripheral readiness correction

**Goal:** Make the fictional microphone optional for starting and continuing a broadcast on `codex/stream-peripheral-readiness`; commit without merging main.

**Architecture:** Keep `PcPeripherals` as the existing owner of fictional equipment identities. Remove the microphone from `StreamSession`'s required environment checks. A computed warning on `StreamReadiness` distinguishes degraded audio presentation from blocking errors without adding state, subscriptions or persistence.

**Tech stack:** Unity 6000.6.0f1, C#, NUnit and existing Game View capture tests.

## Constraints and ownership

- No Streamly redesign, peripheral rearchitecture, quality tiers, OS capture, VAD or STT implementation.
- Channel, upload and supported quality remain required; existing powered desktop checks remain.
- Microphone and webcam are optional. Starting/Live survives their removal.
- `StreamReadiness.WarningKey` is a derived presentation result, not another state owner. Dependencies, `Changed` events and save version remain unchanged.
- Real voice capture is independent of fictional items: OS microphone → Voice Capture Adapter → VAD → ISpeechRecognizer → RecognizedSpeech → StreamSpeechFeed → Viewer AI / Chat Director. No `PcPeripherals` or in-game `ItemInstance` requirement belongs in this pipeline.
- Keep the deliberately provided physical starter microphone and document it as character equipment.

## Steps

- [x] Update domain tests in `StreamSessionTests.cs` and `StreamPeripheralReadinessTests.cs`; observe failures for microphone-free start and removal during Starting/Live before changing production logic.
- [x] Remove the microphone requirement in `StreamSession.cs`; add computed warning to `StreamReadiness.cs`; make the existing mic row and footer yellow in `StreamlyView.cs`. Update both `PeripheralReadinessLocalization.cs` and the existing localization catalog.
- [x] Extend tests for warning/error separation, reconnect, internet abort exactly once and the equipment-only dependency boundary. Adjust `PeripheralVisualPlayModeTests.cs` to exercise the real Start button without a mic and real physical mic removal while live.
- [x] Run the complete Unity suite, regenerate and inspect the 14 RU/EN readiness captures plus live microphone-loss evidence. Update `docs/StreamPeripheralReadinessReport.md` and mark the original plan's microphone rule superseded.
- [x] Review the diff and restore the three pre-existing generated-file changes exactly. Deliver this correction as a commit on the existing branch without merging main.
