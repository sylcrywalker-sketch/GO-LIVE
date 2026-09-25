# Stream hardware and peripheral readiness

Branch: `codex/stream-peripheral-readiness`. Readiness correction follows `6fa20af`; no merge to main.

## Ownership and physical definition

`PcPeripherals` is a plain C# owner of two explicit connections, each holding a physical item instance ID. It exposes `HasMicrophone`, `HasWebcam`, connection commands, `Changed`, and validated snapshot capture/restore. It does not inspect the shop, purchase history, inventory or room proximity.

`ItemDefinition.PeripheralKind` is configuration. `PcPeripheralsBehaviour` bridges the actual `WorldItem`/`PlayerCarry` transfers to the domain. An item is connected only after the player carries the matching device to its authored desk socket and presses E: the same item moves to that socket's install anchor with `ItemLocation.Installed`, then its ID is registered. E with empty hands removes that same item. The bridge validates the explicit player, interaction control permission, kind, empty socket and item identity. Its transfer guard prevents reentrant commands while an item moves. A failed transfer does not publish a connection.

The desk microphone is intentional starter equipment owned by the fictional character: its existing model is a persistent scene item, explicitly installed on a new game and removable. It represents the character's stream audio equipment, never the player's real OS microphone. It is kept as starter equipment, not as a dependency for broadcasting. No microphone quality tiers are introduced. The existing used webcam shop entry fulfils through the existing delivery system using the existing webcam model, a runtime item prefab and an inventory icon rendered from that model. Purchasing, delivering, carrying or storing a device never connects it.

## Start validation and lifecycle

`StreamSession.EvaluateReadiness` returns channel, internet, microphone, webcam and selected-quality readiness. `ErrorKey` alone determines `CanStart`; `WarningKey` is a computed microphone quality warning and never blocks start. `CheckStart` and `Start` use that same result. Streamly's Start button uses `CanStart`; Stop remains available while live. The warning adds no stored state, dependencies, subscriptions or save fields.

Channel connection, a working powered desktop, game-owned upload and supported quality are required. Upload thresholds remain 1/3/6 Mbps for Low/Medium/High. Zero or invalid upload is offline. High still requires the existing GPU capability. Both the in-game microphone and webcam are optional: broadcasting can start with either or both absent. PC boot validation is unchanged: a GPU remains optional for boot.

The desktop runtime receives the peripheral owner explicitly, waits for both PC and peripherals to initialize, and subscribes to connection changes. `SetUploadMbps` updates the existing game-owned upload value and refreshes the same validation. Internet loss or another genuinely required condition uses the existing stream abort/completion path. Removing the microphone during Starting or Live keeps the broadcast running and updates readiness; reconnecting clears its warning. Webcam removal retains its optional behavior. These events update visible Streamly without per-frame device searches or a second UI-owned readiness state. Future Viewer Simulation can consume the existing microphone-presence state as degraded perceived audio quality; no engagement model is added here.

There is no webcam face feed in this slice. The existing room preview and its scoped render texture are preserved for both camera states; no simulated webcam image is introduced.

## Independent real voice boundary

Future real voice input follows: **OS microphone → Voice Capture Adapter → VAD → ISpeechRecognizer → RecognizedSpeech → StreamSpeechFeed → Viewer AI / Chat Director**.

`PcPeripherals` does not belong in that pipeline. Voice capture and speech recognition must never require an in-game microphone `ItemInstance`. Its current domain contract contains only fictional equipment IDs, kinds, snapshots and change notifications; it has no OS device, audio capture or speech-service dependency. A contract/state dependency test protects that boundary. This correction implements none of the future voice pipeline: no Windows microphone capture, VAD, STT, interfaces or placeholder services.

## Saves

Save version remains **7**, with existing version 6 migration retained. The optional `Peripherals` snapshot contains microphone and webcam instance IDs. Save preflight partitions installed internal PC parts and external peripherals, then validates exact ownership, kind, duplicates and orphan connections before any live state changes. Restore uses the appropriate owner's anchor and restores each physical item once.

Older saves that omit `Peripherals` load an empty connection record. No connection is inferred from purchases or loose devices. If an old save predates the newly authored desk microphone item, the existing policy for omitted scene items marks it removed; a physical bought microphone can be connected normally. Empty peripheral connections do not block broadcasting. Active streams and open windows remain transient during load, and restore notifications cannot generate an extra stream summary. This correction does not modify persistence or the scene.

## Presentation

The existing five Streamly readiness rows keep their layout. Ready rows are green; failed required rows are red; absent microphone and webcam rows are yellow. Microphone absence displays “Микрофон не обнаружен” / “Microphone not detected”, with a yellow footer: “Без игрового микрофона качество звука ниже.” / “Without an in-game microphone, stream audio quality is reduced.” The footer remains yellow while live. Blocking errors take priority over the quality warning. The approved shell, app chrome, icons, other applications and 708×399 room preview retain their existing design; no scene/UI regeneration is needed for this correction.

## Verification

Before the correction, five changed regression cases failed for the intended reasons: microphone-free starts returned `desktop.stream.microphone_missing`, and microphone removal during Starting/Live produced Offline instead of Live. Those failures demonstrate the superseded rule, not the desired behavior.

Corrected tests cover microphone-free start with and without a webcam, warning/error separation, live microphone removal and reconnect, uninterrupted startup after microphone removal, unchanged optional webcam behavior, internet loss with/without a microphone, exactly-once abort under reentrant refresh, and equipment-only contract/state dependencies. The real GL journey clicks Start without a microphone, removes both physical peripherals during broadcasts, checks yellow warnings and Start/Stop availability, and verifies required internet loss still aborts exactly once. Existing ownership, save/load, PC and desktop tests remain in the full suite.

Full suite on Unity **6000.6.0f1**: **743 passed / 743 total, 0 failed, 0 skipped**, 46 fixtures, 110.35 seconds. All existing unrelated tests remain green. The final run includes the corrected equipment dependency guard and regenerates all 14 required bilingual readiness captures plus two additional live microphone-loss captures.

Evidence: [full results](<E:/GO-Live-Peripheral-correction-audit-20260925/final-full-suite.xml>), [Unity log](<E:/GO-Live-Peripheral-correction-audit-20260925/final-full-suite.log>), [five failing regressions before correction](<E:/GO-Live-Peripheral-correction-audit-20260925/red.xml>). Independent read-only review found no additional actionable issues in production logic, lifecycle, localization or the corrected tests.

All following captures are actual **1920×1080 Game View**, inspected in both languages; the capture test also checks translated text, truncation, readiness marker states and Start availability. The full suite recaptured the previous Desktop/PC journey as well.

| State | Russian | English |
|---|---|---|
| Everything ready | [RU](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-ru-01-everything-ready.png>) | [EN](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-en-01-everything-ready.png>) |
| Microphone missing | [RU](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-ru-02-microphone-missing.png>) | [EN](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-en-02-microphone-missing.png>) |
| Optional webcam missing | [RU](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-ru-03-webcam-missing.png>) | [EN](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-en-03-webcam-missing.png>) |
| Internet missing | [RU](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-ru-04-internet-missing.png>) | [EN](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-en-04-internet-missing.png>) |
| Unsupported quality | [RU](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-ru-05-unsupported-quality.png>) | [EN](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-en-05-unsupported-quality.png>) |
| Live with webcam | [RU](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-ru-06-live-with-webcam.png>) | [EN](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-en-06-live-with-webcam.png>) |
| Live without webcam | [RU](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-ru-07-live-without-webcam.png>) | [EN](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-en-07-live-without-webcam.png>) |
| Additional: live after microphone removal | [RU](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-ru-08-live-without-microphone.png>) | [EN](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-en-08-live-without-microphone.png>) |

[Physical socket and E prompt](<E:/GO-Live-Peripheral-correction-audit-20260925/screenshots/peripheral-world-connected.png>).

Existing unrelated generated project files and the pre-existing TMP fallback changes were restored to their initial working-copy state and excluded from the commit.

Evidence directory: `E:\GO-Live-Peripheral-correction-audit-20260925`.
