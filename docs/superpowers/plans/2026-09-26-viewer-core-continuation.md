# Viewer Core continuation: stages D–G

**Goal:** Continue A `4a4eb06`, B `5af07d1`, C `39710e5` on `claude/sharp-wozniak-iakuny`. Preserve those commits; commit D, E, F and G separately. Do not merge main.

**Architecture:** Keep the existing speech/event/selector/director pipeline. Add one plain-C# durable community owner at the desktop composition boundary. The roster remains a transient naming of seats inside the authoritative audience total. Text generation remains behind `IViewerLanguageModel` and cannot mutate gameplay state.

**Tech stack:** Unity 6000.6.0f1, C#, NUnit; current local OpenAI-compatible development adapter with Ministral 3 8B Q4_K_M.

## Preconditions and handoff

- [x] Inspect actual A–C ownership, lifecycle, prompt/profile data and local adapter.
- [x] Run all non-Explicit tests after C: 900 passed, zero failed; four external-input Explicit tests excluded.
- [x] Run existing reaction/director/profile tests independently: 79 passed.
- [x] Verify two confirmed handoff defects with failing tests, then minimally fix unsupported support claims on non-speech events and delayed duplicate publication.
- [x] Run the same five required contexts through all ten profiles; retain every raw model response, including rejected output. Separate conditional style auditions from actual selector willingness to speak. Evaluate the C quality gate before D implementation. Two complete auditions retained; attention/style/timing distinct, semantic grounding remains a reported quality defect for final evaluation.

## Task 1: Stage D — presence and relationship

New abstraction: `ViewerCommunity` is necessary because authored profiles cannot own runtime relationships and ChatDirector must not own presence. It owns stable `PermanentViewerState` records and exposes presence/relationship changes; it never changes the audience number.

- Use one hidden relationship dimension, **Sentiment**, -100..100, initialized from existing profile data. It means personal warmth toward the streamer. Conservative direct acknowledgement/thanks increases it with a per-viewer cooldown; direct hostility lowers it. Witnessed kept/broken promises later alter it. Visit/acknowledgement counts provide familiarity context rather than introducing extra meters. Sentiment affects deterministic attendance probability and supplied social context, never reward amounts.
- Attendance uses the existing authored schedule, interests, sentiment and a fixed seeded roll per viewer/broadcast. Arrivals and finite watch durations vary; no roll-per-frame chance inflation. At a new broadcast each viewer may return, not all attend.
- Reconcile roster shrink immediately. Named watchers never exceed the existing audience count. Track presence epochs so leaving/rejoining cannot publish an old pending response.
- Extend the desktop snapshot with an optional community snapshot; validate all identities/ranges before restore and replace state atomically. Current presence, chat, requests and timers remain transient.
- Wire the existing community asset and absolute game minutes through `DesktopRuntimeBehaviour` into plain C#.
- [x] Domain tests: joins/leaves/returns, schedule/interests/sentiment, non-universal attendance, seat limits, direct target, intended mutation, save/load and duplicate rejection.
- [x] Integration tests: absent/left viewers cannot publish, UI/LLM cannot create presence, old-save compatibility.
- [x] Run targeted tests, review and commit `Add viewer presence and relationships`: 194/194 passed; independent review and regression repair complete.

## Task 2: Stage E — witnessed memory

New abstraction: a bounded structured memory collection belongs to each permanent viewer. It solves cross-stream continuity without keeping transcripts or allowing generated prose to define truth.

- Stamp immutable witnesses/presence epochs when normalized facts occur, before asynchronous generation or later roster changes. Filter both event eligibility and recent chat/speech context by the viewer's visit.
- Store compact canonical facts: ID, type, subject, importance, emotional direction, game creation/reference time, reference count and knowledge source. Streamer-reported speech is explicitly something heard, not verified gameplay.
- Types remain small: significant failure/achievement reports, direct acknowledgement, technical incident and known promise. Filler and routine chatter do not become memories.
- Capacity: 16 memories per viewer. Decay lower-value old records and evict the least useful first; retain important/referenced facts longer.
- Retrieve at most two relevant memories by deterministic topic/type/recency/importance scoring. Cooldowns and reference counts prevent automatic callbacks every stream. Only successful, relevant publication consumes a callback opportunity.
- [x] Test witness-only creation, absence and unrelated subjects, trivial-event rejection, capacity/decay, relevant retrieval, prompt isolation, durable save/load and immutable historical truth.
- [x] Run targeted tests, review and commit `Add witnessed viewer memory`: 260/260 passed; independent spec/code-quality review approved after callback-accounting repair.

## Task 3: Stage F — promises and social callbacks

New abstraction: a small C# promise ledger is necessary to distinguish spoken commitments from objective fulfillment. Speech parsing proposes only explicit supported action/subject/time combinations. The generic state machine consumes typed gameplay facts, not model prose; subject vocabulary stays outside that state machine.

- Keep bounded stable promise IDs, canonical condition, deadline, witnesses and Open/Fulfilled/Broken/Expired status. Ambiguous, negated, quoted and speculative statements are rejected.
- Only original witnesses or explicitly recorded later knowledge may receive a promise context. No omniscient catch-up on load.
- Rare viewer replies require an actual published triggering line, both viewers present in matching epochs, a long global cooldown and reply depth one. A social reply cannot trigger another social reply.
- Anonymous people remain aggregated. Track only existing ephemeral chatters; rare qualifying promotion creates one durable identity, with a strict maximum promoted population and no duplicate after load.
- [x] Test conservative parsing, witness sets, gameplay-only transitions, eligible/non-repeating callbacks, bounded replies and deterministic rare stable promotion.
- [x] Run targeted tests, review and commit `Add viewer promises and social callbacks`.

## Task 4: Stage G — integrated acceptance

- Preserve AudienceSimulation's event-driven chat impulses and authoritative outcomes. Attribute outcomes to eligible identities before prose; never add a second wallet/follow/subscription path.
- Extend the existing Reaction Monitor with attendance, relationship, candidate/context/memory and latency information. Keep this editor-only.
- Check outage, cancellation, recovery and no replay/duplicate rewards. Keep bounded queues and one inference job at a time; no per-viewer/per-frame inference.
- Run at least 100 raw local-model generations over all profiles and varied RU/EN contexts, relationship/memory contrasts, ordinary speech and silence. Record duplicates, invalid/fallback and assistant-tone rates, length, opener repetition, collisions and latency distribution; include bad samples.
- Produce a blind identity review artifact without visible profile names and document uncertainty honestly.
- Execute real Unity persistence/outage/cross-stream witness scenarios and 1920×1080 RU/EN chat captures. A 10+ minute live-microphone session requires a person; if unavailable, provide the exact path and mark it unexecuted.
- Measure representative live queue depth, stale drops, inference and main-thread timing/allocation behavior, prompt sizes and bounded population/memory sizes.
- Document the embedded local inference migration: replace the development adapter/runtime packaging, preserve backend-independent domain systems. No replacement runtime in this milestone.
- [ ] Complete `docs/ViewerCoreMilestoneReport.md` with evidence, commit hashes and remaining quality/acceptance limitations.
- [ ] Run the complete non-Explicit Unity suite, review and commit `Complete living community integration`.

## Verification discipline

Only one Unity process may run the project; no source edits during a Unity run. Stage changes get focused tests; final gets the complete suite. The initial dirty csproj files, TMP fallback and VersionControlSettings are backed up outside the repository and excluded from commits. Model/style quality is judged from raw evidence, not the fact that compilation or an audit runner passed.
