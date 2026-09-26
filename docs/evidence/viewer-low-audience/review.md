# Independent low-audience continuation review

Review scope: uncommitted changes relative to c8c5c3a in SpeechRelevance, ReactionSelector, ViewerUtterancePlan, ViewerLowAudienceContinuationTests, ViewerDailyConversationRegressionTests, and the recorded-scene fixture/procedure. No project files changed and no Unity process launched by the reviewer. Existing unrelated project/TMP/settings dirt excluded.

## Final code-review disposition

**Approved by code inspection. No outstanding actionable findings in the requested scope.** The P2 found during the initial review is resolved as described below. Approval of the code is not a claim that the currently running post-fix suite or the three scene attempts passed: their final artifacts remain for verification.

## Original finding and verified resolution

**P2 — Preserve existing personal questions before applying the new whole-utterance subject rejection. Resolved.**

The first implementation put `Any(tokens, OtherConversationSubjects)` before the existing `PersonalQuestion` early return. The concrete phrase `я нормально а ты как дела` remains globally PersonalQuestion and QuestionToViewer, because `ты` is singular and `как дела` satisfies the existing state-question rule. The baseline ContinuesConversation returned true, but the first implementation returned false solely because `я` appears in the first clause. In an active exchange, this skipped the direct continuation path, allowing a different viewer to answer or restarting the turn count even when the original viewer answered.

The revised implementation restores the preexisting PersonalQuestion priority immediately after the unchanged null/filler/plural rejection. The new subject guard therefore only constrains ambiguous continuation forms. This directly removes the introduced regression while keeping ordinary self-talk and third-person ambiguous statements excluded. No new global speech classification was added.

The added `ExplicitPersonalQuestionStillContinuesAfterStreamerSharesOwnState` cases use `я нормально а ты как дела` and `я отдохнул а ты как сам`. Each first verifies PersonalQuestion classification and then requires same-viewer FollowUp selection across 30 predetermined seeds. `review-red-confirmed.xml` demonstrates both intended regressions before the fix: each produced zero same-viewer follow-ups instead of at least 25. These are behavioral tests of the regression, not implementation-shape assertions.

The earlier `review-red.xml` was also inspected and retained. Its second proposed phrase, `я отдохнул а ты устал?`, failed the PersonalQuestion precondition rather than demonstrating this regression; it is correctly excluded from the confirmed evidence. Both original and corrected RED artifacts remain available.

## Other review results

No additional actionable defects found. The contextual predicate does not mutate global speech acts or recognized text. Existing thread presence/epoch/window/turn/pending/cooldown guards and release/cancellation implementation remain in place. Question selection and follow-up planning use the same predicate; ordinary acknowledgements retain their statement chance. Whisper and voice capture code are unchanged.

The existing per-call pronoun-array allocation was replaced by a static readonly lexicon in the reviewed working tree. No separate allocation finding remains.

The scene fixture fixes the existing audience RNG state before normal StreamSession.Start and checks the resulting broadcast seed. It retains the actual scene, recorded corpus slices, voice worker, stream feed and local model adapter. The current-partner assertion matches ConversationThread ownership by the last published conversational reply; it correctly permits a bounded second opening reply to become the partner. The predeclared states 1/2/3, fresh output directory check, raw seed/transcript/thread/output logging and instruction to retain failures support an honest three-attempt report. The fixture alone does not prove semantic reply quality; its accepted record explicitly leaves that to raw-output review.

## Evidence actually inspected

- `regression-red.xml`: 37 tests, 20 passed, 17 failed on the baseline. Failures include intended missing-punctuation reliability/planning cases and third-person ambiguous statements.
- `viewer-green.xml`: 220 tests passed, zero failures; initial new fixture 37/37 passed. This predates the two additional review regression cases and their fix.
- `full-nonexplicit.xml`: 1,211 passed, zero failures, 11 Explicit tests skipped; also predates the final review fix.
- `review-red.xml`: original attempted regression run preserved, including the invalid second case described above.
- `review-red-confirmed.xml`: two intended same-viewer regression cases, both failed with zero matching follow-ups before the fix.
- `predetermined-attempts.txt`: states 1, 2 and 3 declared before scene replay outputs existed.

At re-review completion the final post-fix full suite was still running and no scene replay results existed. This review makes no post-fix test-pass or scene-acceptance claim. Root must inspect `full-final.xml` and all three raw scene outputs before reporting their outcomes.

## Final-suite evidence addendum

The reviewer subsequently inspected `full-final.xml`: 1,213 tests passed, zero failed, 11 Explicit cases skipped (1,224 total entries). Both `ExplicitPersonalQuestionStillContinuesAfterStreamerSharesOwnState` cases passed. This confirms the post-fix automated verification previously left pending. Scene acceptance remains separate and is assessed in `scene-review.md` after all three predetermined attempts finish.

## Scene-evidence addendum

All three predetermined scene attempts have now been independently inspected; see `scene-review.md`. Strict XML outcome is one pass and two failures: absent partner in attempt 1, screenshot navigation in attempt 3. The two eligible partners continued with the correct identity, but attempt 3's wording was semantically unclear. Code-review approval and the 1,213 passing automated tests do not imply overall scene acceptance or completion of Viewer Core.
