# Final whole-change review — GO! LIVE live-play correction

Reviewed 2026-09-26 against baseline `a030b9f` and the current working tree on `claude/sharp-wozniak-iakuny`. Read-only source review: no Unity launch, source/index/HEAD/branch edits, reset, or merge. This audit file is the only reviewer-written artifact.

## Strengths

- Responsibility and state ownership stay in ordinary C#: ViewerDailyLife derives personal context, ReactionSelector owns transient conversation selection/reservations, ViewerUtterancePlanner owns permitted content, and ChatDirector owns generation/publication. VoiceInputBehaviour remains a Unity lifecycle/capture/settings bridge. No new gameplay singleton or service locator was introduced.
- The core and planner share the daily-context gate (`Assets/Game/Scripts/Viewers/ViewerCore.cs:184`, `ViewerUtterancePlan.cs:140`). A personal question outranks a greeting. ViewerDay, OwnLine, and generated OtherViewerSaid text are excluded from authority for specific streamer/game facts (`ViewerUtterancePlan.cs:181`). Deterministic daily data and authored profile options remain separate from persistent witnessed memories/promises.
- All conversational scheduling paths converge on continuation derivation and pending reservation (`ReactionSelector.cs:530`, `:552`). Publication advances the actual selected origin; cancellation, queue rejection/eviction, departure, expiry, and discard release reservations through the existing Finished integration (`ViewerCore.cs:209`). Initial answers count toward the bound.
- Warmup and retired ordinary requests share the adapter limit until actual task completion (`ChatDirector.cs:274`, `:385`). Slow cancellation no longer permits a new broadcast to overlap requests or apply old measurements/results. The tests exercise both warmup and ordinary cancellation-resistant tasks.
- Production voice configuration is consistent between GL, bridge defaults, and scene authoring's use of those defaults. The selected turbo/GPU/beam path is connected to the real scene-owned recognition instance. Forced RU is the absent/invalid-choice default for RU UI; explicit saved language choices remain intact. Decoder context preserves captured samples/timestamps.
- The comparison harness retains required CPU/GPU configurations, external model roots, fresh output directories, flushed raw evidence, strict incomplete-run failure, production VAD, and separately labeled memory/latency limitations. Installation verifies the selected artifact hash. Diagnostic audio ownership and callback shutdown guards have focused regressions.
- The short replay drives the real scene-owned worker/feed/core and configured local model. It explicitly labels recorded input, records requests/daily context/plans/outputs, and preserves the failing follow-up assertion rather than weakening it to produce a green acceptance.

## Issues

### Critical (Must Fix)

None found in the reviewed source.

### Important (Should Fix)

No additional blocking runtime/code-quality defect found. Final full-suite evidence and the final reports were still controller-owned work in progress at this review snapshot; this is not an unconditional whole-task acceptance.

### Minor (Nice to Have)

1. **[P3] A session capture can mix sample rates under one WAV header.** `Assets/Game/Editor/Viewers/ViewerSessionRecorder.cs:154`–`:158` appends every captured block to one `_capture` list and overwrites `_captureRate` each time; `:78` writes the entire list with that last rate. If the microphone is restarted during one recording and the new default device uses a different supported rate, earlier audio plays at the wrong speed and its duration no longer matches the speech evidence. Per-phrase WAV files retain their own rates and are unaffected. For robust diagnostic recording, split the whole capture on a rate change (or resample to the first rate) instead of relabeling all prior samples. This is a development-recorder edge case, not a blocker for the retained fixed-rate corpus or short replays.

## Review scope and evidence

- Read requirements in `C:/Users/delus/.codex/attachments/146ddde2-ddb9-4726-9259-6a7fce9ec692/Pasted text.txt`, the live-play correction plan, and the code-reviewer rubric. Read the full tracked source/configuration diff and all new C# files from `final-review-package.txt` in bounded sections. Reviewed runtime, editor recorder/model loader/installer, authored profile data, harnesses, and tests, including inherited Claude work.
- Where concrete integration risks required context, read the complete bridge, core situation/publication hooks, director terminal paths, LocalizationContext's lazy initialization, and the scene author's VoiceInput construction. The lazy localization property avoids an assumed Awake-order defect. No unrelated repository exploration or Unity rerun was performed.
- Read both independent task reviews and verified their repaired code paths directly. Earlier STT diagnostic-retention/callback findings and four Task 2 findings are resolved; the withdrawn negation allegation is not repeated.
- Independently read result headers: `viewer-review-green.xml` is 370/370 passed, zero failed; `viewer-negation-red.xml` is 8/8 passed (despite its filename); `viewer-runtime-diagnostic.xml` is 2/2 passed. Read the added eight negation controls and two final diagnostic tests from current source. The 100-seed real-ephemeral, empty-budget, late-statement check is evidence of available continuation behavior, not evidence that the three real-model replays passed.
- Read the semantic STT audit and sampled the final third short-replay raw speech/publication rows. The original opening retains the greeting, plural address, and personal questions. A meaningful opening publishes, filler is silent, and the tiredness follow-up is transcribed without a question mark. The third replay's thread is present, epoch-matching, one turn old, and about 32.66 seconds old; it still chooses silence. All three short replays remain failures of their positive same-viewer-follow-up assertion. The code deliberately permits silence on a statement continuation (0.6 direct chance), so those outcomes alone are not proof of a broken continuation path; they also cannot be represented as successful end-to-end follow-up acceptance.
- The final full non-Explicit Unity suite was running at this snapshot. `docs/ViewerCoreLivePlayCorrectionReport.md` and `docs/ViewerCoreShortMicrophoneAcceptance.md` were intentionally pending. Their final evidence wording and the full-suite result require the controller's completion/delta check.

## Recommendations

Retain the three failed replay artifacts and state the exact limitation in the report; do not change probabilities or select only successful reruns to create an acceptance claim. Keep fresh human microphone acceptance separate from recorded replay and deterministic regression evidence. Address the optional recorder rate-change issue when expanding device-change diagnostic coverage.

## Assessment

**Code-quality verdict: Approved with one non-blocking recorder finding.**

**Ready to merge? Not assessed as ready at this snapshot.** The code has no remaining blocking finding from this whole-change review, but the requested complete non-Explicit suite and final evidence reports were pending. No main merge is authorized, and neither replay follow-up acceptance nor overall Viewer Core completion is established.

## Final evidence and documentation delta review

This section supersedes the pending-evidence statements and snapshot readiness assessment above. No runtime sources changed after the reviewed package. Reviewed the final two diagnostic tests and eight contraction cases, the replay's read-only thread/epoch/age telemetry and finite unset-value correction, both final reports, and the third replay assessment. No Unity execution or source/index/HEAD changes were performed by this reviewer.

- Independently parsed `full-nonexplicit.xml`: **1,185 discovered; 1,174 passed; zero failed; 11 skipped, all with label Explicit; 137.0871654 seconds.** The eight contraction cases and both runtime/configuration diagnostic cases are present and passed. The full run has finished; the previous full-suite evidence gap is closed. The log records normal shutdown as well as existing diagnostic noise; a green suite is not described as warning-free.
- `docs/ViewerCoreLivePlayCorrectionReport.md` accurately separates production corpus evidence, deterministic verification, three failed real-model replay follow-ups, and the unperformed fresh human microphone procedure. It explicitly states that no replay demonstrated the requested same-viewer follow-up, documents the first replay's repaired daily contradiction, avoids general reliability claims from one corpus, and does not claim Viewer Core complete.
- `docs/ViewerCoreShortMicrophoneAcceptance.md` provides the requested 60–90-second procedure and clearly marks it unperformed. It asks to retain failures rather than replace them with successful attempts. It distinguishes the monitor's available trace from the automatic replay's full model request/response evidence.
- The third replay's initial non-finite thread-age telemetry is disclosed in its assessment; raw evidence was retained unchanged. The fixture now emits -1 for an absent thread and for pre-audio timestamps. This is a diagnostic serialization correction and does not alter selection, speech recognition, probabilities, or past results.
- The report retains the non-blocking session-recorder sample-rate issue. The four unrelated initially modified files were reported byte-exact restored and excluded by the controller; this reviewer did not mutate or independently rehash those files.

**Final code-quality verdict: APPROVED, with the documented non-blocking P3 recorder limitation.** The required complete non-Explicit verification and final evidence reporting are now satisfied. This correction is suitable to commit on the current branch. This verdict does not authorize a main merge, declare the positive replay follow-up acceptance passed, or declare Viewer Core complete; the reports correctly retain those limits.
