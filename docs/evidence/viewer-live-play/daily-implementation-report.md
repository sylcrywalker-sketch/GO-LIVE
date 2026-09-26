# Task 2 — daily-life and conversation repair

Status: expanded focused verification passed (370/370), including all four reproduced independent review findings. Independent re-review and root acceptance/full-suite verification continue. No commit or merge was made. This report does not claim Viewer Core complete or replace the real acceptance evidence.

## Scope and preservation

Continued the unfinished Claude working tree on `claude/sharp-wozniak-iakuny` (baseline `a030b9f`). Preserved the existing authored profiles, daily stories, same-day/broadcast selection algorithm, presence epochs, saved community state, structured memory/promises, and the STT changes already approved under Task 1. No models, scene, authored assets, STT files, or acceptance fixtures were edited by this worker.

The prior daily integration was already present. Repairs target reproduced defects rather than replacing that integration or changing the architecture.

## Regression evidence

All Unity runs were launched by the root agent, with edits frozen until process exit.

| Run | Tests | Passed | Failed | Time | Evidence |
|---|---:|---:|---:|---:|---|
| Initial focused RED | 31 | 16 | 15 | 0.1846493 s | `viewer-regressions-red.xml`, `.log` |
| Expanded first verification / remaining RED | 346 | 343 | 3 | 3.0972428 s | `viewer-expanded-first.xml`, `.log` |
| Pending-turn RED | 349 | 348 | 1 | 3.154 s | `viewer-pending-red.xml`, `.log` |

The initial 15 failures established: an extra published turn and immediate same-viewer reopening; unrelated gameplay being treated as personal follow-up; daily story tokens authorizing streamer hardware, quantity, recurrence and purchase/model claims; own daily chat laundering hardware into follow-up evidence; an irrelevant daily validator constraint; rejection of a reasonable negated activity; named personal fallback returning presence; concurrent warmup/answer and missing warmup cancellation on end/load.

The expanded run proved those 15 repairs and reproduced three remaining failures: `SituationFor.Day` still being present for unrelated gameplay; prompt size 4328 exceeding the existing 4000-character test limit; repeated general group questions bypassing the ordinary viewer cooldown. Those tests were preserved and the causes repaired.

The pending-turn RED proved a further race: with published turns at maximum minus one, a pending final answer allowed another final answer to be scheduled before publication. Cancellation/expiry release cases were added before implementing its reservation.

One further controllable-fake regression verifies cancellation-resistant warmup across broadcast restart: the old request keeps the worker until actual completion, cannot publish, and cannot overwrite the new broadcast's warmup latency. Final expanded result will be appended after root verification.

## Changes and why

- `SpeechRelevance`: a small deterministic shared continuation predicate recognizes personal replies such as the actual STT output `Жесть. Устал, наверное.` without requiring `?`. New gameplay, setup/money topics, filler and streamer self-talk do not acquire a personal follow-up merely because a thread exists. No transcript store or general NLP framework was added.
- `ReactionSelector` / `ConversationThread`: the configured maximum counts the initial published answer; active continuation uses a strict less-than bound. An exhausted same partner cannot immediately be selected to reopen a personal exchange within the window. General repeated questions to the whole chat obey the ordinary viewer gap; personal/direct questions retain the short conversational gap and small-audience budget exemption.
- A pending follow-up reserves the conversation until it publishes, drops, cancels or expires. One intent id and expiry are transient C# state; no new abstraction was needed. Existing `Director.Finished` releases the reservation through `ViewerCore`, and publication also releases it for direct selector callers. Releasing an old id cannot clear a newer reservation. An expired result still passes through the director's existing stale-publication check and cannot publish after its expiry.
- `ViewerUtterancePlanner` / `ViewerCore`: `UsesDailyContext` is shared because the producer and planner previously disagreed about relevance. Unrelated gameplay now has `SituationFor.Day == null`, rather than merely omitting day facts from the prompt. Personal planning still receives the selected viewer's original deterministic day.
- Hard specific-claim evidence excludes `ViewerDay`, `OwnLine`, other generated viewer text and general recent-chat message content. These remain conversational context, not authority for streamer hardware, quantities or past gameplay. Actual streamer/game/memory/promise facts and visible sender names retain their established permissions. The existing ordinary recurrence cue from another participant is preserved separately; the selected viewer's own personal answer cannot supply it.
- `ChatOutputValidator`: day consistency applies only to a personal plan. Narrow immediate negation such as `не играл, работал` no longer asserts a gaming day; a positive contradictory activity is still rejected. Existing money, transaction, history, promise, relationship and specific-claim guards remain.
- `FallbackChat`: personal questions are answered before a named-viewer presence/greeting fallback. Harmless personal follow-ups use the personal fallback as well.
- `ChatContextBuilder`: clips only other people's recent chat quotes to 60 characters instead of 120. Six recent lines and the full current/own quote limits remain; this repairs the existing 4000-character prompt regression without weakening its assertion.
- `ChatDirector`: warmup uses an owned cancellation source, runs once per broadcast, participates in the same worker count, and is cancelled at restart/end/load/disposal. A cancellation-resistant warmup keeps its slot until completion. A cancelled old result cannot publish or overwrite new-broadcast metrics. Warmup failure still does not count as an ordinary model failure.
- `ReactionTuning`: clarified that maximum turns means published viewer answers including the initial one; authored values were not changed.

`git diff --check` passed for the edited paths; only existing LF/CRLF conversion notices were printed.

## Ownership and architecture contract

| System | Responsibility / state | Dependencies | API / events | Saved / tested |
|---|---|---|---|---|
| Speech relevance | Classify one recognized phrase; immutable analysis and stateless bounded lexicons | Recognized text, known name forms | `Analyze`; no events | Not saved; personal priority, plural address, filler, punctuation-free contextual reply |
| Selector / rhythm / thread | Choose present viewers and timing; own per-broadcast budget, gaps, thread, published turns and one pending continuation | Tuning, roster/epochs, seeded random, relationship lookup | `Select`, `ObservePublished`, existing publication selection; internal finish release; no new events | Not saved; budget exemption, ordinary rhythm, same viewer, expiry/epoch, maximum, queue race and reservation release |
| Daily life | Derive harmless viewer context, separate from configuration and durable game facts | Authored activity choices or existing generic options, viewer id, day/broadcast, relationship | Existing `ViewerDailyLife.For`; no events | Derived, not saved; same-day cross-broadcast stability, changes across days, anonymous within-broadcast stability, authored deterministic selection |
| Core / planner | Supply relevant viewer context and construct a bounded social/factual plan | Current game/broadcast state, selected intent, structured memories/promises | Existing core situation and planner APIs; shared internal relevance predicate | Cached per reaction; no new saved fields; actual core-to-planner day gating and hard-evidence separation |
| Validator / fallback | Accept bounded presentation text or choose harmless fallback; no game-state mutation | Intent, plan, visible chat, language/persona and existing hard facts | Existing validation and fallback APIs; no events | Not saved; false hard claims, negation, unrelated context and fallback priority |
| Director | Own queued generation and warmup lifetime, cancellation, publication and measurements | Model adapter, settings, chat/log, clock | Existing `BeginBroadcast`, `Warmup`, `Submit`, `Update`, `CancelAll`, `Dispose`; existing `Shown`/`Finished` events | Not saved; worker bound, once-per-broadcast, end/load cancellation, delayed cancellation, stale publication |

All new runtime state is owned by ordinary C# classes and used by the existing main-thread coordination flow. No MonoBehaviour business logic, singleton, service locator, global manager, persistent transcript, ScriptableObject runtime database, or save-format migration was added.

## Limits and remaining work

The validator remains a conservative phrase/quantity/hardware guard, not semantic proof against every possible vague unseen-gameplay or purchase sentence. This task removes daily/own-generated-chat authority and preserves existing structured hard facts; it does not expand into a general NLP system. Real model wording and real microphone response quality require the root's short acceptance run, with raw transcript, actual selected viewer/day/plan, model output and latency recorded.

Independent technical review, final expanded verification, the complete non-Explicit suite and short real acceptance are coordinated by the root. Final results will be appended below when available.

## Queue-terminal follow-up

`viewer-queue-red-2.xml` ran 351 focused-expanded cases: 350 passed, with the new queue rejection fixture failing at its setup counter because LiveDesktop can retain startup jobs. The fixture was corrected to clear those jobs before reserving any turn, prove six blockers fill the queue without drops, and prove that submitting the reserved continuation increments the rejection counter by exactly one. No assertions were weakened.

`viewer-queue-red-3.xml` then reproduced the actual terminal-path bug with the corrected fixture: 1 test, 0 passed, 1 failed; resumed conversation count was 0 instead of greater than 15. `ChatDirector.Submit` logged a full-queue rejection but omitted its existing `Finished` notification. The minimal repair now invokes `Finished(intent, null, null)` on that terminal path, releasing the selector's reservation through the same owner callback as cancellation, expiry and other drops.

The pending-turn reservation and cancellation-resistant warmup tests passed in the 351-case expanded run. Final verification is pending after the queue notification repair.


## Final focused verification

Root launched the following Unity command with Assets frozen; this worker independently read the resulting XML and command-line arguments in the log:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -projectPath 'E:/GO! Live' -runTests -testPlatform EditMode -testFilter 'GoLive.Tests.ViewerDailyConversationRegressionTests;GoLive.Tests.ViewerUtterancePlanTests;GoLive.Tests.ViewerPromiseSocialTests;GoLive.Tests.ViewerProfileTests;GoLive.Tests.ViewerPresenceIntegrationTests;GoLive.Tests.ViewerMemoryTests;GoLive.Tests.ViewerIntegrationGuardTests;GoLive.Tests.ViewerCommunityTests;GoLive.Tests.ReactionFoundationTests;GoLive.Tests.ChatDirectorTests;GoLive.Tests.VoiceInputConfigurationTests;GoLive.Tests.AudioPreparationContextTests;GoLive.Tests.VoiceEvidenceRegressionTests;GoLive.Tests.VoicePipelineTests' -testResults 'E:/GO-Live-LivePlay-audit-20260926-continue/viewer-expanded-green.xml' -logFile 'E:/GO-Live-LivePlay-audit-20260926-continue/viewer-expanded-green.log'
```

Result: **351 total, 351 passed, 0 failed, duration 3.1345168 seconds**. The focused fixture has 36 deterministic cases. This includes the corrected queue-rejection release, pending final-turn bound, cancellation/expiry release, cancellation-resistant warmup, existing prompt/cooldown tests, and the approved Task 1 voice regressions.

Source implementation is frozen for independent review and the root's short replay. The complete non-Explicit suite and real scene/model/microphone acceptance remain separate root-owned evidence; 351 passing focused tests alone do not establish complete Viewer Core acceptance.


## Independent review repairs

Review RED (`viewer-review-red.xml` / `.log`, root launched): 55 tests, 47 passed, 8 failed, 0.2424046 s. Failures reproduced all four P2 findings; passing controls stayed unchanged.

- Conversation scheduling now assigns opening/continuation identity in one existing scheduling path, including a continuation selected after the direct probability roll fails. Transient reservations are owned per viewer and presence epoch, release by intent id, and expire when no publication occurs. Initial answers reserve their first turn too. No authored chances were changed.
- ChatDirector retains only unfinished tasks from cancelled ordinary jobs until the adapter actually completes them. CancelAll, stale expiry, and departure share this terminal handling. Finished still releases the conversation immediately; retired results never publish or affect generation metrics. This uses a bounded owned task collection to preserve the existing worker limit across cancellation-resistant adapters.
- Daily contradiction checks gained narrow English work/sleep/gaming claims and immediate English negation alongside existing Russian checks. Separate valid-day, negation, and other-person controls passed in RED. This remains a conservative guard, not general semantic language understanding.

Responsibility/state/dependencies/API/events/save: the existing selector owns transient per-viewer pending intent ids, epochs, and expiry; the director owns transient unfinished retired model tasks. No new public API, events, persistence, service, or abstraction was added. Existing Finished is the release boundary. Deterministic tests cover fallback publication/counting, pending named/general openings, restart/stale/departure worker accounting, and bilingual contradiction controls. Expanded verification and independent re-review follow with Assets frozen.


Review GREEN verified directly from XML: `viewer-review-green.xml` / `.log`, 370 total, 370 passed, 0 failed, 3.3034883 s. Unity exited. Assets remain frozen for root replay/full-suite verification and independent re-review.

Exact matching expanded command (root launched):

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe' -batchmode -projectPath 'E:/GO! Live' -runTests -testPlatform EditMode -testFilter 'GoLive.Tests.ViewerDailyConversationRegressionTests;GoLive.Tests.ViewerUtterancePlanTests;GoLive.Tests.ViewerPromiseSocialTests;GoLive.Tests.ViewerProfileTests;GoLive.Tests.ViewerPresenceIntegrationTests;GoLive.Tests.ViewerMemoryTests;GoLive.Tests.ViewerIntegrationGuardTests;GoLive.Tests.ViewerCommunityTests;GoLive.Tests.ReactionFoundationTests;GoLive.Tests.ChatDirectorTests;GoLive.Tests.VoiceInputConfigurationTests;GoLive.Tests.AudioPreparationContextTests;GoLive.Tests.VoiceEvidenceRegressionTests;GoLive.Tests.VoicePipelineTests' -testResults 'E:/GO-Live-LivePlay-audit-20260926-continue/viewer-review-green.xml' -logFile 'E:/GO-Live-LivePlay-audit-20260926-continue/viewer-review-green.log'
```

## Contracted-negation re-review evidence

The remaining contracted-English-negation concern did not reproduce. Root narrow run `viewer-negation-red.xml` / `.log`: 8 total, 8 passed, 0 failed, 0.1463456 s. Both ASCII and curly apostrophes were exercised with recognized activity words (`haven't/haven’t slept`, `wasn't/wasn’t gaming`), alongside explicit-not and positive-claim controls. Positive sleep/gaming claims were rejected with `contradicts your day`; negations were accepted.

Current `SpeechRelevance.Tokens` explicitly skips both apostrophe characters instead of flushing the token. Thus Normalize produces `havent` and `wasnt`, already handled by NegatedActivity. No production repair was made for this non-reproduced finding; focused tests remain as regression evidence. Assets stay frozen for root verification.

## Late statement follow-up diagnostic

Root's bounded deterministic diagnostic passed (`viewer-runtime-diagnostic.xml` / `.log`): 2 tests, 2 passed, 0 failed, 0.1575034 s. Across 100 fixed seeds using real ephemeral one-seat rosters, an initial personal answer publicly observed at second 10, filler speech at 27, and exact statement `Жесть. Устал, наверное.` at 43 yielded 64 same-viewer follow-ups. Ordinary budget was exhausted. The actual GL-bound ViewerCore configuration loaded in Unity retained conversation window 40 seconds, maximum 5 answers, and gap 6 seconds despite the fields being absent from the asset YAML.

All three root-owned actual 75-second replay attempts had an opening answer but did not observe the follow-up answer. The third showed the original partner still present with unchanged epoch and thread age approximately 32.66 seconds. These limited live observations do not establish a selector defect: the exact late statement and ephemeral path retain the configured probability in the deterministic diagnostic. No runtime or tuning changes were made to force acceptance. A fresh human scenario is prepared but has not been performed. Full non-Explicit verification is root-owned and running; this report does not claim acceptance complete.
