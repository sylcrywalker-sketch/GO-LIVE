# PC / Desktop visual polish

Visual pass on `codex/pc-desktop-vertical-slice`, starting at `1f9e5f6`. The final supplied brief and six reference images govern this pass. No merge to main.

## Presentation

- Blue wave desktop with the app column, two-column Start menu at the left edge, taskbar and an event-driven clock from the existing game clock.
- Common window chrome, light system applications, light Outline mail client, dark purple Streamly/Trich and dark amber Donation. Real Manrope font weights, compact controls and 0.12-second hover transitions.
- PC Build shows six hardware rows with code-native monochrome symbols, green checks, red required-component errors and a yellow optional-GPU warning. Verdict and explanation follow the rows. Existing physical models, ghost, picking and assembly remain in use.
- Trich separates channel overview from profile editing. Its default banner is black, avatar ID 0 is a user silhouette, and an empty description uses the localized default. Existing avatar IDs and saved profile values remain compatible.
- Outline presents existing account, inbox, read state and reports in a three-column mail layout. Stream reports remain attributed to Trich, matching the existing report body. No follower notification system was added.
- Hub lists the five supported apps from catalog data, includes search and excludes Hub/My Computer. My Computer reads actual drive capacity and use. Web adds bounded local Back/Forward history to its existing controlled navigation.
- Streamly reads channel identity from Trich and uses current quality/upload/hardware rules. An explicitly authored camera renders the actual room into its preview. The view owns and releases its render texture when hidden.
- Global stream UI contains viewer/subscriber/time counters at the top center and compact chat at the right. Donation alerts remain temporary; no global LIVE label or support total is rendered.

## Architecture and scope

The existing plain-C# state owners, save version and validation remain unchanged. Authoring is divided into shell, system apps, community apps and broadcast apps; all runtime scene references remain serialized explicitly. New runtime components draw panels, symbols and hardware rows, or subscribe to the existing clock. No runtime scene searches, singleton, locator, global mutable state or simulated hardware meters were introduced.

The only domain addition is `StreamSession.Disconnect()`, needed for the requested real Disconnect action. It is idempotent while offline, rejects Starting/Live/Stopping, and clears only the transient channel link. Quality, history, statistics and support are preserved. Six new cases cover those boundaries; the initial RED run produced exactly six expected failures, then all 24 stream-session tests passed after implementation.

## Exact icon identities

| App | Approved source | Identity |
|---|---|---|
| My Computer | Existing approved `MyComputer.png` | Gray tower and monitor |
| Streamly | `C:/Users/delus/Downloads/streamly.png` | Purple square and magenta/purple play triangle |
| Trich | Existing approved `Trich.png` | Purple speech icon |
| Outline | Existing approved `Outline.png` | Blue envelope |
| Donation | Existing approved `Donation.png` | Orange/yellow dollar speech bubble |
| Hub | `E:/pack references/320c231c-6572-48c4-b7ab-deeceda3e3d2.png` | Red/green polygon with blue center |
| Web | `E:/pack references/d805ac91-9131-4227-8f3d-fb73520fe8a8.png` | Blue/green globe |

All surfaces use the same catalog sprites. The seven source byte hashes are locked by `DesktopContentTests`. An editor import treatment removes outside-connected neutral export mattes and documented empty screenshot margins from the five opaque exports, preserving enclosed white artwork. PNG source bytes are unchanged by this treatment. RGBA32 import preserves the derived transparency; GPU-readback checks verify it survives import. Streamly SHA256: `68282D5D235C9DF9956AB3FE706EE92DE32FE040C0125FF1397CC30845CCBB0D`.

## Changed files

All paths below are relative to the repository. Added C# files include their Unity `.meta` files.

| Area | Files |
|---|---|
| Authored scene and localization | `Assets/Game/Scenes/GL.unity`; `Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset` |
| Shared authoring | `Assets/Game/Editor/Desktop/DesktopUiAuthoring.cs`, `DesktopSceneAuthoring.cs`, `DesktopShellAuthoring.cs`, `DesktopAppAuthoring.cs`, `DesktopLocalizationAuthoring.cs`, `DesktopIconImport.cs` |
| App and PC authoring | Same folder: `DesktopCommunityAuthoring.cs`, `DesktopBroadcastAuthoring.cs`, `PcWorkbenchUxAuthoring.cs`, `ShellPolishLocalization.cs`, `CommunityPolishLocalization.cs`, `BroadcastPolishLocalization.cs`, `PcPolishLocalization.cs` |
| Desktop presentation | `Assets/Game/Scripts/Desktop/DesktopShellView.cs`, `DesktopWallpaper.cs`, `DesktopPanelGraphic.cs`, `DesktopGlyphGraphic.cs`, `DesktopTrayView.cs`, `ChannelAvatarGraphic.cs`, `TrichView.cs`, `OutlineView.cs`, `HubView.cs`, `WebView.cs`, `StreamlyView.cs`, `DonationView.cs`, `StreamOverlayView.cs` |
| PC presentation | `Assets/Game/Scripts/PcBuilding/PcHardwareGlyph.cs`, `PcHardwareStatusView.cs`, `PcWorkbenchHudView.cs`, `PcWorkbenchText.cs`, `PcBuildSlotMarkerView.cs`, `PcWorkbenchBehaviour.cs`; `Assets/Game/Prefab/PC/Materials/M_PcInstallGhost.mat` |
| Small domain addition | `Assets/Game/Scripts/Desktop/StreamSession.cs` |
| Icons | `Assets/Game/Art/Desktop/Icons/{Streamly,Hub,Web}.png`; import metadata for all seven approved icons |
| Verification | `Assets/Game/Tests/Editor/DesktopContentTests.cs`, `DesktopFlowPlayModeTests.cs`, `StreamSessionTests.cs` |
| Documentation | This report; `docs/superpowers/plans/2026-09-25-pc-visual-polish.md` |

## Data limitations

The accepted model has no average-viewer integral, category/FPS/resolution controls, microphone/webcam configuration, webcam image or receipt message text. This pass does not invent these values or new gameplay systems. Trich shows the three available metrics; Streamly shows actual supported controls and a room preview. Donation shows actual sender/amount/history. Mail dates are not fabricated. The taskbar displays the actual game day, rather than a fictitious calendar date.

## Verification and evidence

Final verification: **672 passed / 0 failed / 0 skipped, 43 fixtures**, Unity 6000.6.0f1, 93.37 seconds. This retains all 666 baseline cases and adds six Disconnect cases. See [full EditMode results](E:/GO-Live-PC-polish-audit-20260925/full-editmode-02.xml) and [runner log](E:/GO-Live-PC-polish-audit-20260925/full-editmode-02.log).

The separate [PlayMode runner](E:/GO-Live-PC-polish-audit-20260925/standalone-playmode.xml) finds 0 cases, as before. PlayMode integration coverage resides in Editor UnityTests that explicitly enter and leave PlayMode. The full suite includes the real first-broadcast journey, seven applications, storage, save/load, power, input ownership, PC assembly, shop/orders, apartment HUD and lighting.

The real-scene flow passed with all 43 captures; [flow output](E:/GO-Live-PC-polish-audit-20260925/final-flow-output.txt) records each one. [Screenshot manifest](E:/GO-Live-PC-polish-audit-20260925/final-screenshot-manifest.csv) records dimensions, times and SHA256 hashes. The flow checks actual raycast controls, source icon identity, optional/installed GPU marks, long profile layout, read state, window uniqueness, persistence and untranslated/truncated labels. Test pacing explicitly waits for the character's stance/eye-height transition before aiming; no gameplay/input logic was relaxed.

Console: no unexpected managed game warnings/errors or C# compilation errors in the final passing suite. The existing `Props/SM_Shelf_001` negative-scale BoxCollider warning remains and is matched explicitly by the real-scene test. The editor log still contains the pre-existing license access-token refresh message; the licensed runner completes successfully. Native AssetDatabase/UDS errors occurred in earlier intermediate editor runs, but no `[Error]` entries occur in the final full-suite log.

Independent source review found caption/requirement clipping, which was corrected and verified in the captures. Final visual inspection found no overlapping or clipped labels in the required RU/EN states; missing gameplay data remains the explicit limitation above.

## Game View screenshots

43 real 1920×1080 Game View PNGs are saved in [screenshots](E:/GO-Live-PC-polish-audit-20260925/screenshots). Captures use Unity `ScreenCapture`, real authored UI and the physical scene. No composed mockups or replacement screenshots are used. The preview is a live render of the existing room; the room's furniture and lighting are unchanged.

| Required state | Russian | English |
|---|---|---|
| Empty desktop | [Desktop](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-04-desktop.png) | [Desktop](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-01-desktop.png) |
| Start menu | [Start](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-05b-start-menu-installed.png) | [Start](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-01b-start-menu-installed.png) |
| PC Build | [Build](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-18-pc-build.png) | [Build](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-08-pc-build.png) |
| Streamly idle | [Idle](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-11b-streamly-idle.png) | [Idle](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-app-streamly.png) |
| Streamly live | [Live](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-12-streamly-live.png) | [Live](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-02-streamly-live.png) |
| New Trich channel | [Default](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-08a-trich-default.png) | [Default](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-trich-default.png) |
| Configured Trich | [Profile](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-08-trich-profile.png) | [Profile](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-app-trich.png) |
| Outline inbox | [New inbox](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-07-outline-created.png) | [Inbox with report](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-app-outline.png) |
| Outline report | [Report](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-15-outline-stream-summary.png) | [Report](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-app-outline.png) |
| Hub | [Hub](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-05-hub.png) | [Hub](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-app-hub.png) |
| One physical drive | [Computer](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-06-my-computer.png) | [Computer](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-app-mycomputer.png) |
| Donation | [Settings](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-09-donation-settings.png) | [Support history](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-app-donation.png) |
| Web | [Web](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-10-web-local-result.png) | [Web](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-app-web.png) |
| Global HUD and chat | [World](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-14-live-overlay-world.png) | [World](E:/GO-Live-PC-polish-audit-20260925/screenshots/en-03-live-overlay-world.png) |

Additional captures cover physical monitor power/boot, seated state, held GPU placement and installed GPU. [Maximum-length channel profile](E:/GO-Live-PC-polish-audit-20260925/screenshots/ru-trich-long-profile.png) verifies the 32-character name wraps and the 240-character description scrolls without overlapping statistics.

Visual review against the supplied references rejected and corrected clipped captions, overlapping inventory text, invisible hardware symbols, opaque icon mattes, an overly bright ghost and the preview's initial wall-facing pose. The final composition preserves the blue desktop, restrained application branding, light mail and six-row physical PC status.

## Worktree preservation

The initial worktree/index and modified tracked files were backed up in the audit directory before editing. Pre-existing phone, project-file, scene/catalog and UserSettings changes remain outside this feature commit. Generated project files and the dynamic TMP fallback are restored to their backed-up initial contents after verification. Scene/catalog staging uses the reviewed semantic delta from this pass; original working files remain in place. The semantic audit reports zero unresolved scene-local references.

Tests describe the preserved working tree. The baseline commit already contains five duplicate phone localization entries that the user's initial uncommitted catalog had removed. That pre-existing cleanup is deliberately excluded from this visual commit, so a clean checkout needs the same earlier cleanup to reproduce the catalog checks. This pass adds 59 localization entries and changes 15 existing entries without taking ownership of that unrelated fix.

## Commits

- Baseline: `1f9e5f6`.
- Implementation: `dc89f883f7541cb9310e04e8fa394d9a2670d68e` — 63 files, including Unity metadata and the plan.
- Final implementation/report hashes: [commit record](E:/GO-Live-PC-polish-audit-20260925/commits.txt).

Branch remains `codex/pc-desktop-vertical-slice`; no merge to main and no push.
