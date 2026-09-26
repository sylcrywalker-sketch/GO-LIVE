# Task 1 STT independent review

Reviewed 2026-09-26 on `claude/sharp-wozniak-iakuny`, baseline `a030b9f`. Read `task-1-brief.md`, `stt-report.md`, `task-1-review.diff`, the current voice/benchmark/test sources, authored scene/default authoring, semantic audit and raw results. Review did not edit Assets, run Unity, commit, or merge. Root owns Unity execution.

## Verdict

**Spec: substantially satisfied.** The required model comparison, unchanged corpus, production VAD comparison, semantic annotations, verified shipping configuration and focused verification are present. **Code quality: two P2 lifecycle findings remain before approval.** No P0/P1 findings. These concern diagnostic retention and callback teardown, not the model choice or decoder preparation.

## Finding — P2: clear abandoned diagnostic audio and enforce the actual cache bound

**Location:** `E:/GO! Live/Assets/Game/Scripts/Voice/VoiceRecognition.cs:154-155` (full segment retention and ineffective eviction across sequence gaps); related lifecycle at `VoiceRecognition.cs:204-208` (`StopWorker` abandons the worker without clearing those entries).

Every diagnostic submission stores the entire `SpeechSegment`, including its audio array. `SetEnabled(false)` discards its worker and all pending responses, so those dictionary entries can never be removed by `Update`. Unsubscribing diagnostics lets phrase sequences advance without adding dictionary entries. On a later recording, `_queued.Remove(_nextSequence - 32)` can target a key that was never inserted, allowing `_queued.Count` to grow beyond 32. The dictionary is otherwise cleared only by disposing the whole pipeline.

**Reproduction against the exact current sources, compiled in a separate PowerShell/.NET process without Unity:** use an immediate fake recognizer and one second of 220 Hz tone followed by two seconds of silence at 16 kHz. Repeat 40 times: enable/begin; subscribe a no-op `PhraseFinished` recorder; submit the tone; disable before calling `Update`; unsubscribe recorder; enable/begin; submit 33 ordinary phrases without diagnostics; disable. Inspect `_queued` only to read the result. Observed:

```text
After 40 recorder/voice-toggle cycles, _queued.Count=40; retained audio bytes=2867200; disabled=True
```

This is retained capture memory after shutdown of recognition, and growth is not bounded by the stated 32-entry logic. Longer phrases and 48 kHz capture retain more memory. The scenario matters when recording diagnostics while voice is repeatedly disabled/re-enabled or when dropped requests and listener gaps leave abandoned entries.

**Bounded correction:** clear diagnostic tracking when `StopWorker` abandons its pending responses, and evict the actual oldest retained entry when the cache exceeds its limit rather than assuming contiguous diagnostic sequence numbers. Add a focused regression for cancellation plus recorder subscription gaps. Keep the existing diagnostic events and worker ownership intact.

## Finding — P2: handle worker shutdown from the Recognized callback

**Location:** `E:/GO! Live/Assets/Game/Scripts/Voice/VoiceRecognition.cs:183-184`, with the null dereference on the next `while (_worker.TryTake(...))` iteration at line 165.

A `Recognized` subscriber may call the public `SetEnabled(false)` API. This stops the worker and sets `_worker = null` without setting `_disposed`. The post-event guard checks only `_disposed`, so `Update` immediately dereferences the now-null worker on the next loop condition. The newer `PhraseFinished` guard correctly handles this case; the adjacent `Recognized` guard does not.

**Reproduction against exact sources outside Unity:** subscribe `voice.Recognized += _ => voice.SetEnabled(false)`, start listening, submit one second of tone followed by two seconds of silence to an immediate fake recognizer returning `Привет, парни!`, then call `Update`. Observed:

```text
NullReferenceException: at GoLive.Voice.VoiceRecognition.Update()
                       at SttReviewDisableRepro.Run()
```

The issue is inherited from the baseline rather than newly introduced by this continuation. Root explicitly requested verification of this adjacent lifecycle case. It is still a concrete callback contract failure in the method already being hardened. Extend the post-`Recognized` guard to return when the worker was cleared and cover disabling recognition from that callback with a regression.
## Confirmed compliance and quality

- The original comparison utility/configurations are preserved, with tiny/base GPU variants added. It supports external model storage, a fresh output directory, flushed per-item evidence, explicit missing/unknown configuration failure, real-speech warm-up and separately labeled memory limitations.
- All required RU CPU/GPU comparisons are retained. Smaller contenders received the same symmetric decoder context before final selection. The final production run loads the actual StreamingAssets turbo file, uses beam search and fixed Russian, adds production context once (8,000 samples per side), and has zero experimental extra context.
- Final evidence includes all 25 marked phrases and 26 production VAD segments. The semantic audit covers all 25, explicitly preserves all three questions and the plural address in phrase 1, labels the damaged nickname in phrase 16, and separates the two English/mixed controls. Selection is correctly limited to tested candidates and this single take.
- Independently recomputed local SHA-256 hashes match the retained manifest for tiny, base, small-q5_1 and shipping turbo. The original corpus hash is unchanged: `1eff759b65feb95d5d787cef1af011faa005d9567817ffe23bce124d0ca72a66`. Only the 574,041,195-byte selected model and its metadata are present in StreamingAssets/Whisper. The final native log confirms the Vulkan0 backend.
- `VoiceInputBehaviour`, `GL.unity` and fresh authoring defaults agree on the selected model/GPU/beam path. Valid saved Auto/RU/EN choices are preserved; absent/invalid choices use the UI language without manufacturing a saved preference. Decoding is snapshotted before the worker factory uses it.
- Audio preparation preserves the original samples and capture timestamps; added decoder silence introduces no extra microphone capture wait. Existing worker ownership and pure C# separation remain intact.
- WAV validation is performed before destination truncation for invalid input; reads bound unsigned chunk sizes and reject malformed PCM/frame layouts. UTF-8 prompt memory is owned and released by the recognizer. Diagnostic callbacks that dispose the pipeline have return guards and regression coverage.
- Inspected root-owned `stt-production-green.xml`: **67 total, 67 passed, 0 failed**, including 66 deterministic cases and the explicit production benchmark, 11.312 s. I did not independently rerun Unity. These passing tests do not cover the two additional lifecycle findings above.

## Evidence wording

`docs/evidence/viewer-live-play/stt-semantic-audit.md:3` initially labeled the analysis “Human-reviewed meaning.” No human review was established in this task. Suggested “Phrase-by-phrase semantic review”; communicated to root. Root subsequently corrected it to “Phrase-by-phrase semantic review” and explicitly identified the coding agent as reviewer. Resolved; not another runtime defect.

No conclusion is made here about Task 2, the complete non-Explicit suite, new live microphone acceptance, or overall Viewer Core completion.



## Final re-review — both P2 findings resolved

Re-reviewed the current `VoiceRecognition.cs`, the three new tests, `task-1-fix-review.diff` and the correction section of `stt-report.md` on 2026-09-26. This verdict supersedes the initial two-finding verdict above; the original findings remain in this file as the audit trail.

**Task 1 spec verdict: PASS. Code-quality verdict: APPROVED for continuing to Task 2. No outstanding actionable findings in the reviewed Task 1 scope.**

- The first P2 is resolved: `VoiceRecognition.cs:211-216` clears diagnostic tracking when the worker is abandoned. Lines 155-161 remove the oldest actually retained sequence key, so gaps in listener subscriptions cannot defeat the 32-entry bound. The bounded scan adds neither another state container nor LINQ allocations.
- The second P2 is resolved: `VoiceRecognition.cs:190-191` returns after a `Recognized` subscriber clears the worker. It cannot revisit the loop with a null worker or deliver a second response from the abandoned worker.
- The new regressions exercise cancellation while inference is blocked, listener gaps with more than 32 retained phrases, and disabling from the first of two already completed responses. They verify the relevant lifecycle behavior, keep native inference/microphone access out of the tests, release test gates in `finally`, and await fake-recognizer disposal. Private response queue inspection takes the worker's own lock.
- Independently inspected root-owned evidence: `stt-review-red.xml` has **28 total / 25 passed / 3 failed**, with the three new tests failing on the described defects. `stt-review-green.xml` has **69 total / 69 passed / 0 failed**, 1.4403231 seconds; all three new regressions pass. Focused `git diff --check` is clean apart from the existing line-ending normalization warning. No further Unity run was performed by this reviewer.

The fixes preserve the reviewed model selection, audio preparation, language handling, public API and worker-thread ownership. The earlier real-corpus production run remains valid evidence for the unchanged recognizer/preparation path; the 69-case run is deterministic regression evidence and does not claim another corpus run.

Root separately tracks migrating the old explicit acceptance fixture away from its base/tiny hardcoded recognizer and adding historical-document supersession guidance in Task 3. Those items, full-project verification and fresh live microphone acceptance remain outside this Task 1 approval. No overall Viewer Core completion claim is made.
