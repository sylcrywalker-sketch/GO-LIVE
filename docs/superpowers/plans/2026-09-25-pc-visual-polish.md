# PC/Desktop visual polish implementation plan

**Goal:** Apply the user's final visual brief and six authoritative references to the accepted vertical slice.
**Architecture:** Preserve plain C# owners and save schema. Change authored UI and explicit presentation components. Only minimal missing commands such as disconnect may extend a domain; no invented gameplay metrics.
**Tech stack:** Unity 6000.6.0f1, uGUI/TMP, existing Editor authoring and real GL screenshot test.

The supplied final brief is the approved design; no second design approval is needed. Work stays in `codex/pc-desktop-vertical-slice`, baseline HEAD `1f9e5f6`. Dirty work is backed up in `E:/GO-Live-PC-polish-audit-20260925` and will remain separate in the commit.

## Visual contract

Blue waves (#07549B, #149BE8, #57C5EE), blue glass taskbar/titlebars (#123654), dark app paper (#141820), light mail paper (#F4F7FA), purple (#7950B6), amber (#D9A54B). Manrope Medium body 18–20, Semibold headings 23–28, utility 15–17; no oversized slogans. Compact controls, 1px borders, 4–6px corners, restrained hover/focus 0.12s. Wallpaper has no text. All seven app identities follow the NEW explicit mapping.

## Tasks

- [x] Root: audit supplied assets; preserve exact approved icon artwork. Swap Hub/Web mapping and find/use authoritative purple Streamly. Source geometry/sprite regions may remove unused surrounding margins; do not redraw app icons.
- [x] Root: rebuild shell, blue wave graphic, titlebars, compact taskbar and clock bound to GameClock, Start two columns and real shutdown. Shared UI helpers provide light/dark palette and compact typography, explicit references only.
- [x] Community worker: separate Trich overview/edit presentation with default silhouette/plain banner and read-only real statistics; no fake average when domain lacks viewer integral. Outline narrow inbox navigation, mail rows and readable selected report. Own new DesktopCommunityAuthoring and the two existing views; publish localization lines separately.
- [x] Broadcast worker: Streamly dark setup/preview, real channel identity, quality/environment controls; show only existing supported data. Donation dark history/settings, compact top-center stream counters plus right chat with colored names. Own new DesktopBroadcastAuthoring plus existing Streamly/Donation/StreamOverlay views and minimal Disconnect if required; publish localization separately.
- [x] Root: compact Hub data-driven authored cards including Web, search; simple drive rows and Web Back/Forward/Home using local navigation. Hardware status rows with monochrome icons, green checks / red required / yellow optional; preserve ghost and installation.
- [x] Root: run authoring serially, update presentation expectations to new actual controls, then real 1920×1080 RU/EN flow captures for every required state including default/configured Trich. Use current 666-test baseline and preserve all domain assertions.
- [x] Independent reviewer: check state/input/save regressions and compare screenshots with authoritative targets. Root resolves findings, runs full EditMode plus standalone PlayMode, records exact counts/console/limitations.
- [x] Root: preserve baseline edits via semantic scene/catalog blobs, restore generated baseline files, commit visual change and evidence without merging main.

## Shared authoring API and boundaries

`DesktopUiAuthoring` keeps Rect, Panel, Text, Label, Button, Input, Icon and serialized Set/String/Int/References. Root adds `WithTheme(bool dark, Color accent)` returning themed helper; dark helpers use white text, muted grey, dark fields/buttons, accent primary. Workers can use existing APIs before this addition. Methods accept `DesktopUiAuthoring ui, DesktopRuntimeBehaviour runtime, Transform body`; normal app content is 1180×706 under common window chrome. Root may size a containing shell with room for chat, without changing those local coordinates.

New authoring components are editor-only; app views retain existing serialized test field names where possible. Workers never launch Unity, write GL/catalog, commit, or modify shared UI helpers. Root serializes all asset authoring/test runs. `superpowers:dispatching-parallel-agents` is used for independent file ownership.

## Verification command

Unity batch `-runTests -testPlatform EditMode -testResults E:/GO-Live-PC-polish-audit-20260925/full-editmode.xml -logFile .../full-editmode.log`; separate PlayMode runner. Set `GO_LIVE_VISUAL_OUTPUT` to this audit's screenshots. Inspect XML, not launcher exit code. Presentation geometry is judged from real PNGs; previous acceptance screenshots are comparison baseline, not evidence of this pass.
