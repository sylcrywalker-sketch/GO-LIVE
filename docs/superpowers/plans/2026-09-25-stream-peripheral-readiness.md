# Stream peripheral readiness implementation plan

> Historical implementation plan. Its mandatory-microphone start/abort rule is superseded by [the readiness correction](2026-09-25-stream-peripheral-readiness-correction.md): the fictional microphone is optional, independent of real voice capture, and its removal never interrupts a broadcast. See the updated `docs/StreamPeripheralReadinessReport.md` for current behavior and validation.

> Execute the approved request in this session. Root owns domain validation, persistence, integration and Unity runs. Independent workers own world connection bridges and compact Streamly presentation. No worker launches Unity or edits the shared scene.

**Goal:** Start a normal broadcast only when the actual connected microphone, Trich connection, network and selected PC quality are ready; a webcam remains optional.

**Architecture:** A two-device plain-C# `PcPeripherals` owns connection identities. `PcPeripheralsBehaviour` moves the same real item between hands and explicitly authored sockets using the existing Installed location. `StreamSession.EvaluateReadiness` produces the result used by both `CheckStart`/`Start` and Streamly. The existing save graph records and validates peripheral ownership separately from internal PC components.

**Tech stack:** Unity 6000.6.0f1, C#, uGUI/TMP, Editor NUnit/UnityTests. Baseline `222c6cb` contains the accepted desktop pass and the user's cleanup. Branch `codex/stream-peripheral-readiness`; main remains untouched.

## Constraints and design decisions

- Preserve desktop/Streamly composition and PC boot rules. No simulated OS devices, sliders, cables, load meters or generated camera feed.
- A connected device is one real, compatible `WorldItem` in Installed location whose instance ID belongs to this PC's microphone or webcam socket. A catalog entry, owned purchase, loose room item or item in inventory/hands is insufficient.
- Two explicit sockets are preferable to proximity detection, which would count unrelated apartment equipment, or extending the internal PC assembly, which would mix boot components and peripherals.
- The existing microphone on the desk becomes an explicitly authored initial connection; it can be disconnected back into the player's hands. The existing webcam model and shop product become a real deliverable item. No free webcam connection is inferred.
- Microphone removal interrupts an active normal stream using the existing environment-failure path. Webcam removal does not interrupt. Camera presence is reported without inventing a face-camera image; there is no existing webcam feed implementation.
- Internet remains the existing game-owned upload value. Zero is offline; available speed must meet the selected quality's current requirement. A controlled runtime setter notifies readiness when it changes.
- Save version remains 7 (version 6 migration remains supported). The new optional peripheral snapshot defaults to no connections for old saves. Validate device types, unique IDs and the exact Installed item graph before any mutation; no historic purchase/scene proximity migration creates a connection.

## Ownership contract

| System | Responsibility and state | API/events | Persistence and tests |
|---|---|---|---|
| `PcPeripherals` | Microphone/webcam connection IDs; no Unity dependencies | `HasMicrophone`, `HasWebcam`, `GetConnectedId(kind)`, `CanConnect(kind,id)`, `TryConnect(kind,id)`, `TryDisconnect(kind)`, `Capture`, `Validate(snapshot, installed)`, `Restore`; `Changed` | Two IDs; duplicate/type/ownership/atomic restore cases |
| World bridge | Serialized sockets/anchors and actual item transfers | `State`, `IsReady`, `TryConnectCarried`, `TryDisconnectToCarry`, `TryGetRestoreAnchor`, `Restore` | No parallel saved state; hands/slot/rollback and repeated-load coverage |
| StreamSession | Authoritative preflight and stream transitions | `EvaluateReadiness(capabilities,powered,upload)`, existing `CheckStart`, `Start`, `RefreshEnvironment`, `Changed` | Connection is transient as before; mic/network/channel/quality/webcam matrix |
| Streamly | Five compact rows and Start enabled state | Read-only result display through existing change events | No saved UI state; RU/EN captures and actual button checks |

## Execution

- [ ] Add an absent-microphone regression using the current real StreamSession API and run it RED.
- [ ] Implement the two-device owner and immutable readiness result; extend StreamSession with injected peripherals and one validation path. Update existing test fixtures to explicitly connect a microphone where they test unrelated streaming behavior. Run domain cases GREEN.
- [ ] Implement world sockets and transfer bridge with explicit dependencies, ItemDefinition peripheral kind and scene-authored initial microphone. Reuse real item models; enable existing webcam product fulfillment. Cover connect/disconnect and stale/duplicate ownership.
- [ ] Add optional peripheral snapshot to GameSaveData, capture, prevalidate and restore. Split Installed items between internal PC and peripheral owners, reject orphan/duplicate/wrong-type records, preserve old saves. Verify through GameSaveController round trips and malformed-save atomicity.
- [ ] Bind the bridge to DesktopRuntime; refresh preflight on peripheral, power, hardware and upload changes. Keep stream abort/completion suppression during save restore.
- [ ] Add compact readiness to existing Streamly panel, domain-driven Start enablement and localized required/optional messages. Retain real room preview and do not fabricate a webcam image.
- [ ] Author explicit GL bindings serially. Extend real Game View journey with seven requested states in RU/EN, including connected webcam and no-webcam live streams. Verify all 14 new captures at 1920×1080.
- [ ] Run the complete suite from the 672-case baseline, inspect XML/logs and all captures. Resolve review findings, restore generated project-file dirt and commit only this pass with report/evidence links.

## Verification commands

Unity `-batchmode -projectPath "E:/GO! Live" -runTests -testPlatform EditMode -testResults "E:/GO-Live-Peripheral-audit-20260925/full-editmode.xml" -logFile "E:/GO-Live-Peripheral-audit-20260925/full-editmode.log"`; narrow runs use the relevant fixture filter. Separate PlayMode discovery is also recorded. Set `GO_LIVE_VISUAL_OUTPUT=E:/GO-Live-Peripheral-audit-20260925/screenshots` for the real-scene flow. XML completion and screenshots, not launcher exit code, are acceptance evidence.
