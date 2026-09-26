# Independent whole-branch review

Scope: `39710e5` through the current working tree, D/E/F/G production integration and meaningful domain regressions, including untracked support attribution and integration guards. Excluded the four protected initial dirty files and root-owned Explicit scene/model audit fixtures. Read-only review; no Unity/model calls, source edits, or delegation. Root's Unity scene session was left untouched.

## Verdict

PASS for the reviewed technical correctness/spec scope: no outstanding actionable P1/P2 findings remain. The historical P2 below is closed by the verified fix. No additional concrete P1/P2 ownership, event-time witness, save/load atomicity, asynchronous publication, reward quantity, or lifecycle defect was found in the reviewed paths. This is a bounded code review, not an acceptance or model-quality success claim. Root already reports that raw model quality does not meet 9.5/10; human microphone acceptance, actual scene evidence, performance interpretation and the complete suite remain separate root-owned gates.

## Closed finding

**P2 — Anonymous fallback can become an own donation by a zero-tendency named viewer.** `Assets/Game/Scripts/Viewers/ViewerSupportAttribution.cs:41–42` returns the display string `Anonymous` when no eligible donor is chosen. `Assets/Game/Scripts/Viewers/StreamEventSource.cs:226` then recovers donor identity from *every* receipt sender via `Roster.FindByName`. `ViewerProfile.Validate` allows a profile named `Anonymous`. With that profile present, DonationTendency=0, and all audience seats named, Choose correctly returns null, but the fallback receipt resolves back to that unwilling named profile. The normalized Donation event now claims that profile owns the support, allowing direct own-donation prose and the donation badge despite no C# identity selection. The new invalid-name fallback has the same collision if another present profile uses the fallback name. **Minimal correction:** ensure the generic anonymous fallback is always unowned when normalizing receipts (or carry the selected donor identity explicitly across the existing receipt path instead of reconstructing it from display text). A narrow `Anonymous` exclusion in OnDonation is safe and conservative, although legitimate profiles with that name would then have generic support attribution. Add a regression with a valid present zero-tendency profile named `Anonymous`, one authoritative simulated receipt, and assert no SubjectViewerId/direct own-outcome intent is attributed to it. Do not alter amount/count outcomes or introduce another wallet path.

## Checked contracts

- Community state owns relationships, visits, bounded memories and durable first follow/sub flags; ScriptableObjects supply configuration. Transient attendance, epochs, queued jobs and memory/promise reservations are discarded at stream/load boundaries.
- Normalized events capture immutable witnesses before draining. Delayed generation checks the same visit epoch; prompt history is filtered by event/chat witnesses, and late/absent identities do not receive earlier content.
- Reported speech remains hearsay; only typed shop/installation/start facts resolve objective promises. The gameplay adapter drains earlier normalized speech before applying a later same-frame fact, and rebaselines silently on restore.
- Save capture copies durable records, validation precedes mutation, legacy optional fields seed defaults, restoration cancels live work and does not publish receipt payouts again. Promotion is bounded and has separate persistent descriptors.
- Director jobs remain bounded, are polled on the owning thread, release callback reservations on every terminal path, and revalidate output immediately before publication. Model/fallback text does not determine audience or reward quantities.
- Direct names retain priority within existing total rhythm/event caps. Social replies require a published origin and matching witness/visit identities; reply depth and cooldown prevent recursive chatter.
- Both prior G findings are fixed: invalid presentation sender names fall back before receipt validation, and zero-threshold ValueBelow fulfillment is rejected. I inspected root's `stage-g-reviewed.xml`: 311 passed, 0 failed, duration 1.9163632 seconds. This is focused root-produced evidence, not the complete non-Explicit suite or independently rerun verification.
- The production profiling marker/counter wrap the ordinary viewer tick. The fixed 140-cell audit owns the all-raw >=100 denominator; scene performance intentionally excludes synchronous recorder overhead.

## Final fix disposition

**Closed / PASS:** StreamEventSource.OnDonation now checks the fallback `Anonymous` with `StringComparison.OrdinalIgnoreCase` and leaves its SubjectViewerId null before normalizing the Donation event. It preserves the accepted receipt and amount and prevents the namesake viewer from receiving a direct own-donation intent. The conservative limitation is explicit: a genuine profile whose display name is Anonymous also receives generic, unowned receipt attribution; that is safer than fabricating ownership and does not change rewards.

I inspected both parameterized regressions (Anonymous and anonymous), which assert a present namesake, retained sender display text, null donor identity, and no direct intent. I independently read root's `scene-acceptance-3.xml`: 23/23 passed, 0 failed, duration 51.975987 seconds, comprising 22 integration guard cases and LivingCommunityPersistenceOutageAndBilingualChat. No Unity process was launched by this reviewer. The full suite was still running at review time and is not covered by this focused result.

The verdict covers code/spec correctness within the reviewed D/E/F/G paths. It does not certify social or semantic chat quality: the fixed 140-cell raw audit explicitly fails that quality bar. Physical microphone acceptance, final full-suite evidence and detailed capture/performance interpretation remain separate limits.

## Allocation instrumentation follow-up

**PASS, no actionable finding:** reviewed the replacement of the unsupported managed byte-delta API by an editor/development-only native GC.Alloc occurrence recorder. The recorder is created in Bind on the owning Unity game thread with CollectOnlyOnCurrentThread, capacity 1, WrapAroundWhenCapacityReached and SumAllSamplesInFrame. Update resets/starts it after StreamSession.Tick, stops it in finally around the one ordinary ViewerCore.Tick, and reads GetSample(0).Count; an empty scope reports zero. OnDisable disposes and clears the handle before unbinding, and subsequent Bind creates a fresh recorder. The original tick timing marker remains around ViewerCore.Tick. The counter now correctly uses the distinct name GO! LIVE Viewer Allocations and Count unit. This is allocation occurrences, not bytes or GC.Alloc timing Value, and excludes worker inference and work outside the scope.

I inspected the two diagnostics (known 65536-byte payload calibration, plus two inside allocations / reset-to-empty / outside exclusion) and the acceptance fixture's nonzero live-load assertion. The installed Unity performance package uses the same current-thread native recorder options and Stop/GetSample(0).Count/Reset pattern. Root's allocation-diagnostic.xml records the calibration passing; its emitted evidence reports managed delta 0 and native allocation count 1. The combined 25-case scene-acceptance-4 run was still in progress at review time, so its result is not inferred here.

Root's final-full-suite.xml inspected: 1033 passed, 0 failed, 7 Explicit skips, duration 133.952533 seconds. That complete suite predates this small instrumentation change; the follow-up combined run must verify the new code. No reviewer Unity/model execution or source edits. The semantic/social-quality failure and separate physical-microphone limits are unchanged.


## Final verification disposition

**Technical review PASS, no outstanding actionable finding.** Independently inspected the final root-produced results after the allocation-counter fix: `scene-acceptance-4.xml` passes all 25 cases, 0 failures, duration 64.7632979 seconds; `final-full-suite-2.xml` records the complete suite with 1035 passed, 0 failed and 7 Explicit skips, duration 133.9535878 seconds. This supersedes earlier pending/suite-before-fix notes.

The final scene JSONL reports 12323 timing samples and nonzero native viewer allocation occurrences (mean 1.7, p95 1, maximum 824), with audit serialization/disk writes excluded from the measured interval. The unsupported managed-API zero-counter defect is therefore closed by calibration, scope regressions, actual-scene evidence, and the final complete suite. These figures count allocation occurrences, not bytes or worker-thread inference allocations, and are Unity Editor measurements rather than player-build performance guarantees.

Reviewer performed no source edits, Unity/model execution, commit or merge. Root owns the final report and commit on claude/sharp-wozniak-iakuny; no merge is authorized by this review. The technical PASS does not change the explicit semantic/social-quality gate FAIL from the 140-cell raw audit, and the human physical-microphone session A remains unexecuted.
