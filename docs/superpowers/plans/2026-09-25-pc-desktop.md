# PC Desktop Vertical Slice Implementation Plan

> For agentic workers: use superpowers:subagent-driven-development for bounded task review and superpowers:dispatching-parallel-agents for the independent domain tasks below. Shared Unity runs and scene mutations belong to the primary agent only.

**Goal:** Deliver the approved PC/monitor/build/desktop/stream loop in the real GL scene.
**Architecture:** Existing PcAssembly remains hardware authority. Small plain C# owners expose commands and events; serialized Unity bridges own input/presentation. Save preflight validates all domain snapshots before mutation.
**Tech Stack:** Unity 6000.6.0f1, C# 9, uGUI/TMP, existing Input System and NUnit fixtures.

## Global constraints

- Work in the already approved `E:\GO! Live`, branch `codex/pc-desktop-vertical-slice`; preserve the dirty baseline recorded in `E:\GO-Live-PC-slice-audit-20260925`.
- Do not reset, discard unrelated work, or merge to main.
- No Service Locator, Singleton, global static runtime state, runtime hierarchy recovery, reflection dependency recovery, or ScriptableObject runtime databases.
- Do not modify Shop/PhoneShop/Shop*/PracticalLight production code.
- One owner per mutable state; all critical rules testable without Play Mode.
- Final icons and their mapping are in `docs/PcDesktopVerticalSliceDesign.md`; preserve source bytes.
- Subagents do not launch Unity or commit shared files. Primary serializes compilation/tests, scene authoring and commits.
- Approved design and user specification govern all tasks. Root handles shared localization, scene, save and integration.

## Task 1 — PC interaction/session

Owner: pc-session worker. Files: new `Assets/Game/Scripts/Desktop/PcSession.cs`, `PcSessionBehaviour.cs`, `PcCaseInteractable.cs`, `PcMonitorInteractable.cs`; change existing Interact/IInteractable.cs, PlayerInteractor.cs, HUD/InteractionPromptView.cs, UI/GameUiInputRouter.cs, PcBuilding/PcWorkbenchBehaviour.cs, PcScreenView.cs; tests `PcSessionTests.cs`, `PcSessionPlayModeTests.cs`.

Domain API in `GoLive.Desktop`: `PcPowerState { Off, Booting, Running }`, `PcUsageState { Standing, Seated, Focused }`, `PcSession` with `Power`, `Usage`, `MonitorOn`, `ScreenActive`, `event Action Changed`, `bool TryPowerOn(PcCapabilities)`, `void PowerOff()`, `void ToggleMonitor()`, `bool Sit()`, `bool Focus()`, `bool Back()`, `void Reset()`, `void Tick(float seconds)`, `void HardwareChanged(PcCapabilities)`. Session contains no Unity objects. Boot 1.5 seconds; rejects missing Desktop capability using the capability diagnostics; powering off unfocuses but preserves seated usage and independent monitor power. `PcSessionBehaviour.Session` exposes the owner, `IsSeated`, `HandleBack()`, `PrepareForBuild()` and `TryTogglePower()` connect to scene/runtime.

- [x] Write tests and compilable API skeletons; report ready for primary's red run.
- [x] Implement domain and bridges; preserve existing inputs. E append a general Primary interaction fallback after pickup, F Use, B Special. Add general object label interface to prompts, with backward-compatible constructor.
- [x] Route Esc and prevent Q/Tab ownership conflicts. Serialize PC, player, carry, camera, seat anchor, monitor collider and actions; no runtime Find.
- [x] Test boot success/refusal, monitor independence, sitting while off, repeated cycles, focus loss, disable/re-enable, cursor/control/pose restoration. Existing PC tests must still pass except intentional monitor power expectation updated with explicit tests.

## Task 2 — Catalog/storage/windows

Owner: desktop-shell worker. Files: new `Assets/Game/Scripts/Desktop/DesktopAppCatalog.cs`, `DesktopStorage.cs`, `DesktopWindows.cs`, snapshot types; change PcComponentSpec.cs and StarterHdd config only; new `DesktopStorageTests.cs`, `DesktopWindowsTests.cs`.

Public types in `GoLive.Desktop`: `DesktopAppId { MyComputer=0, Streamly=1, Trich=2, Outline=3, Donation=4, Hub=5, Web=6 }`. Catalog ScriptableObject immutable config definitions: Id, NameKey, DescriptionKey, Icon, SizeMiB, Preinstalled. Plain C# constructors accept IReadOnlyList<DesktopAppDefinition>. Storage uses explicit `DesktopDrive` values containing persistent item id, slot id, CapacityMiB; exposes Drives, InstalledContent and `IsInstalled(DesktopAppId)`, `TryInstall(DesktopAppId, string driveId=null)` returning localization error key or null success, Capture/Validate/Restore. Drive letters C onward ordered by assembly slot layout; disconnected disk content retained, inaccessible until same item returns. Preinstalled MyComputer/Hub/Web also account space; root supplies drive data. Content records designed to admit future content ids with validated config size, not fake filesystem. Windows owns open/minimized/activation order, has Changed, Open, Close, Minimize, Activate, IsOpen, IsVisible and ActiveApp; no UI dependency.

- [x] Write tests and API skeletons; report ready for red run.
- [x] Implement catalog validation, atomic install, capacity accounting, stable physical identity, strict snapshot validation and windows uniqueness/order.
- [x] Extend `PcComponentSpec` with serialized `storageCapacityMiB` and `StorageCapacityMiB`, StarterHdd=327680. Preserve old 4-argument Validate API and all existing fixtures; 0 legacy capacity remains valid hardware but not an install target.
- [x] Test C/D, install/uninstall, insufficient storage no mutation, unplug/replug, corrupt saves and catalog, duplicate/minimized/reopened windows. Report exact final API to root.

## Task 3 — Accounts and stream

Owner: streaming worker. Files: new `Assets/Game/Scripts/Desktop/OutlineAccount.cs`, `TrichChannel.cs`, `DonationAccount.cs`, `StreamSession.cs`, associated snapshots, `DesktopAccountTests.cs` / `StreamSessionTests.cs`. Does not edit other workers' files.

Types in `GoLive.Desktop`. Account APIs return null for success or localization error key. Outline owns Address, Messages, CreateAddress(username), Receive(id,subjectKey,bodyKey,bodyArguments), MarkRead(id), Capture/Validate/Restore. Trich owns Email, Name, Description, AvatarId (0..3), ChannelCode, totals; Register(outline,email), EditProfile(name,description,avatar), CompleteStream(summary), snapshots. Code stable across repeated opens/save/load. Donation owns config/name/history and idempotent Receive, snapshots. StreamSession depends on TrichChannel and DonationAccount by constructor; Connect(code), CheckStart(PcCapabilities,bool powered,float uploadMbps), Start(...), Stop(), Abort(), Tick(float). State Offline/Starting/Live/Stopping, Quality Low/Medium/High, IsConnected, DurationSeconds, Viewers, Followers, DonationCents, bounded Chat; Changed, ChatAdded, Completed. Low upload>=1; Medium>=3; High>=6 plus GamingGraphicsAvailable; all require powered desktop. Starting=.75s; Stopping=.25s. Deterministic gameplay activity, no network. Lost capabilities handled via RefreshEnvironment or root calls Abort after eligibility loss. All timers reject invalid/nonfinite deltas. Transient stream not serialized; stats committed once per finished session.

- [x] Write tests and API skeletons; report ready for red run.
- [x] Implement validation and atomic restore with bounded messages/history and unique ids. User text length/markup handled safely.
- [x] Test exact Outline→Trich→Streamly dependency flow, invalid and duplicate commands, quality/environment loss, bounded activity, post-stream summary and persistence corruption.
- [x] Report final API and localization keys to root; avoid Unity types except existing PcCapabilities data dependency.

## Task 4 — Composition and persistence

Owner: primary. New DesktopRuntimeBehaviour creates domains and connects explicit references. Hardware event updates storage/session once, not each frame. Stream tick uses scaled time; screen/chat rendering change-driven. New DesktopSnapshot validates against all saved storage items (including disconnected), then applies. Save v7 accepts v6 with fresh desktop migration, rejects malformed v7 before any mutation. Existing save controller mutation path stays unchanged except preflight and desktop apply, state reset before successful apply and world pose handling. New tests in DesktopSaveTests and DesktopIntegrationTests.

- [x] Consume final worker APIs and write domain composition/save tests before implementation.
- [x] Implement constructor/lifecycle/subscription wiring, installed-content gates, completed-stream mail, power-loss abort.
- [x] Add strict migration and explicit serialized `_desktop` reference in GameSaveController; avoid any Shop changes.
- [x] Run area tests with actual XML evidence.

## Task 5 — Desktop, app UI, scene authoring

Owner: primary. New focused views: DesktopShellView, DesktopWindowView, MyComputerView, HubView, OutlineView, TrichView, StreamlyView, DonationView, WebView, StreamOverlayView, MonitorSurfaceView. Shared UI style factory may be editor-only to author serialized hierarchy. New `Assets/Game/Editor/Desktop/DesktopSceneAuthoring.cs` constructs new prefab/config and binds GL explicit references; never automatic InitializeOnLoad rewrites. Own localization entries use `desktop.*`, `stream.*`, and `pc.session.*`.

- [x] Import original seven files into Art/Desktop/Icons unchanged, author Sprite import settings (no destructive image processing). Use light icon backing for opaque source assets.
- [x] Author quiet blue-grey late-2000s fictional shell at 1920x1080, Start, icons, taskbar, window titlebars and coherent focus, real app fields/buttons/inboxes and storage bars.
- [x] Author physical monitor boot/black/desktop surface with one domain owner; seated LMB focus. Author right-side live stats/chat overlay independent of other HUD fading.
- [x] Capture and visually inspect actual Game View: standing prompts, seated off/boot/on, shell and all seven apps, full first-stream flow, RU/EN. Correct clipping/layout issues before acceptance.

## Task 6 — Workbench UX / integration regression

Owner: primary after task 1. Preserve assembly/ghost/camera transactions. Extend PcWorkbenchText for actual watt budget and compatible/incompatible/dependency presentation; clamp slot card around target and keep physical case unobscured. Author HUD styling only.

- [x] Add focused expectations for watt budget/reasons and target card location; run old workbench suite.
- [x] Diagnose baseline nested PlayMode startup timeout in the two PhoneShop readability tests, keep every assertion and production semantics unchanged.
- [x] Run full EditMode + separate PlayMode runner; report exact discovered counts (baseline PlayMode=0 standalone tests).
- [x] Review hot paths, subscriptions, scene disable/load, restore original input and cursor, all v6/v7 migration tests.
- [x] Get independent code review, resolve actionable findings, commit only slice-owned changes, attach final 25-point report with evidence and limitations. Never merge main.

## Test command

Primary serially invokes:
```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -projectPath 'E:\GO! Live' -runTests -testPlatform EditMode -testFilter '<area filter>' -testResults 'E:\GO-Live-PC-slice-audit-20260925\<run>.xml' -logFile 'E:\GO-Live-PC-slice-audit-20260925\<run>.log'
```
Unity starts detached on this Windows host; wait on the matching process/result XML, inspect each failure and exact counters. Do not equate shell launch exit 0 with passing tests. All original generated csproj and TMP fallback files have pre-test copies and test-induced changes are removed without touching user edits.

## Progress

- [x] Audit/reference mapping and design approved by user.
- [x] Tasks 1–3 red/green.
- [x] Tasks 4–6 integration, visual verification and final review.
