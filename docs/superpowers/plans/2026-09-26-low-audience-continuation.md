# Low-audience conversation correction

**Goal:** A short personal question in an active exchange retains question-like reliability when recognition omits punctuation. Continue c8c5c3a on the current branch; do not merge main.

**Design:** Keep the existing plain C# selector as owner of the transient conversation (viewer, presence epoch, window, turns, pending answer). Add a bounded linguistic predicate in SpeechRelevance used only for conversation continuation by the selector and utterance planner. Do not mutate global speech acts, recognized text, saves, configuration, Whisper, or voice capture. No new abstraction, events, dependencies or persisted state is needed. Existing thread eligibility remains authoritative.

The user's supplied correction is the approved scope. A global question-classifier expansion would change unrelated speech; increasing all statement probability would erase useful silence. Use a contextual predicate instead. Short elliptical personal/interrogative forms receive the existing .95 question chance; ordinary acknowledgements retain .6. Limits, presence, cancellation and cooldown stay unchanged.

- [x] Add deterministic regression cases for all requested punctuation-free forms, punctuation parity, ordinary statement/filler, inactive/expired/absent/changed visit, maximum turns, pending answer, cooldown, group/name switching and unrelated topics. Prove the new question reliability assertion fails on the baseline.
- [x] Implement the narrow shared predicate and use it in selection and planning; run relevant Viewer tests. Keep unrelated classification unchanged.
- [x] Prepare the existing real-scene replay with predetermined seed inputs **1, 2, 3**, selected before any run. Retain unchanged corpus, actual adapters and 75-second timeline. Run each once and retain every outcome, model request/response and transcript.
- [x] Run complete non-Explicit Unity EditMode suite with zero failures; independently review code and evidence, correcting verified defects and rerunning affected checks if needed.
- [x] Update the report and 60–90 second fresh microphone procedure. Fresh human capture is prepared, not invented or silently substituted with playback. Preserve original dirty files, commit on current branch, no merge; do not declare Viewer Core complete.

**Verification commands:** Unity 6000.6.0f1 `-batchmode -projectPath "E:/GO! Live" -runTests -testPlatform EditMode -testFilter GoLive.Tests.ViewerLowAudienceContinuationTests` for regression RED/GREEN, Viewer fixture filter for broader checks; omit testFilter for full non-Explicit suite. For each replay explicitly select `GoLive.Tests.DesktopFlowPlayModeTests.SeventyFiveSecondRecordedConversationUsesTheProductionScenePipeline`, set GO_LIVE_SHORT_ACCEPTANCE_SEED and a unique GO_LIVE_SHORT_ACCEPTANCE_OUTPUT. Evidence root: `E:/GO-Live-LowAudience-audit-20260926`.

Outcome: 1213 non-Explicit tests passed, zero failures, 11 Explicit excluded. Three predetermined attempts ran exactly once: strict XML 1 pass / 2 fail. Both present thread partners continued; seed1 partner left, seed3 output unclear and later screenshot step failed. All failures retained. Fresh human microphone procedure prepared, not performed. Viewer Core is not complete. Commit retains the current branch without main merge.
