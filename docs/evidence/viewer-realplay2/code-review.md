# Independent code review — Viewer Core Real Play Correction #2

Reviewed on 2026-09-26 by the read-only review subagent against the user request in `eb96e577-fec7-4eb7-a6c8-c91e9c5360af/Pasted text.txt` and the uncommitted changes over `5aaf708` in `E:/GO! Live`. The reviewer did not edit project source, launch Unity, invoke either model, change branches, or merge. This evidence document is the only file written by the reviewer.

**Code-review outcome:** all four concrete review findings below are addressed in the inspected source. No remaining blocking code defect was identified in the reviewed scope. Fresh full-suite and three-seed replay results for the final named-question probability fix were pending when this review was written; the root agent must append their actual results. This is not a gameplay acceptance or a claim that Viewer Core is complete.

## Findings and resolutions

1. **P1 — late group reply overwrote a newer conversation. Closed.** `ReactionSelector.ObservePublished` previously let a delayed sibling from an older group event replace a newer named exchange's owner, previous line, and turn count. Scheduled conversational intents now carry a transient `ConversationTurn`; only the current turn may mutate thread ownership. The reservation is released even for an old turn. Siblings share their original turn, and a refused repeat while the current answer is pending does not advance it. `OlderGroupSiblingCannotOverwriteANewerNamedExchange` exercises the actual ownership, previous-line, turn-count and subsequent recipient behavior. The old reply may still appear while fresh, without taking over the newer exchange.

2. **P2 — broad speech categories supplied the wrong answer task and facts. Closed.** The initial purpose resolver mapped every `PersonalQuestion` to `Mood` and every `GameplayQuestion` to `GamePreference`. Thus a meal question could authorize mood/current-state facts, and a game's price question could request a preferred genre. The inspected resolver now uses bounded positive forms for concrete purposes and falls back to `GenericQuestion` for unsupported subtypes. Game choice remains recognized; unrelated questions do not acquire viewer-day authority.

3. **P2 — a question about someone else's mood acquired the responding viewer's day. Closed.** `AboutSomeoneElse` now excludes explicit first/third-person subjects before personal mood/day classification, while preserving a turn back to the addressee and ordinary recipient wording such as `расскажите мне`. A question about the streamer's mood therefore does not instruct the viewer to explain their own day. Hard streamer facts remain separate from soft `ViewerDay` and `OwnLine` facts.

4. **P2 — explicitly named follow-up still used statement probability. Closed in inspected source; fresh full verification pending.** After fixing named-question pairing, `критик, почему?` correctly belonged to the active thread but the name prefix made the contextual predicate return false. The selector consequently used 0.6. The additional regression was confirmed red in `named-chance-red.xml`: 23 tests, 22 passed, 1 failed; the named question answered 84/150 rather than the required >125. The final selector uses `speech.AsksForAnswer || SpeechRelevance.ConversationAsksForAnswer(speech)` inside the already locked follow-up branch, selecting 0.95 for an explicit question and retaining 0.6 for a plain statement. No fallback audience lottery was reintroduced.

## Reviewed boundaries

- Target resolution precedes responder selection. Specific/active targets return from their own guarded branch, so failed rolls, absence, visit changes, pending answers, cooldowns and exhausted turns cannot redirect the primary answer to another person.
- Explicit group questions own one bounded set of unique intents tied to the same event. The extra-response probability is reduced when the existing chat budget is depleted; the initial fresh-audience distribution remains covered by deterministic tests.
- Group publication waits for earlier siblings and enforces an actual 1.4-second pause after a publication, including delayed/out-of-order model completions. Existing stale expiry, cancellation, visit checks, last-moment output validation and queue bounds remain in place.
- A direct-answer plan retains relevant supplied memory as knowledge without letting a callback replace the current task. Viewer-day/previous-line data does not become authority for streamer hardware, money, quantities or historical events.
- The old seed-44 regression now asserts the newly required silence after a failed target roll and still proves the final allowed turn increments and stops. The former explicit-scene two-answer bound was updated to the requested audience-bound maximum of three and adds uniqueness. No cooldown, stale, turn-limit or presence assertions were removed to obtain a pass.
- The source changes preserve the selected Whisper and Ministral models and do not introduce a second verifier, embeddings or a new orchestration architecture.

## Evidence inspected independently

- `full-before-named-review.xml` (originally `full-final.xml` during review): **1275 passed, 0 failed, 12 skipped/Explicit**, duration **139.5500159 s**. This predates the last named-probability one-line fix and added test case.
- `replay-initial.xml` (originally `replay.xml` during review): the first fixed three-seed replay harness passed its single explicit test, duration **150.8169796 s**. Its pass concerns harness/routing invariants, not natural conversation quality.
- `model-replay/realplay2-replay-20260926-191926-988.jsonl`: 47 model-result records, 47 director outcomes, 46 publications; summary reports zero invariant violations. The manifest explicitly records partial reconstructed state, missing original PRNG/rhythm/day information and the assumed earlier Zina day. This is a recognized-text replay, not a fresh microphone capture.
- `named-chance-red.xml`: independently inspected the failing named-follow-up test before reviewing its source fix.
- `git diff --check` over the reviewed runtime/test scope reported no whitespace errors.

## Remaining gameplay quality — not closed by this review

The first replay still fails the semantic gameplay gate. In the recorded sequence-7 fixture, all three attempts kept kritik as the answerer, but the outputs were weak: seed 1 rejected the model's unsupported recurrence and published the generic fallback `норм, а ты как?`; seed 2 published `ранки всё ломят`; seed 3 published `рандомные матчи меняют весь день`. Correct target ownership and a valid structured plan are not evidence of a coherent answer.

The existing fallback path remains broad: a failed personal/explanation follow-up may select a stock personal line rather than answer the supplied reason. This is a documented remaining quality limitation, separate from the corrected routing and fact-authority defects. Do not report the quality gate as passed, do not claim Viewer Core complete, and do not merge. Preserve the unchanged models and present only the requested fresh 60–90-second human acceptance as the next gate.

## Final-run evidence to append

The root agent is rerunning the full non-Explicit suite and the complete fixed seeds 1–3 replay after the named-question probability correction, retaining the first replay as pre-review evidence. Append the actual result paths, counts and quality assessment below; do not infer their success from the prior run.

## Final evidence appended by root

After the named-question fix: full-final-2.xml = 1276 passed, 0 failed, 12 Explicit skipped, 137.6949784 s. Final replay-final.xml = 1 Explicit passed, 0 failed, 150.8607236 s; the complete fixed 1/2/3 matrix reports 47 adapter OK results and zero routing invariant violations. Final raw output is model-replay.jsonl; the earlier matrix is model-replay-initial.jsonl. The semantic gameplay gate remains unaccepted, including the retained incorrect fallback in the first recorded why fixture. Only trailing blank lines in the replay fixture were removed after these runs; runtime/test behavior was unchanged.
