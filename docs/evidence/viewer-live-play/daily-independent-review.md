### Spec Compliance

- ❌ Issues found: the maximum published-answer count and pending-turn bound remain bypassable through two selection paths in `Assets/Game/Scripts/Viewers/ReactionSelector.cs:249` and `:240`. English day-activity consistency is absent (`ChatOutputValidator.cs:84`) and was bypassed in actual replay output. Warmup does not fully share the worker limit across a broadcast restart with an ordinary request still completing cancellation (`Assets/Game/Scripts/Viewers/ChatDirector.cs:174`, `:134`).
- ✅ Verified from the supplied package: personal questions outrank greeting; plural address and filler remain distinct; daily context is gated by the common core/planner predicate (`SpeechRelevance.cs:110`, `ViewerUtterancePlan.cs:140`, `ViewerCore.cs:184`). Permanent day selection depends on viewer/day, anonymous selection on viewer/broadcast (`ViewerDailyState.cs:94`).
- ✅ ViewerDay, OwnLine and OtherViewerSaid are excluded from specific-claim authority, while actual facts retain it (`ViewerUtterancePlan.cs:181`). The regression cases exercise the actual core-to-planner path and hard-claim rejection (`ViewerDailyConversationRegressionTests.cs:233`, `:253`, `:265`).
- ⚠️ Real model and microphone quality and the complete non-Explicit suite are controller-owned acceptance checks, not established by this diff. The supplied final expanded result is green: `E:/GO-Live-LivePlay-audit-20260926-continue/viewer-expanded-green.xml:2`, 351 total / 351 passed / 0 failed / 3.1345168 seconds.

### Strengths

- The daily state is derived in ordinary C# with configuration separate from transient state and no save-format addition (`ViewerDailyState.cs:29`, `:94`). The changes preserve authored profiles and reuse existing core/director ownership.
- The common relevance predicate prevents the producer from sending irrelevant daily state before prompt assembly (`ViewerCore.cs:184`, `ViewerUtterancePlan.cs:140`).
- The continuation reservation is released through the existing Finished event; rejected incoming queue submissions now invoke it too (`ChatDirector.cs:150`, `:367`, `ViewerCore.cs:209`). Publication, expiration, departure, discard, cancellation, queue eviction and queue rejection all reach a release path.
- Canceled warmup itself retains its worker until task completion and cannot overwrite the next broadcast's warmup metric (`ChatDirector.cs:193`, `:266`), with a controllable delayed-cancellation regression (`ViewerDailyConversationRegressionTests.cs:332`).

### Issues

#### Critical (Must Fix)

- None found in the reviewed scope.

#### Important (Should Fix)

1. **[P2] Preserve continuation identity in the fallback selection path.** `Assets/Game/Scripts/Viewers/ReactionSelector.cs:249`–`:255` schedules a generic conversational answer after the direct continuation roll at `:235` fails. With one present viewer and a personal question such as “Устал?”, PickConversational can select the same current partner, but the new intent has FollowUp=false and no reservation. On publication `:126` resets Turns to 1 instead of incrementing it. Thus an uninterrupted exchange can publish beyond ConversationMaximumTurns. Make all paths that select the active partner for the same continuation preserve FollowUp and reserve the turn, or prevent this fallback from restarting that partner. Add a fixed-seed regression that publishes whichever same-viewer answer was actually selected, including the non-direct fallback, and verifies max-minus-one becomes max and blocks the next answer.

2. **[P2] Reserve initial conversational answers as well as continuations.** `Assets/Game/Scripts/Viewers/ReactionSelector.cs:240` reserves only when continuing is true; the initial named path at `:237` and general path at `:253` have no pending reservation. Before the first answer publishes, the thread has no current viewer, so Exhausted cannot stop another initial answer after the scheduled due time plus ConversationGapSeconds. A slow request or a queued answer can therefore admit two opening answers from the same viewer, both later resetting Turns=1. With ConversationMaximumTurns=1 this directly publishes two answers despite a one-answer cap. Track pending opening ownership by viewer and presence epoch, release it through the existing terminal callback, and verify both the named and general paths with a delayed first publication. This is separate from issue 1: correcting only the fallback continuation flag leaves this race intact.

3. **[P2] Include canceled ordinary generations when deciding whether warmup may start.** `Assets/Game/Scripts/Viewers/ChatDirector.cs:134` relies on RunningGenerations, but CancelAll cancels then clears all ordinary jobs at `:174`–`:180` without retaining tasks until completion. After a broadcast restart, an old ordinary GenerateAsync task can still be running while the new Warmup starts because it is no longer counted. The explicit retention at `:268` protects only old warmup tasks, and the new delayed-cancellation test covers only that direction. Preserve/count canceled ordinary tasks separately until actual completion while finishing their reaction reservations immediately. Add the reverse lifecycle test: complete old warmup, start a delayed ordinary request, restart and request warmup, assert no new GenerateAsync call until the old request completes. Also retain this accounting for stale/departure removal, which calls the same disposal/removal path.

4. **[P2] Apply daily-activity consistency to English-speaking viewers.** `Assets/Game/Scripts/Viewers/ChatOutputValidator.cs:84`–`:94` contains only Russian DayClaims, although English and Mixed personas are supported. Thus English output can claim an excluded activity and pass the new daily guard. This is reproduced by the controller's real replay, not a hypothetical paraphrase: `short-replay-1/raw.jsonl:9` supplies an English persona with gen.stress (stressful day at work; switching off), `:10` records the model result "slept too much", and `:12` records its actual publication. Sleep is not in that day's allowed activities. Extend the existing bounded activity patterns and immediate-negation handling to supported English phrases, including `slept`, `played`, `worked`, and their negative forms; do not introduce a general NLP system. Add a deterministic English-persona regression for this exact observed output, an allowed work-day reply, and a negated gaming/sleep reply. The captured rejection of “Жесть. Устал, наверное.” is not a separate defect by itself because the continuation branch intentionally has a probabilistic silence outcome.
#### Minor (Nice to Have)

- **[P3] Verification output contains warning noise.** `viewer-expanded-green.log:237`, `:257` and `:263` report UAC0009 on ReactionSelector's DEVELOPMENT_BUILD checks; `:12` and `:21` show Unity licensing validation/access-token noise. There are also existing editor obsolete-API warnings from `:275` onward. The suite passed, but output is not pristine. These are largely existing conventions/environment issues; record them separately rather than expanding this repair into broad unrelated cleanup.

### Focused inspection and evidence

- Reviewed the supplied base-a030b9f/current-working-tree package. The first output was truncated, so unread/truncated portions were read in bounded slices; no git commands or broad repository crawl were performed.
- The ChatDirector Post and MakeRoom hunks ended mid-function. Read only the necessary remainder (`ChatDirector.cs:277`–`:395`) to check the named risk of missing Finished notifications and callback ordering. Remove invokes Finished before Shown; cancellation/expiry/departure/discard/queue eviction all release, and incoming rejection is covered by the new explicit invocation.
- Named risk: cancellation is asynchronous and can outlive a restart. Inspected the actual adapter `OpenAiCompatibleChatModel.cs:30`–`:54`: it awaits HttpClient work and handles cancellation afterward, so Cancel does not establish that the task has completed. This supports issue 3 without assuming a synchronous cancellation contract.
- Read the existing final expanded XML header and selected warning/error log lines. Did not launch Unity, rerun tests, or edit source, index, branch or HEAD.
- Existing maximum-turn tests at `ViewerDailyConversationRegressionTests.cs:104` manually publish known follow-up flags; pending tests at `:133` start with a published thread. They do not exercise issues 1–2. Existing cancellation-resistant test at `:332` starts with a warmup task, not an ordinary generation, so it does not answer issue 3.

### Assessment

**Task quality: Needs fixes.**

Daily-state separation, evidence boundaries, and terminal release are sound in the reviewed paths. The remaining selection resets, English day-consistency gap and incomplete in-flight accounting violate the explicit bounded-conversation and shared-worker requirements despite the green focused suite.



### Re-review after the four-P2 repair wave

- ✅ Original findings 1 and 2 are addressed. `Assets/Game/Scripts/Viewers/ReactionSelector.cs:530` derives continuation identity at the common scheduling boundary, and `:552` reserves every conversational opening or continuation. Pending ownership includes viewer and presence epoch; release is still keyed by intent id. The fixed-seed fallback test publishes the actual selected origin (`ViewerDailyConversationRegressionTests.cs:155`); the named/general opening tests hold publication and enforce a one-answer cap (`:174`).
- ✅ Original finding 3 is addressed. `Assets/Game/Scripts/Viewers/ChatDirector.cs:385` retains unfinished retired tasks, `:279` counts them alongside warmup and ordinary jobs, and `:196` removes completed retired results without applying them to chat or metrics. Both CancelAll and Remove use this terminal handling (`:177`, `:374`). The new delayed-adapter test covers restart, stale expiration and departure.
- ✅ The exact real-replay failure in original finding 4 is addressed: `Assets/Game/Scripts/Viewers/ChatOutputValidator.cs:90` recognizes "slept", and the English/Russian cases reject excluded sleep and gaming while allowing an authored sleep day. The English guard is deliberately limited to work/sleep/gaming phrases, consistent with a bounded correction rather than general language parsing.
- **WITHDRAWN FALSE POSITIVE — see final correction below.** The following earlier assessment was incorrect: **contracted English negation is normalized differently from the new guard.** `Assets/Game/Scripts/Viewers/ChatOutputValidator.cs:97` expects `havent`, `wasnt`, etc., but `:184` passes `SpeechRelevance.Normalize` output. That tokenizer separates apostrophes (`SpeechRelevance.cs:412`, `:428`), so a legitimate work-day reply "haven't slept, worked" yields `haven t` before the recognized `slept` claim and is falsely rejected; "wasn't gaming, worked" has the same problem. Handle the normalized contraction form (including straight/curly apostrophes) and add negative controls with activity words that actually match DayClaims. The existing "didn't sleep" / "did not sleep" controls at `ViewerDailyConversationRegressionTests.cs:337` do not exercise the negation code because bare `sleep` is absent from the Sleep claim regex. This is an error in the newly introduced negation handling, not a request for wider NLP coverage.
- ✅ Verification evidence read, no tests rerun: `viewer-review-red.xml:2` reports 55 total / 47 passed / 8 failed; `viewer-review-green.xml:2` reports 370 total / 370 passed / 0 failed / 3.3034883 seconds. The green result does not cover contracted negation of a recognized activity. Existing warning-noise finding remains non-blocking; real acceptance remains root-owned.
- Check scope: read the repair-relevant sections of `task-2-review-package-final.txt` and the appended implementer report. No Unity process, source edit, index or branch mutation was performed. The only written artifact is this review.

**Superseded assessment: the alleged remaining English-negation P2 is withdrawn below; it was not a production defect.**


### Final correction and task-scoped approval

- ✅ **Spec compliance: Approved for the reviewed Task 2 scope.** All four original P2 findings have been addressed by the repair wave described above. No blocking code-quality finding remains from this review.
- ✅ **Withdrawn false positive: English contracted negation.** The reviewer inferred tokenization behavior from incomplete context and the broad method comment. The actual implementation at `Assets/Game/Scripts/Viewers/SpeechRelevance.cs:413` explicitly ignores both straight and curly apostrophes, so "haven't" and "haven’t" normalize to `havent`, exactly as `ChatOutputValidator.cs:97` requires. No production change was needed; the prior allegation of false rejection was incorrect.
- ✅ The added controls at `Assets/Game/Tests/Editor/ViewerDailyConversationRegressionTests.cs:353` exercise recognized `slept` and `gaming` claims with straight/curly contractions, full-form negation, and positive counterparts that must be rejected. They are valid behavioral tests. `viewer-negation-red.xml:2` confirms 8 total / 8 passed / 0 failed / 0.1463456 seconds with production unchanged; the filename does not make this a RED result.
- ✅ Previously verified expanded repair evidence remains `viewer-review-green.xml:2`: 370 total / 370 passed / 0 failed / 3.3034883 seconds. The eight added negation controls were verified separately, not claimed as part of that earlier expanded run.
- ⚠️ This is the requested task-scoped implementation review, not complete Viewer Core acceptance. The root's remaining natural replay investigation and complete non-Explicit suite remain separate evidence. Existing Unity warning noise is non-blocking and has not been silently relabeled as pristine output.

**Final task quality: Approved.**
