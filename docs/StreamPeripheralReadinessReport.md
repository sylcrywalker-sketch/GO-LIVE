# Stream hardware and peripheral readiness

Branch: `codex/stream-peripheral-readiness`, based on `222c6cb`. No merge to main.

## Ownership and physical definition

`PcPeripherals` is a plain C# owner of two explicit connections, each holding a physical item instance ID. It exposes `HasMicrophone`, `HasWebcam`, connection commands, `Changed`, and validated snapshot capture/restore. It does not inspect the shop, purchase history, inventory or room proximity.

`ItemDefinition.PeripheralKind` is configuration. `PcPeripheralsBehaviour` bridges the actual `WorldItem`/`PlayerCarry` transfers to the domain. An item is connected only after the player carries the matching device to its authored desk socket and presses E: the same item moves to that socket's install anchor with `ItemLocation.Installed`, then its ID is registered. E with empty hands removes that same item. The bridge validates the explicit player, interaction control permission, kind, empty socket and item identity. Its transfer guard prevents reentrant commands while an item moves. A failed transfer does not publish a connection.

The existing decorative desk microphone is converted to a persistent scene item without duplicating its visual. It is explicitly installed on a new game and can be removed. The existing used webcam shop entry now fulfils through the existing delivery system using the existing webcam model, a runtime item prefab and an inventory icon rendered from that model. Purchasing, delivering, carrying or storing a device never connects it.

## Start validation and lifecycle

`StreamSession.EvaluateReadiness` returns channel, internet, microphone, webcam and selected-quality readiness plus one authoritative error. `CheckStart` and `Start` use that same result. Streamly's Start button uses `CanStart`; Stop remains available while live.

Channel connection, a working powered desktop, game-owned upload, an explicit microphone and supported quality are required. Upload thresholds remain 1/3/6 Mbps for Low/Medium/High. Zero or invalid upload is offline. High still requires the existing GPU capability. Webcam absence is optional and never rejects start. PC boot validation is unchanged: a GPU remains optional for boot.

The desktop runtime receives the peripheral owner explicitly, waits for both PC and peripherals to initialize, and subscribes to connection changes. `SetUploadMbps` updates the existing game-owned upload value and refreshes the same validation. Required equipment/environment loss uses the existing stream abort/completion path; removing a webcam keeps the broadcast running. These events update visible Streamly without per-frame device searches or a second UI-owned readiness state.

There is no webcam face feed in this slice. The existing room preview and its scoped render texture are preserved for both camera states; no simulated webcam image is introduced.

## Saves

Save version remains **7**, with existing version 6 migration retained. The optional `Peripherals` snapshot contains microphone and webcam instance IDs. Save preflight partitions installed internal PC parts and external peripherals, then validates exact ownership, kind, duplicates and orphan connections before any live state changes. Restore uses the appropriate owner's anchor and restores each physical item once.

Older saves that omit `Peripherals` load an empty connection record. No connection is inferred from purchases or loose devices. If an old save predates the newly authored desk microphone item, the existing policy for omitted scene items marks it removed; a physical bought microphone can be connected normally. Active streams and open windows remain transient during load, and restore notifications cannot generate an extra stream summary.

## Presentation

The existing Streamly left settings panel now contains five compact readiness rows. Ready rows are green; failed required rows are red; an absent webcam is yellow. Microphone absence displays “Микрофон не обнаружен” and “Подключите микрофон к компьютеру.” The approved shell, app chrome, icons, other applications and 708×399 room preview retain their existing design. The incremental authoring entry rebuilds only the Streamly body and adds the explicit peripheral scene references.

## Verification

The initial regression test failed because a registered channel and working PC could start without a microphone. After implementation it passed in the targeted run.

Added tests cover domain start/readiness boundaries, no microphone/channel/internet, optional webcam, selected quality, change events, microphone loss during Starting/Live, webcam loss while live, unique identities, atomic snapshot rejection, physical hand/socket transfers, repeated save/load and legacy records. Existing stream completion/save fixtures now explicitly provide a physical microphone or a domain-owned test connection where appropriate.

Full suite on Unity **6000.6.0f1**: **739 passed / 739 total, 0 failed, 0 skipped**, 46 fixtures, 108.62 seconds. This includes all 672 previously green cases and 67 added cases: 39 peripheral/readiness domain cases, 7 physical connection cases, 19 save cases, the initial missing-mic regression and the real GL visual journey. The latter also proves live Streamly refresh, real E socket input, purchase/delivery not implying connection, no-camera start, optional camera loss, mandatory microphone loss and unchanged boot without a GPU.

Evidence: [full results](<E:/GO-Live-Peripheral-audit-20260925/full-suite.xml>), [Unity log](<E:/GO-Live-Peripheral-audit-20260925/full-suite.log>), [initial failing regression](<E:/GO-Live-Peripheral-audit-20260925/microphone-red.xml>). Independent read-only code review found no confirmed high/medium issues.

All following captures are actual **1920×1080 Game View**, inspected in both languages; the capture test also checks translated text, truncation, readiness marker states and Start availability. The full suite recaptured the previous Desktop/PC journey as well.

| State | Russian | English |
|---|---|---|
| Everything ready | [RU](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-ru-01-everything-ready.png>) | [EN](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-en-01-everything-ready.png>) |
| Microphone missing | [RU](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-ru-02-microphone-missing.png>) | [EN](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-en-02-microphone-missing.png>) |
| Optional webcam missing | [RU](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-ru-03-webcam-missing.png>) | [EN](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-en-03-webcam-missing.png>) |
| Internet missing | [RU](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-ru-04-internet-missing.png>) | [EN](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-en-04-internet-missing.png>) |
| Unsupported quality | [RU](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-ru-05-unsupported-quality.png>) | [EN](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-en-05-unsupported-quality.png>) |
| Live with webcam | [RU](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-ru-06-live-with-webcam.png>) | [EN](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-en-06-live-with-webcam.png>) |
| Live without webcam | [RU](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-ru-07-live-without-webcam.png>) | [EN](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-en-07-live-without-webcam.png>) |

[Physical socket and E prompt](<E:/GO-Live-Peripheral-audit-20260925/screenshots/peripheral-world-connected.png>).

Existing unrelated generated project files and the pre-existing TMP fallback changes were restored to their initial working-copy state and excluded from the commit.

Evidence directory: `E:\GO-Live-Peripheral-audit-20260925`.
