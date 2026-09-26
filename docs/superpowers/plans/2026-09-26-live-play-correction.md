# Viewer Core live-play correction plan

> **For agentic workers:** Use superpowers:subagent-driven-development with one implementation worker at a time; root owns Unity execution and the evidence review.

**Goal:** Finish the existing working-tree correction: choose the smallest adequate STT candidate from the newest real corpus, then complete daily-life context and bounded low-audience conversation.

**Architecture:** Preserve Claude's existing VoiceRecognition/Whisper adapter and speech/event/selector/planner/director ownership. Daily life is deterministic, harmless viewer-side context, separate from witnessed memories, promises and authoritative gameplay. ConversationThread remains transient and bounded. No new gameplay manager, service locator or alternate inference pipeline.

**Tech stack:** Unity 6000.6.0f1, C#, NUnit, bundled whisper.cpp, local Ministral development adapter.

## Global constraints

- Continue the CURRENT working tree on claude/sharp-wozniak-iakuny; no reset/rewrite, no merge to main.
- Existing work is backed up at E:/GO-Live-LivePlay-audit-20260926-continue/initial-files with the initial diff.
- Root alone launches Unity. All source edits freeze while Unity compiles or runs tests.
- STT first: same newest audio for tiny-ru, base-ru, small-q5_1-ru, small-q5_1-ru-gpu, turbo-q5_0-ru-gpu; add tiny/base GPU configurations supported by the existing native plugin.
- Forced Russian is the RU baseline. Record all transcripts, semantic/content recall, speech acts, inventions, latency, VAD boundaries and RAM/VRAM measurements with limits. Select by preserved conversational meaning, not WER alone.
- Only the chosen shipping model stays in the StreamingAssets project path; benchmark alternatives remain local/gitignored. Do not delete the original recorded audio.
- Keep ordinary silence, actual audience presence, cooldown and bounded conversation; a question at 1–3 viewers may overdraw the ordinary budget.
- No ten-minute session request. Prepare a 60–90 second real microphone acceptance and retain raw pipeline evidence; do not mislabel replay as new live capture.
- Do not claim Viewer Core complete.

## Task 1 — verify and complete STT evidence

Files: existing SpeechModelComparison.cs, WavFile.cs, VoiceRecognition.cs, WhisperSpeechRecognizer.cs, VoiceInputBehaviour.cs, scene/authoring voice configuration; add focused regression tests under Assets/Game/Tests/Editor.

- [x] Inspect the current diff and successfully compile it before edits.
- [x] Extend the existing comparison harness with tiny/base GPU entries, external benchmark-model root, unique output location, incremental raw results and strict missing-config/model failure. Preserve the original comparison utility.
- [x] Validate WavFile round trip and malformed input bounds, UTF-8 decoding/prompt ownership and diagnostic lifecycle where the current diff exposes concrete gaps.
- [x] Download missing official model files into Logs/SpeechModels, verify hashes, and retain a manifest. Use corpus Logs/VoiceCorpus/20260926-182411 (25 marks, 95.17 s) unchanged.
- [x] Run all required configurations with fixed Russian and the same phrase slices; run production VAD on the corpus for the shipping candidate and smaller contenders. Annotate semantic meaning of every conversational question, especially phrase 1.
- [x] Configure the real VoiceInput path, authored scene and authoring defaults for the smallest semantically adequate winner. Preserve deliberate player language choice while making RU production use forced RU. Retain evidence and document single-take reliability limits.
- [x] Focused tests and independent technical review before continuing (69 deterministic voice cases pass; both lifecycle review findings resolved and re-review approved).

## Task 2 — daily-life and conversation regressions, then repair

Files: SpeechRelevance.cs, ReactionSelector.cs (ConversationThread), ReactionTuning.cs, ViewerDailyState.cs, ViewerCore.cs, ViewerUtterancePlan.cs, ChatContextBuilder.cs, ChatOutputValidator.cs, FallbackChat.cs, authored ViewerCommunity data; focused tests.

- [x] First add deterministic regressions for PersonalQuestion over Greeting; plural парни/ребят/вы; filler silence; direct low-audience question despite empty budget; ordinary speech rhythm; same-viewer follow-up; expiry; maximum turns counted including the initial published answer.
- [x] Test same-day permanent state stability across broadcasts, expected changes over different game days, anonymous state stability within a broadcast, and deterministic authored data selection.
- [x] Test the actual ordinary core-to-planner path: personal question gets relevant selected-viewer daily context; unrelated gameplay does not; daily context cannot authorize purchases, streamer hardware, history, money, promises or unseen gameplay.
- [x] Repair only reproduced gaps. Preserve existing authored profiles and hard-fact validation. No general NLP framework or persistent transcript.
- [x] Run expanded focused tests and independent technical review.

## Task 3 — short end-to-end acceptance and final suite

- [x] Replay the user's recorded microphone phrases through the production VAD/recognizer/feed/core and actual local chat adapter; retain raw transcripts, primary act, selected viewer, daily state, grounded plan, output, rejection/fallback and timing.
- [x] Update the older explicit recorded-speech acceptance fixture, which still hardcodes base/tiny, to use the scene's production voice pipeline. Mark the older StreamCore milestone model section as historical with a link to the new report.
- [x] Prepare the 60–90 second live path: opening personal questions, natural pause, так секунду, then жесть устал. Explicitly separate recorded replay evidence from unexecuted fresh human acceptance.
- [x] Run the COMPLETE non-Explicit Unity suite with zero failures; no weakening/ignoring tests.
- [x] Finish docs/ViewerCoreLivePlayCorrectionReport.md with model choice, evidence, limitations and acceptance path. Preserve unrelated initial csproj/TMP/VersionControl files byte-exact, review, and commit on the same branch without merging main.

Verification outcome: full non-Explicit suite 1,174 passed / 0 failed; 11 Explicit cases excluded. All three 75-second replays are retained as failed follow-up acceptance. The fresh human 60–90 second procedure is prepared but not performed. Code review approved with one minor recorder sample-rate edge case; Viewer Core is not declared complete. The commit step is delivered together with this plan's final version; no main merge.
