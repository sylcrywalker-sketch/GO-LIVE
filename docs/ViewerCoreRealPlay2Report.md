# Viewer Core — real-play correction 2

Continues `5aaf708` on `claude/sharp-wozniak-iakuny`. No merge to main. Viewer Core remains subject to the next human gameplay gate.

## Source evidence

The primary input is the newest human session, `Logs/ViewerSessions/session-20260926-212336.jsonl`. Its unchanged [copy](evidence/viewer-realplay2/source-session.jsonl) has SHA-256 `2B1B0E28535D41F702128FC38136B93A0575F9F7A9A2F510A2BB34ED5676F7A2`. The [source assessment](evidence/viewer-realplay2/source-assessment.md) and [extracted chains](evidence/viewer-realplay2/source-evidence.json) distinguish recorded facts from inferred causes.

The trace confirms kritik's “без настроения но здесь сижу” followed by “Почему без настроения?” going to Zina. The later group request misses personal-state classification and produces “девочка с девятого этажа”; the named clarification goes to Zina, and the next clarification to an anonymous viewer. The existing fuzzy matcher already recognizes “критика” as “критик”; adding an alias would not fix the audience fallback.

The actual recorded day is `krit.ranked / Stressed`, not the illustrative school day in the request. The source also contains a later two-response group turn; the old implementation was not universally limited to one respondent. Original audio, the complete audience/PRNG state, game time and scheduled cooldown times are unavailable. Original named join keys record presence epoch 1.

## Changes and state ownership

`ReactionSelector` resolves one immutable `ConversationTarget` before choosing responders: None, SpecificViewer, ActiveThreadViewer or Group. A specific target either answers or remains silent; it never falls through into the ordinary audience lottery. Explicit names take priority; explicit plural speech starts a group turn. Contextual why/explanation questions and punctuation-free personal questions can retain the thread owner. Ownership stays distinct from eligibility so an absent, departed/rejoined, cooling-down or exhausted viewer cannot be impersonated by another candidate.

Each intent snapshots the target and previous viewer message. A transient conversation-turn serial prevents a late answer to an older group event from overwriting a newer exchange's owner, line and turn count. Denied pending repeats do not advance that serial. No save data, MonoBehaviour business logic, global manager or new lifecycle service was introduced.

The bounded `QuestionPurpose` classifier selects what must be answered. Relevant mood/Today/Now fields become separate soft `DirectAnswerFacts`. A follow-up prompt explicitly pairs `YOUR PREVIOUS MESSAGE` with `STREAMER REPLIED TO YOU` and `YOUR TASK`. Personal answers supply content before personality; group questions remain directed to the streamer. Unknown meal/price questions and questions about the streamer or someone else do not borrow the viewer's daily state. Supplied relevant memory retains its knowledge source and hard-fact grounding even when a callback cannot replace the current answer.

At 1–3 viewers, one group speech event owns at most 1/2/3 unique intents. The first-answer chance remains .92; a fresh three-person group normally schedules two answers. Personality weights, availability and cooldown still affect selection. Extra answers are less likely when recent conversations have depleted the ordinary budget. Scheduling separates due times, and publication also enforces a 1.4-second gap when delayed model results complete together. Older pending siblings cannot publish out of order. Expiry, cancellation, queue limits and same-visit checks remain active.

The existing rare viewer-to-viewer path is unchanged. No second verifier model or embeddings were added. The monitor and session recorder now include target, purpose, answer facts and the captured previous line. Published text is populated before the log notification, so recorded reaction rows retain it.

## Regression method

The baseline [RED run](evidence/viewer-realplay2/regression-red.xml) compiled 49 new cases: 44 failed, five passed. Subsequent review negatives reproduced simultaneous/out-of-order group publication, stale-wave ownership theft and overly broad purpose/day inference before repairs. Intermediate results, including the failed first full run, are retained alongside the final result.

The existing ten-minute chat-density test was kept unchanged: the repaired small-group energy rule produced 15 scheduled messages with a longest gap of 113 seconds at three viewers, versus 118 at fifty viewers. The 300-seed fresh-question sweep produced counts for zero/one/two/three replies of `24/276/0/0` at one viewer, `24/115/161/0` at two, and `24/26/203/47` at three. These measure deterministic selection, not human-play quality or guaranteed publication.

Two existing assertions necessarily track the new requested behavior: the seed-44 continuation test now requires silence after the failed target roll, then verifies the next target answer still consumes the final allowed turn; the older Explicit scene replay permits up to the actual 1–3 audience and additionally requires unique responders. Window, cooldown, absence, stale cancellation, factual knowledge and ordinary density assertions were preserved.

The final review also reproduced a named-prefix probability regression: “критик, почему?” produced only 84 continuations in 150 fixed seeds because the contextual lexical helper missed the leading name. An already resolved follow-up now treats the explicit question act or the bounded contextual predicate as sufficient for the .95 question path. The new case and all existing cases pass in the final ordinary suite.

**Final complete non-Explicit Unity result: 1,276 passed, zero failed, 12 Explicit cases skipped, 137.695 seconds.** No ordinary test was ignored. This includes unrelated scene, save/load, voice, desktop and peripheral checks. [Final XML](evidence/viewer-realplay2/full-final.xml), [named-question RED](evidence/viewer-realplay2/named-chance-red.xml), [independent code review](evidence/viewer-realplay2/code-review.md).

The first complete replay before that final probability fix is also retained: [raw evidence](evidence/viewer-realplay2/model-replay-initial.jsonl), [Explicit result](evidence/viewer-realplay2/replay-initial.xml), [exhaustive assessment](evidence/viewer-realplay2/replay-assessment-initial.md). It had 47 successful adapter results, zero routing invariant violations and a **failed semantic gameplay gate**. The complete matrix was rerun only after the code fix, with the same predetermined seeds 1–3; no seed or successful sample was substituted.

## Final fixed replay and quality verdict

The final [Explicit replay test](evidence/viewer-realplay2/replay.xml) passed its mechanical assertions in **150.861 seconds**: 45 speech selections across the same three fixed selector seeds, 47 responses from the model adapter, **zero routing invariant violations**. Seven raw model texts were rejected; 40 model messages and five fallback messages were published, and two reactions were discarded. [All raw requests/results/decisions](evidence/viewer-realplay2/model-replay.jsonl) and the [exhaustive final assessment](evidence/viewer-realplay2/replay-assessment.md) are retained. Model sampling is not seeded by this fixture; the fixed seeds control selection and fallback, not identical model wording.

This is a controlled **recognized-text replay**, not fresh microphone capture or an exact full-state restoration. It uses the production selector, planner, prompt, adapter, validator and director. Named authored profiles and recorded day entries are used where available; the third seat, original PRNG/rhythm and earlier use of Zina's later recorded day are explicitly reconstructed. Independent recorded-context fixtures retain original prior lines. Fresh-generation episodes inject no original answers, preserve sequence 6–12 order, and run the much later group question 35 as a separate episode. A new generated answer can change the appropriate conversational partner, so the unchanged subsequent original speech does not automatically fit that new answer.

In the recorded-context fixture, all three “Почему без настроения?” attempts resolve `ActiveThreadViewer(viewer.kritik228)`, with previous line “без настроения но здесь сижу”, purpose `ReasonForMood` and these true facts: stressed mood, normal energy, ranked games with repeated losses today, currently moving between small streams.

| Seed | Raw model text | What was actually published | Model / visible delay |
|---|---|---|---|
| 1 | “ранки опять сдули и всё” | Rejected for unsupported recurrence; existing fallback **“норм, а ты как?”** from kritik. This does **not** answer the mood question. | 0.440 / 2.505 s |
| 2 | “рандомит и проигрываю каждый раз” | Same kritik, model text published; gives the supplied ranked-loss reason. | 0.452 / 2.512 s |
| 3 | “рандомный поток и поражения в рейтингах” | Same kritik, model text published; relevant ranked-loss content, awkward wording. | 0.453 / 2.365 s |

The exact recorded named clarification in sequence 11 correctly stays targeted at kritik in all three snapshots. It remains silent under the reconstructed cooldown: 5.899 seconds since his preceding original publication, below the unchanged six-second guard. Silence is not counted as a wrong-recipient reply or rewritten into success. Eligible named selection is separately tested across deterministic seeds.

**Semantic gameplay quality is still not accepted.** Correct routing and the mechanical replay pass do not certify meaningful speech. The unchanged model still produces weak or unrelated answers and unsupplied details in other turns; the full assessment includes every result, not only the relevant ranked-loss answers above. The existing generic fallback is a separate remaining limitation: after a rejected reason answer, it can publish a stock mood line that does not answer “why”. No second verifier, model switch, probability workaround or hidden retry was used to make these outputs look successful.

## Model boundary and next gate

Whisper remains `ggml-large-v3-turbo-q5_0.bin`, SHA-256 `394221709CD5AD1F40C46E6031CA61BCE88931E6E088C188294C6D5A55FFA7E2`. Chat remains `mistralai/ministral-3-8b-instruct-2512` with the authored settings. No model/configuration changes were made.

The four pre-existing unrelated modified files were restored byte-for-byte from the initial backups and excluded from the correction commit: both generated C# project files, the TMP fallback font asset and `ProjectSettings/VersionControlSettings.asset`.

The fresh [60–90 second microphone procedure](ViewerCoreRealPlay2MicrophoneAcceptance.md) is prepared. It asks a group question, follows one answer, explicitly names Zina and checks filler silence. It has not been performed during this correction. Correct routing and automated tests alone do not establish that the conversation feels human.
