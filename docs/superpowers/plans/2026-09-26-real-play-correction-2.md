# Viewer Core real-play correction 2

**Goal:** Preserve conversational ownership, supply meaningful answer content, and let a tiny group answer one group question naturally. Continue current branch from 5aaf708; do not merge main or change either model.

**Primary evidence:** Logs/ViewerSessions/session-20260926-212336.jsonl. Preserve original bytes and distinguish recorded state from reconstruction. The user supplied the approved design and acceptance requirements.

## Design and boundaries

- Add one immutable `ConversationTarget` value and one resolver owned by the existing ordinary C# selector. It describes None, SpecificViewer, ActiveThreadViewer or Group, plus viewer/epoch. Resolve before chance, budget or weighted selection. Specific targets never fall through into a weighted lottery; eligibility failure means silence. Explicit plural phrases interrupt a 1:1 exchange, while named targets outrank plural cues. No new global manager or save state.
- Snapshot the resolved target and previous thread message into each reaction intent so asynchronous generation cannot pair the question with a newer unrelated line. Existing director lifecycle, cancellation, stale checks and presence epochs remain authoritative.
- Extend the ordinary C# utterance plan with bounded QuestionPurpose, direct answer facts and a paired previous-line/current-question task. Structured personal questions answer first; personality styles the answer. Soft viewer-day facts remain separate from hard streamer facts. Do not add another LLM or semantic database.
- A small-group question is one original speech event and one bounded list of distinct reaction intents (the response wave); intent order and increasing due times stagger it. Up to 1/2/3 responders for audience 1/2/3, with two normally selected at three viewers and a minority third. Existing personality weights, availability and bounded conversation budget apply. Do not create AI-to-AI chatter events.
- Add target/purpose/answer-fact/previous-line diagnostics to the existing monitor/session recorder. These are development data, not game UI or persisted runtime state.

## Execution and checks

- [x] Preserve newest session and original four unrelated dirty files; inspect actual routing/semantic failures and available replay state.
- [x] Write deterministic targeting/group and answer-purpose tests, run RED before production changes.
- [x] Implement target ownership and bounded staggered group wave; integrate semantic plan and diagnostics; run relevant Viewer checks and correct regressions without weakening existing assertions.
- [x] Replay exact recognized sequences from newest session with known authored viewers/day facts where recoverable; label reconstruction and missing data. Retain targets/responders/purposes/facts/previous line/model output/latency and every failure.
- [x] Independently review code and evidence. Run full non-Explicit Unity suite with zero failures, including existing unrelated tests.
- [x] Prepare fresh 60–90 second human microphone gate (group, follow-up, named Zina, filler). Document model quality limitations separately and preserve evidence for the current-branch correction commit. No merge and no Viewer Core completion claim; human acceptance remains unperformed.

Evidence root: E:/GO-Live-RealPlay2-audit-20260926. Root alone runs Unity. Semantic-plan agent owns planner/context and its new tests; root owns selector/target/group and integration; independent evidence analysis is read-only. All source writes pause during Unity runs.
