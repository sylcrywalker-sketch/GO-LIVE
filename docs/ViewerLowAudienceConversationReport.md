# Low-audience conversation correction

Continues `c8c5c3a` on `claude/sharp-wozniak-iakuny`. This report covers the narrow punctuation-independent continuation correction. No main merge. Viewer Core is **not declared complete**.

## Behavior and ownership

The existing C# conversation thread still owns the current viewer, presence epoch, last published line, window, answer count and pending turn reservation. No runtime state, save fields, events, managers or dependencies were added. `SpeechRelevance.ConversationAsksForAnswer` is used only when selecting the active conversation partner or planning an already selected follow-up. The global speech analysis and recognized text remain unchanged.

Short Russian personal/interrogative forms such as “устал наверное”, “тяжело было”, “нормально там”, “серьезно”, “а ты”, “и как”, “что потом”, “понравилось” and “долго” now use the existing question probability **0.95**. Bounded forms permit leading conversational particles, second-person “ты”, and trailing “наверное”/“видимо”. They match the whole short clause, not an arbitrary matching substring. “понятно” remains an acknowledgement on the existing **0.6** statement path; filler remains silent. Self-talk, third-person statements and unrelated gameplay/setup/money do not gain the new privilege.

The existing presence, visit epoch, six-second gap, 40-second window, five-answer maximum, pending work, stale cancellation and group/name switching remain authoritative. The initial published answer still counts. No probabilities, authored tuning, audience sizes or viewer identities were changed. The lexical classifier is intentionally bounded; it is not a general Russian semantic parser.

Selection and planning share the question predicate, so the generated continuation answers a question about the viewer's previous line instead of merely reacting to a statement. Global `PrimaryAct` may still show `Statement`: the question interpretation is local to the active exchange. A genuine preexisting personal question such as “я нормально, а ты как дела” retains its original continuation behavior.

## Regression evidence

The first new regression run reproduced **17 failures out of 37** on the baseline. The missing-punctuation tiredness phrase produced 117/200 same-viewer continuations; several elliptical forms produced zero. After the scoped correction, the selected Viewer fixtures passed **220/220**, including all 37 new cases.

The former exact late-follow-up diagnostic (published answer at 10 seconds, filler at 27, follow-up at 43, empty ordinary message budget) improved from **64/100** to **96/100** fixed seeds. A separate 200-seed sweep at audience sizes 1, 2 and 3 produced **185/200** same-viewer continuations for every tested punctuation-free form. These deterministic samples support the configured 0.95 decision; they are not a human-play reliability estimate. The punctuation-present form also has the existing general-question fallback selection paths and produced 198/194/193 continuations respectively.

Independent review found an introduced ordering defect in the ambiguity guard: rejecting every utterance containing “я” before honoring a preexisting PersonalQuestion broke mixed clauses. Two corrected regression cases reproduced zero continuations before repair. The guard now preserves the original PersonalQuestion priority. The initial review test draft's second phrase did not classify as PersonalQuestion and was corrected before confirming the actual defect; both RED artifacts are retained.

## Preserved speech and replay method

The selected `ggml-large-v3-turbo-q5_0.bin`, GPU/beam settings, VAD and speech preparation were not changed. Verified SHA-256 of the installed model: `394221709cd5ad1f40c46e6031ca61bce88931e6e088c188294c6d5a55ffa7e2`. The unchanged microphone corpus SHA-256 is `1eff759b65feb95d5d787cef1af011faa005d9567817ffe23bce124d0ca72a66`.

The three attempts use audience RNG states **1, 2, 3**, chosen before any run. Test-only reflection resets the existing StreamSession RNG before Start, and evidence records and checks the resulting broadcast seed. There is no injection of audience seats, daily state, viewer identity, response probability or generated text. The local chat adapter is the existing `mistralai/ministral-3-8b-instruct-2512` Q4_K_M model.

Each attempt uses the same real scene and unchanged corpus marks 1/4/8 at seconds 2/25/39 for 75 real seconds. OS hardware capture is disabled before Start, while the scene-owned speech worker, feed, core and model adapter run normally. These are **recorded replays, not fresh microphone acceptance**. The asserted partner is the thread owner at follow-up recognition; a bounded second opening answer may become that current partner. All attempts and their actual model outputs are retained.

## Three fixed scene attempts

All three replays preserved the opening PersonalQuestion, ignored “Так, секунду.” and transcribed the follow-up as **“Жесть. Устал, наверное.”**, without `?`. No reruns or replacement seeds were used.

| RNG state / broadcast seed | Opening and current partner | Follow-up result | Explicit test result |
|---|---|---|---|
| 1 / `10451216379200822465` | `Kotik_30`: “ахах, как котёнок после работы”; stressed workday | Partner had left before the filler and follow-up; watching=false, old epoch 1 versus current 0. No continuation. | **Failed**, empty follow-up. [Raw](evidence/viewer-low-audience/attempt-1/raw.jsonl), [XML](evidence/viewer-low-audience/attempt-1/results.xml) |
| 2 / `10905525725756348110` | `pavlik43`: “ниче не делал”; lazy day off | Same present viewer: **“даже не встал”**. Consistent dry reply about their lazy day. | **Passed**. [Raw](evidence/viewer-low-audience/attempt-2/raw.jsonl), [XML](evidence/viewer-low-audience/attempt-2/results.xml), [scene capture](evidence/viewer-low-audience/attempt-2/scene.png) |
| 3 / `2092789425003139053` | `vortex_28`: “классы сегодня”, then `arbuz`: “спокойно лежу с телефоном”. The latter becomes the current partner. | Same present `arbuz`: **“круто, что так долго выдержат”**. Published on the question-follow-up plan, but semantically unclear and inappropriate to the exchange. | **Failed** later in `CaptureApp`, clicking `desktop.app.streamly`, after all conversation assertions and the raw `accepted` marker. [Raw](evidence/viewer-low-audience/attempt-3/raw.jsonl), [XML](evidence/viewer-low-audience/attempt-3/results.xml) |

Both eligible attempts (2 and 3) scheduled and published a continuation from the same viewer with the same presence epoch. Their thread ages were 33.59 and 29.14 seconds; each had one prior turn. Follow-up recognition-to-publication latency was approximately 2.30 and 1.98 seconds (audio-end-to-publication 3.10 and 2.79 seconds). The first attempt is retained as a failure and is not counted as eligible: allowing an absent viewer to answer would violate the requested presence guard.

**Strict explicit-suite outcome: one pass, two failures.** The third raw `accepted` marker only records passed mechanical conversation assertions; it does not make the later failed test green or establish semantic quality. That attempt's generated wording remains a quality limitation, and no screenshot is claimed for it. The first opening is a vague personal work metaphor, while the third first opener uses awkward Russian (“классы”); neither is presented as polished dialogue. Ambient `Noob228`/`zhenya17` lines in attempt 1 are separate chatter events, not answers to the three speech phrases.

An [independent scene/evidence review](evidence/viewer-low-audience/scene-review.md) confirms this distinction and the final ordinary-suite result. The original unrelated four dirty files were restored byte-for-byte from this task's initial backup and remain outside the commit.

The evidence supports the narrow fix: missing punctuation no longer routinely kills an otherwise eligible continuation. It does **not** establish universal semantic conversation quality or fully green scene acceptance. The [fresh 60–90 second microphone procedure](ViewerCoreShortMicrophoneAcceptance.md) is prepared and **not performed**. It uses “Жесть. Устал, наверное?” and explicitly allows recognition to omit the question mark. Earlier failed replays remain in the [prior correction report](ViewerCoreLivePlayCorrectionReport.md).

## Final ordinary test suite

The post-review full Unity EditMode run without a test filter passed **1,213 tests, zero failures**, in 135.659 seconds. All 11 skipped cases carry `Explicit`; no ordinary case is ignored. The new continuation fixture contains 39 cases. This includes the existing unrelated scene, voice, save/load and peripheral tests. [Final XML](evidence/viewer-low-audience/full-final.xml), [initial RED](evidence/viewer-low-audience/regression-red.xml), [selected Viewer GREEN](evidence/viewer-low-audience/viewer-green.xml), [confirmed review RED](evidence/viewer-low-audience/review-red-confirmed.xml), [independent code review](evidence/viewer-low-audience/review.md).

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe' -batchmode -projectPath 'E:/GO! Live' -runTests -testPlatform EditMode -testResults 'E:/GO-Live-LowAudience-audit-20260926/full-final.xml' -logFile 'E:/GO-Live-LowAudience-audit-20260926/full-final.log'
```

Known Unity obsolete-API/DEVELOPMENT_BUILD and licensing diagnostics are separate from the zero-failure test result. Existing unrelated generated project files, TMP fallback font and VersionControlSettings changes are excluded from the correction.
