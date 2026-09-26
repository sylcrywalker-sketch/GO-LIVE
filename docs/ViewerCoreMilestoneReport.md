# Viewer Core milestone — continuation report

Status: **D–G implemented; social quality pass applied (see [SOCIAL QUALITY PASS](#social-quality-pass)); human Session A still pending.** Work continues on `claude/sharp-wozniak-iakuny`. A–C commits are preserved: A `4a4eb06`, B `5af07d1`, C `39710e5`. Nothing merged to main. Domain correctness, persistence and outage checks pass. The grounding pass removed most invented facts, role and relationship defects in the fixed audit; incoherent lines and softer history presumptions remain. No 9.5/10 is claimed. The human ten-minute microphone session remains unexecuted and is required for completion.

## Handoff audit of actual A–C code

`VoiceInputBehaviour` captures real audio. Plain `VoiceRecognition` coordinates VAD and the recognition worker; results are drained on the game thread. `StreamSpeechFeed` accepts nonduplicate phrases only while Live and rejects speech ending before this broadcast. It is independent of the fictional microphone item.

`StreamEventSource` normalizes speech, accepted donation receipts, aggregate counter deltas, silence/away periods and peripheral changes. `SpeechRelevance` assigns cues/topics/relevance and matches known name forms. `ReactionSelector` chooses a watching participant and delay with deterministic randomness and audience-scaled chat rhythm. `ChatDirector` runs bounded asynchronous generation through `IViewerLanguageModel`, validates text and publishes to `StreamChat`; the existing stream overlay reads that chat.

`AudienceSimulation` is authoritative for total viewers and aggregate outcomes. `StreamSession` accepts donation receipts; `DonationPayout` credits the wallet. `DesktopState` applies completion to Trich/Outline. None of the text-generation pipeline creates money, followers or subscriptions.

Stage C supplies ten authored permanent profiles, structured style, interests, schedules, affinities and initial sentiment. At the audited A–C handoff, the community asset was configured but runtime attendance was not connected; Stage D below closes that seam. The original `AudienceRoster` exposed join/leave but needed shrink reconciliation before enabling runtime named viewers.

The current backend is `OpenAiCompatibleChatModel`, with a local-only endpoint, request timeout, cancellation, bounded output and JSON text contract. `ChatDirector` polls task results on the game thread, backs off after transport failures and uses fallback or silence. Broadcast end/load cancels and removes pending publication paths. Stage D adds the required visit epoch to pending generation once viewers can leave and return.

Two concrete pre-existing issues were reproduced with failing regressions: nullable speech-topic checks allowed unsupported support claims on non-speech moments, and two generated lines could both pass duplicate validation before their delayed publication. Both were narrowly fixed in `86145b5`; A–C were not rewritten.

## Baseline verification after C

Unity 6000.6.0f1 complete non-Explicit suite: **900 passed, 0 failed**, 134.14 seconds. Four Explicit cases require a person/microphone, recorded voice or a local model and were excluded by the normal suite. Existing reaction/director/profile target: **79/79 passed**. The handoff's quoted 125 targeted total is not used as evidence; these are the actual results in this checkout.

Evidence: [complete baseline](evidence/viewer-core/stage-c-full-suite.xml), [baseline targeted](evidence/viewer-core/stage-c-targeted.xml), [two failing handoff regressions](<E:/GO-Live-ViewerCore-audit-20260926/handoff-red.xml>).

## Current development model

Local server inventory confirms `mistralai/ministral-3-8b-instruct-2512`, GGUF **Q4_K_M**, loaded context **4096**. Current settings: temperature 0.85, top-p 0.95, maximum 64 output tokens, 6-second timeout, one concurrent request and queue capacity six. LM Studio is a development runtime, not shippable game packaging.

Implementation and acceptance are tracked in [the continuation plan](superpowers/plans/2026-09-26-viewer-core-continuation.md). Raw comparisons, stage verification, quality review and acceptance evidence are retained below, including unsuccessful scene-fixture attempts.

## Stage C profile gate: raw evidence before continuation

The [first complete comparison](evidence/viewer-core/stage-c-before.md) contains all ten profiles on the five required situations. [Machine-readable records](evidence/viewer-core/stage-c-before.jsonl) retain prompts, raw HTTP responses, parsed text, rejection reasons, fallback candidates and latency. This is a **conditional style audition**: one forced generation for every cell, no retries and no output selection. It is not evidence that all ten would speak. Actual selector measurements separately use all ten competing for ten seats over fixed seeds 0–199, with ordinary rhythm.

First run: 50/50 backend responses, four validator rejections (8%), zero exact duplicate lines, mean 39.3 characters; median 287 ms, p90 413 ms, p95 439 ms, maximum 485 ms. Fallback entries in this artifact are candidates, not observed runtime fallback rates. All ten profiles were silent for low-value speech in all 200 selection trials. On a chat question PixelFox was selected 49/200 times versus Mika 8/200; their mean delays were 5.52/5.96 seconds. Zina's question response delay was 7.79 seconds. On equipment failure ByteCat/Sovetnik were selected 30/33 times versus Mika once.

The profiles are not merely the same reaction plus different signatures: a reserved Mika, technical ByteCat, older concerned Zina, blunt kritik and English ArcadeKid differ in attention, length and timing. However, the first raw audition is **not a production-quality pass**. Accepted failures include Jonas asking about Germany during an unrelated game mistake, ByteCat inventing an upgrade, Mika saying `тупой как орех`, and Sovetnik inventing a game with a microphone on the table. Four output-limit/profanity rejections do not capture these semantic defects. Low-value forced outputs are also often irrelevant, although the runtime selector correctly suppresses those moments.

Minimal corrections address actual prompt contradictions: biography must shape voice rather than invent the topic, gentle personalities may remain warm without canned praise, teasing must fit the authored person, and a question habit must fit the conversation. Jonas's authored English-heavy note conflicted with his Mixed language rule; only that note is corrected in both asset and authoring source. No other profile is rewritten. The later raw audition and final integration audit determine the remaining quality limits; unit tests cannot establish natural conversation quality.

The [second complete audition](evidence/viewer-core/stage-c-after.md), with [all raw records](evidence/viewer-core/stage-c-after.jsonl), keeps the same 50 cells and settings. **86/86 checks passed**, including this Explicit audition; this verifies execution and prompt contracts, not naturalness. Distinct attention/voice/timing is demonstrated, so the specific failure condition “same semantic reaction plus a slang token” is not the overall pattern. The broader quality gate remains open: ByteCat still invents an unseen CPU, Doshirak brings up food without cause, and Mika is not consistently gentle. These failures remain visible in both artifacts and must be assessed again after integration. No claim of 9.5/10 or a production-ready chat is made.

## Stage D — presence and relationships

`ViewerCommunity` is the new plain-C# owner because neither authored ScriptableObject profiles nor the text generator should own durable social state. It keeps the existing `AudienceRoster` as transient seats inside AudienceSimulation's total. It never changes that total. Authored profiles, absolute game minutes and explicit content hints reach it through the existing desktop composition/runtime; recent recognized speech contributes a topic hint for 90 seconds.

Each profile gets one fixed broadcast-seeded attendance roll, an arrival after 5–90 seconds and a 3–15 minute dwell. Current schedule, interests and sentiment can change eligibility without rerolling each frame. Chance is authored regularity × schedule factor (1 inside / 0.18 outside) × interest factor (1.15 related / 0.65 unrelated) × (1 + sentiment/250), capped at 0.95. Arrival order is seeded, not catalog order. Viewers can leave and return in later broadcasts; not every profile attends. Shrink removes excess named and ephemeral watchers immediately. Only actual anonymous chatters receive transient identities (maximum 128), while the remaining audience stays a number.

One hidden relationship dimension is sufficient here: **Sentiment**, -100..100, personal warmth toward this streamer. It starts from the profile. A direct exact-name acknowledgement adds 1, thanks adds 3, narrow explicit personal hostility subtracts 4; a 60-second per-viewer cooldown prevents repeated speech farming. Familiarity derives from visit/acknowledgement counts. Sentiment changes return likelihood and a bounded social description in the prompt, without replacing the authored personality or determining money. The UI has no relationship meter. Fuzzy transcription matching remains useful for reaction priority, but cannot alter a similar nickname's durable relationship.

Presence epochs invalidate old visits before generation and again before publication. Leaving/rejoining or loading a save cannot revive an in-flight response. The optional community snapshot holds stable IDs, sentiment and counters; missing legacy rows seed current defaults. Validation rejects unknown/duplicate identities, null records and invalid ranges before touching any desktop state. Snapshot capture/restore does not share mutable records with the caller. Plans, visitors, cooldowns, chat and model requests are transient.

Verification: eight initial RED cases; the subsequent integration run exposed a real fuzzy-name social mutation and missing schedule validation (fixed), plus two incorrect expectations in new tests (actual email domain and lifetime visits versus current seats). Independent review caught thanks to an absent known viewer being misattributed to the last donor. A failing regression proved it; cached known-name matching now prevents that inference, with an independent conservative check in the community. Re-review approved. Final targeted **194/194 passed**, including viewer/director/speech/audience and desktop persistence/completion checks. Evidence: [D final results](evidence/viewer-core/stage-d-final.xml). Event-time knowledge isolation follows in E; D does not claim to implement memory.

## Embedded Local Inference Migration

LM Studio plus the loaded Ministral 3 8B Q4_K_M model is the **development backend**. Shipping cannot require players to install it. `IViewerLanguageModel` is the replacement boundary: request text, token budget, cancellation and a typed result. AudienceSimulation, StreamSession, normalized facts, deterministic selection, community state, save data, validation and the visible chat remain backend-independent. The LM Studio CLI menu is editor-only tooling.

Two candidate packaging paths are a private child process with a loopback protocol, or a native inference library behind a worker-thread adapter. llama.cpp's server supports CPU/GPU inference, OpenAI-compatible chat completions and schema-constrained JSON; that makes the first path plausible without changing the domains. This is an integration candidate, not a validated product choice. [Primary server documentation](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md).

Production work would add lifecycle ownership (start/readiness/exit/cancellation/crash recovery), signed platform binaries, model acquisition/version/hash checks, tokenizer and chat-template compatibility, a restricted local endpoint or native ABI, driver/backend selection, and memory/thread budgets shared with Unity and speech recognition. It must be benchmarked under gameplay load, including CPU fallback and hardware below this workstation. Packaging also needs a review of runtime/model redistribution terms and notices. The publisher provides a GGUF model and Q4_K_M usage examples, but that alone does not validate the game's build/distribution path. [Publisher model card](https://huggingface.co/mistralai/Ministral-3-8B-Instruct-2512-GGUF).

Eight billion weights at four bits have a theoretical floor of roughly 4 GB before quantization metadata, non-four-bit tensors, KV cache and work buffers. Actual package/RAM/VRAM requirements must be measured for the exact shipped file, context and backend; do not advertise that floor as a minimum GPU requirement. Context and generation budgets remain bounded; one shared inference worker is sufficient for this chat design. Smaller compatible models can be auditioned later through the same adapter contract. No embedded runtime is implemented in this milestone.

## Human microphone acceptance — prepared, UNEXECUTED

Session A needs a person speaking for 10+ minutes; an autonomous replay does not stand in for that acceptance. Existing real-device Explicit test checks RU/EN admission and post-stop isolation, but is shorter and does not establish the ten-minute conversational experience.

1. Open `Assets/Game/Scenes/GL.unity` in Unity, select a 1920×1080 Game view, start the configured local model with `GO! LIVE → Viewer Core → Start Local Model (LM Studio)`, then enter Play Mode.
2. Boot the starter PC, sit at the monitor, create Outline/Trich accounts, install/open Streamly and connect the channel code. Choose a supported quality and go Live. Enable real voice input in the existing voice controls. The fictional desk microphone is optional equipment, unrelated to OS capture.
3. Open `GO! LIVE → Viewer Core → Reaction Monitor`. Confirm actual recognized phrases appear as speech events, then as selection/silence and ordinary model/fallback output. Tick **Record session** (social quality pass) so every decision, plan, chat line and Game-view hitch is written to `Logs/ViewerSessions/session-*.jsonl`; it stops automatically when Play Mode exits. Start a real ten-minute timer; retain a screen recording and monitor trace.
4. Minutes 0–2: greet chat, then use filler such as `так, секунду, ага` with natural pauses. Minutes 2–4: ask which game to play and address a **currently present** viewer by name. Check that only the intended viewer receives acknowledgement.
5. Minutes 4–6: describe a mistake and a success without prescribing the response. Leave at least 75 seconds of natural silence. Minutes 6–8: remove/reconnect the in-game microphone while the real OS microphone remains available; broadcast and recognition must continue independently.
6. Minutes 8–10+: speak a few English phrases, ask a final question, then stop the broadcast and speak once more. New speech must not enter the stopped stream. Check for filler suppression, plausible questions, quiet periods, no repetitive bot chatter and no inference-related frame freezes. Capture RU/EN chat readability and log any incorrect name, memory or invented-fact claims.
7. Optionally run the existing Explicit `RealMicrophoneRussianAndEnglishReachTheStreamOnlyWhileLive` in Test Runner to retain a separate device-path regression result. It does not replace the conversational ten-minute session.

Acceptance workstation for the autonomous audit: Ryzen 7 7800X3D (8 cores / 16 threads), RTX 5070 with 12,227 MiB reported VRAM, driver 616.56. Latency on this machine is not a minimum-spec performance guarantee.

## Stage E — witnessed structured memory

Each permanent viewer owns a plain `ViewerMemoryBank`; anonymous chatters have no durable bank. Normalized events capture an immutable witness set with visit epochs, anonymous seat count and the last supplied game-clock sample. The event stamp is made before draining the queue and cannot be replaced. Roster synchronization before callback normalization prevents a donation observer from using an obsolete audience total. A late join, including one at the same timestamp, cannot inherit the event. Someone who witnessed it and left before processing can still retain the fact.

Current chat lines have separate publication-time witness snapshots, bounded by the 60-line chat capacity. A viewer's generation context contains only speech/chat witnessed during their current visit. A newly named anonymous seat can react to its triggering fact but does not inherit prior speech or chat. Visible recent chat is quoted hearsay, not proof of firsthand gameplay knowledge.

Four small memory types cover significant first-person failure/achievement reports, intended personal thanks and the first retained technical microphone/webcam incident for interested witnesses. Filler, ordinary kills, promises, negated outcomes and narrow quoted third-party reports are omitted. Reported speech uses `HeardStreamer`, explicitly distinct from an observed technical fact. Canonical subjects and fields contain no transcript or generated diary.

Capacity is **16 facts per permanent viewer**, with deterministic eviction by importance/reference count/age/ID. Base expiry is 7/30/90 game days for low/medium/high importance. A successful reference refreshes the horizon, capped at 14/60/180 days from creation. Retrieval supplies at most two relevant facts and excludes the current fact, future records, unrelated subjects and exhausted/reserved callbacks. Each fact can be referenced at most three times, at least 120 game minutes apart. Failed/cancelled/stale/irrelevant publications release reservations without consuming a callback. Only a relevant published line updates reference metadata; model text never creates or changes historical facts.

The optional memory save field preserves older saves. Detached capture/restore and whole-graph validation reject invalid enums/subjects, non-finite times, duplicate IDs/event keys, oversized banks and inconsistent reference metadata before any desktop mutation. Repeated load does not duplicate facts. In-flight requests, reservations and chat remain transient.

Initial three regressions failed as intended. The first expanded run passed 180/182; one legacy test treated an invented yesterday claim as valid, and another arranged two named viewers inside an actual one-seat audience. Those fixtures now assert the new historical boundary and actual audience requirement. Three extraction regressions and one finite-retention regression were separately reproduced before fixes. Semantic history validation remains lexical rather than proof of arbitrary generated claims; the strict enforceable boundary is which canonical facts enter a viewer's context.

Final E expanded regression: **260/260 passed**, zero failed, 1.1148458 seconds ([Unity result](evidence/viewer-core/stage-e-complete.xml)). Independent review found one concrete accounting defect: a callback about losing a final consumed both loss and victory memories for that subject. Two failing regressions reproduced it; publication now consumes at most one compatible fact and rejects explicitly opposite known outcomes. A separate failing English case prevents `wonderful` from being parsed as `won`. The final run includes all D fixture groups, memory, stream session and desktop state/save integration.

Final independent Stage E re-review: specification PASS, code quality PASS; no outstanding actionable findings.

## Stage F — promises, social replies and promotion

`ViewerPromiseLedger` is a plain C# state machine with at most 16 structured commitments. The separate RU/EN vocabulary accepts explicit purchase/install commitments for tomorrow or by tomorrow, and starting the next stream earlier. Questions, quotation, negation, speculation and unsupported clauses are rejected. Subject vocabulary stays outside objective comparisons. No transcript is saved and model text cannot add or resolve a promise.

The ledger separates objective status from each original witness's knowledge. A purchase witnessed by one original listener does not tell an absent listener that the promise was fulfilled. Their known state remains Open, meaning outcome unknown. Fulfillment comes from actual paid shop orders, installed item identities and broadcast starts through an explicitly composed adapter. Loading suppresses this adapter and rebaselines it afterward. A cart, spoken claim or restored installation is not a new completed fact.

Tomorrow uses the next calendar-day window; by tomorrow includes the remaining current day. Earlier compares the next actual start's minute-of-day against the original start. No next broadcast within seven game days expires the condition as unverifiable. Terminal records age out after seven game days; Open records are never evicted, and an admission watermark prevents replay after eviction. One relevant known promise may enter a prompt, at least 120 game minutes between recognized callbacks and at most three lifetime references. Only witnesses to a known fulfilled/broken transition receive +2/-2 sentiment once. Deadline processing continues offline. Actual start-time witnesses are uncommon because authored viewers arrive after going Live; later arrivals deliberately receive no automatic outcome catch-up.

Rare social replies originate only from an actual published line. Both permanent viewers must still have the captured visit epochs; maximum depth is one, chance is 0.03 per eligible trigger, and there is at least a 180-second global gap plus the ordinary chat budget. No reply chains or separate AI conversation loop exist. Model outage leaves these optional social replies silent.

Promotion considers only an already named ephemeral chatter after three published lines and eight minutes. Each identity receives one seeded 0.02 eligibility roll, with at most one promotion per broadcast and eight additional permanent profiles overall. A compact generated descriptor and stable identity survive save/load; anonymous audience remains aggregated. All durable names, including absent promoted viewers, are reserved against future ephemeral generation.

Independent review reproduced a same-frame ordering defect: recognized commitment speech was queued while a checkout was observed immediately, so the purchase could be lost before the next viewer tick. The core now processes earlier normalized events before observing the completed gameplay fact. Generation still begins during its ordinary tick. The regression also checks purchase-before-speech exclusion and absence of repeated acknowledgement or scheduling. Final F expanded regression: **291/291 passed**, zero failures, 1.244557 seconds ([Unity result](evidence/viewer-core/stage-f-complete.xml)). Independent re-review: specification PASS, code PASS, no remaining actionable findings.

## Stage G — integrated authority, rhythm and observability

`ViewerSupportAttribution` names an already decided C# outcome using eligible current seats and authored support tendencies. It does not choose donation amounts, create receipts or credit the wallet. Desktop composition injects it before the existing `DonationAccount.Receive` call; the old four-name donor rotation is removed. A donor may be a current permanent viewer or an ephemeral identity occupying an anonymous seat. Unattributed support stays Anonymous. The aggregate simulation remains the sole owner of viewer/follow/subscription totals.

Two persistent flags prevent repeatedly presenting a permanent viewer as a new follower or first subscriber. Old saves default both flags to false. These are lifetime first-event attribution flags; renewal, subscription expiration and billing are outside this game's current model. Follow/subscription presentation receives a C# attribution after its authoritative aggregate outcome. No separate economy path exists.

Direct-name responses now obey the existing total rhythm budget and per-event cap. This closes a demonstrated case that scheduled 35 replies to a mass-name phrase where the event limit was three. Meaningful events, audience impulses, speech and silence still drive selection; no global chat timer is added. A narrow first-person transaction guard requires the correct own C# donation/follow/subscription event, while discussion about support remains allowed. Promise saves also reject terminal timestamps/statuses unreachable through actual transitions.

The existing editor Reaction Monitor now exposes actual attendance, relationship counters, first-support flags, candidate IDs, selected memory/promise IDs, relationship context and p95 latency. Candidate capture is capped at 64 IDs and the existing trace remains capped at 80 entries. Diagnostic string construction is editor/development-only. The release interface is unchanged.

Ordinary `DesktopRuntimeBehaviour.Update` contains one `GO! LIVE Viewer Tick` marker around its existing viewer update. The editor/development `GO! LIVE Viewer Allocations` counter measures native `GC.Alloc` **occurrences** on that same thread during that update. Its instance recorder is created on binding, reset for each scope and disposed on disable. It excludes StreamSession's preceding simulation step and worker-thread inference allocations; whole-editor bytes are reported separately. There is no second update loop.

All six initial G regressions failed for their expected defects. Expanded targeted verification first passed **309/309**, zero failures, 1.3583946 seconds. New coverage includes 3,600 simulated seconds of repeated model outages/recovery against a same-seed baseline, comparing authoritative audience, follows, subscriptions, accepted support and unique receipt IDs.

Independent review then reproduced three edge cases: an invalid display name discarded an otherwise valid simulated donation; a saved before-midnight condition accepted an impossible fulfilled status; and a real profile named Anonymous could inherit a generic support receipt. Name validation now falls back before receipt creation, save validation follows reachable transitions, and the case-insensitive Anonymous sentinel remains unattributed. Explicit spoken acknowledgement of that profile still works. The first two repairs passed [311/311 expanded checks](evidence/viewer-core/stage-g-reviewed.xml); the final two case variants passed with the complete **22-case G fixture** plus scene acceptance, [23/23, zero failures](evidence/viewer-core/scene-acceptance-3.xml). [Independent technical re-review](evidence/viewer-core/technical-review.md): no remaining actionable findings in the reviewed technical scope. This is separate from the failed dialogue-quality gate.

## Final 140-generation quality audit and blind review

The integrated ordinary adapter/director ran a fixed 14-context × 10-profile matrix, **140/140 real local generations**, with no retries or selected replacements. Each cell is a conditional style fixture, not a claim that every profile would naturally speak. It covers RU/EN questions, failure/win/filler/silence, microphone degradation, direct thanks, cold/warm relationships, witnessed versus absent memory and known fulfilled/broken promises. Actual C# community/ledger state supplies those contexts. [Every raw request, wire response, result and publication](evidence/viewer-core/viewer-milestone-raw.jsonl), [complete readable table](evidence/viewer-core/all-raw.md), [metrics](evidence/viewer-core/metrics.json), [Unity execution result](evidence/viewer-core/model-audit-results.xml).

| Measure | Observed |
|---|---:|
| Real generations / malformed responses | 140 / 0 |
| Accepted model publications | 135 |
| Rejected and published fallback | 5 / 140 = 3.57% |
| Exact normalized duplicate excess | 1 / 140 = 0.71% |
| Mean raw length | 37.73 characters / 6.9 words |
| Model latency median / p90 / p95 / max | 319 / 442 / 481 / 814 ms |
| Mean / maximum prompt characters | 3,208 / 3,489 |
| Fixed assistant-phrase proxy | 0 / 140 |
| Narrow manual assistant-tone annotation | 2 / 140 = 1.43% |

The most repeated first word is `да` (10/140); the most repeated two-word opener is `у меня` (4/140). Four lexical-overlap pairs were inspected; the concrete personality collision is ByteCat and kritik228 both writing `нафиг спасибо` (rows 73/76). Rejections were two viewer-length violations, one profanity violation, one unsupported history and one unsupported promise claim. The false-positive risk of lexical rules remains: natural paraphrases are not semantically proven by the validator.

**The living-community quality gate does not pass.** Manual reading flags 27 concrete coherence/grounding/speaker-role failures in this conditional audition; 26 passed validation. Accepted examples include `ну наконец-то заговорил` during continued silence (51), inventing five minutes before the stream ends (90), and unsolicited RTX 4090 details (140). Row 130's invented purchase last week was rejected. Rows 118/120 show advice-desk language despite the phrase proxy being zero. [Complete semantic review and issue list](evidence/viewer-core/semantic-review.md).

The actual cold/warm relationship prompts are different, but generated behavior is unreliable: a wary PixelFox says she is in love (82), while the warm response is less engaged (92). Witness contexts correctly contain the reported final failure and absent contexts do not, yet the ten witness-audition messages do not demonstrate a convincing specific callback. Technical knowledge isolation and callback limits therefore must not be presented as socially convincing memory. Forced hardware/food themes, incoherent remarks and role confusion remain concrete model-quality defects.

The fixed blind sample uses zero-based start 3 and stride 17 modulo 140, chosen before seeing the outputs. A separate assistant saw only those 20 lines and authored profiles and froze its guesses before the key was opened. Root comparison: **13/20 correct**, with 7/7 high-confidence, 3/6 medium and 3/7 low. This is an assistant review with prior Stage C familiarity, not a human panel or calibrated study. Language/hardware/caretaker traits are recognizable; sarcastic and gentle Russian voices often overlap, and obvious topic cues can make bad dialogue easier to identify. [Blind worksheet](evidence/viewer-core/blind-review.md), [frozen guesses](evidence/viewer-core/blind-guesses.md), [answer key](evidence/viewer-core/blind-answer-key.md).

No 9.5/10, production packaging or completed game-quality milestone is claimed. The remaining work is stronger semantic grounding, dependable relationship expression, useful sparse callbacks and the unexecuted human conversation acceptance.

## Real Unity persistence, outage and cross-stream acceptance

The Explicit scene fixture enters Play Mode in the actual `GL` scene and operates the PC/Streamly controls. Speech is supplied as `RecognizedSpeech` through the ordinary feed; OS capture is deliberately disabled for this automated scenario. It is therefore **Session B/C and a controlled cross-stream scenario, not physical-microphone Session A**. Named visits are held inside real audience seats, and the later high-audience fixture clones discovery tuning; it never writes the authoritative viewer total. Natural attendance variability is covered separately by deterministic tests.

In the first broadcast NightOwl witnesses thanks and a reported lost final; PixelFox is absent. The real save controller captures state after an accepted donation and restores the same JSON snapshot twice. Relationship, memory identity, donation total/history and wallet balance survive unchanged, with no receipt re-emission, duplicate memory or retained in-flight chat. After 121 game minutes and a new stream, actual correlated generation requests give NightOwl the old reported-failure memory and give PixelFox no MEMORY context. Both generate through the ordinary director. NightOwl's published `ну ты и уточнил` does **not** demonstrate a convincing historical callback; the enforceable knowledge-isolation test passes while social recall remains a quality limitation.

With the existing Reaction Monitor model switch disabled, the stream stays Live. The captured interval ends at 37 viewers, produces 11 fallback publications, 8 follows, 1 subscription and $7 of accepted donations. Wallet delta exactly equals accepted donation delta. Direct thanks still changes the intended relationship and a newly reported boss victory still creates a witnessed memory. Re-enabling the adapter produces a publication correlated to a fresh question, without replaying rewards. [Complete scene trace, raw model requests/results and save snapshots](evidence/viewer-core/scene-acceptance/living-community-play.jsonl), [passing execution](evidence/viewer-core/scene-acceptance-3.xml).

The first two fixture executions failed and are retained: the first incorrectly expected both names in one phrase to pass the shared chat budget; the second fired five English questions immediately before another request and exhausted that budget. The fixture now asks names separately, establishes language using filler and spaces questions without bypassing selection. Production rhythm was not loosened, no test was ignored, and model outputs were not retried to obtain better wording. [Attempt 1 result](evidence/viewer-core/scene-acceptance-1.xml) and [all raw evidence](evidence/viewer-core/scene-acceptance-1-raw.jsonl); [attempt 2 result](evidence/viewer-core/scene-acceptance-2.xml) and [all raw evidence](evidence/viewer-core/scene-acceptance-2-raw.jsonl).

## Stream Chat visual review

All six real Game-view captures are **1920×1080**, with frame/timestamp provenance in the [capture manifest](evidence/viewer-core/scene-acceptance/capture-manifest.txt). Each was visually inspected; the fixture also asserts that active chat/application TMP text is not truncated. No concrete clipping, oversized messages, username overlap, unreadable amount or HUD collision was observed in these samples. Longer Russian lines wrap naturally; the English ArcadeKid line fits. Streamly was not redesigned.

| Situation | Russian UI | English UI |
|---|---|---|
| Two viewers, normal silence | [RU low](evidence/viewer-core/scene-acceptance/viewer-community-ru-low.png) | [EN low](evidence/viewer-core/scene-acceptance/viewer-community-en-low.png) |
| 34 viewers, accepted donation line and alert | [RU donation](evidence/viewer-core/scene-acceptance/viewer-community-ru-donation.png) | [EN donation](evidence/viewer-core/scene-acceptance/viewer-community-en-donation.png) |
| Denser chat, 41/39 viewers | [RU high](evidence/viewer-core/scene-acceptance/viewer-community-ru-high.png) | [EN high with English model line](evidence/viewer-core/scene-acceptance/viewer-community-en-high.png) |

UI language does not rewrite already published chat. Donation prose may appear later than its immediate alert because publication observes ordinary typing delay and rate limits; the amount remains attached to the original accepted receipt. These captures establish readability, not dialogue quality or a long natural play session.

## Representative LIVE performance

The corrected measurement repeats the complete scene scenario and passes **25/25** checks (22 G guards, two native-allocation calibration checks and the real-scene acceptance), zero failures, 64.7632979 seconds. [Execution result](evidence/viewer-core/scene-acceptance-4.xml), [complete repeated scene trace](evidence/viewer-core/performance-verification/living-community-play.jsonl).

The measurement window is 30 real seconds / **12,323 ordinary LIVE frames** on the workstation above, with a recognized question every 7.5 real seconds. The ordinary runtime drives time; there is no extra viewer/simulation tick during measurement. Audit disk callbacks and the reflecting HTTP recorder are disabled for the window, and the original model adapter is restored. Timing includes ordinary editor/development diagnostic construction; native allocation scope start/stop sits outside the Viewer Tick timing marker.

| Measure | Observed |
|---|---:|
| Viewer Tick median / p95 / maximum | 0.0082 / 0.0107 / 0.5317 ms |
| Viewer Tick allocation count mean / p95 / maximum | 1.7 / 1 / 824 allocations |
| Entire Editor frame allocated bytes mean / maximum | 9,015.9 / 533,244 bytes |
| Entire Editor frame median / p95 / maximum | 2.338 / 2.968 / 96.809 ms |
| Scenario maximum queue / configured capacity | 5 / 6 |
| Scenario stale requests discarded | 13 |
| Permanent profiles / retained memories | 10 / 5 |
| Maximum recorded scene prompt | 3,715 characters |

Queue/stale counts are cumulative over the repeated two-stream fixture, including deliberately accelerated waiting and cancellation boundaries; they are not thirty-second natural-play rates. The model still has one shared inference worker, never one per viewer or frame. The scene recorder contains 15 completed requests outside the uninstrumented profiling window: median/p90/p95 **668/872/945 ms**. The director's bounded 17-sample history, including that window, reports **684/851/872 ms**. The separate fixed 140-generation audit provides the larger latency distribution above. No Viewer Tick exceeded 0.532 ms in this window; the 96.809 ms whole-editor frame spike is real and was not causally attributed. This does not prove freeze-free release performance or minimum-spec suitability.

The first measurement's all-zero scoped byte values are **invalid evidence**, not an optimization success. A known 65,536-byte allocation returned zero through `GC.GetAllocatedBytesForCurrentThread` while the native profiler saw one allocation. The installed Unity Mono/Boehm implementation returns zero for that managed API. The corrected measurement uses the native marker's sample **Count**; its Value has TimeNanoseconds units and is not bytes. Calibration also verifies two allocations inside a reset scope and zero in a subsequent empty scope. [Original diagnostic](evidence/viewer-core/allocation-diagnostic.xml). The existing Unity counter API supports custom profiling values; count/byte units remain explicitly separate. [Unity counter documentation](https://docs.unity.com/en-us/engine/6000.7/manual/analysis/profiler/customizing/adding-information-code/add-counters-code).

Source inspection identifies allocation candidates consistent with the low idle baseline and request-time bursts: the instance method-group delegate passed to `Director.Update`, reaction intent lists, request/context StringBuilders, bounded witnessed-chat/memory copies and LINQ retrieval, cancellation/task/HTTP objects, and development trace strings. This is a code-level attribution, not a captured allocation-call-stack percentage ranking. Worker-thread model buffers and StreamSession allocations are outside the scoped count. The first passing scene's complete serialized community snapshot is 3,009 UTF-8 bytes with five memories; it is not a managed-heap-size measurement. Banks remain capped at 16 per permanent viewer, with ten authored plus at most eight promoted identities; retrieval gives at most two facts and one known promise per request.

The repeated scene also re-verifies persistence, isolated knowledge and recovery with the corrected instrumentation. Its outage interval ends at 36 viewers with eight fallback publications, $9 accepted donation/wallet delta, eight follows and one subscription. All repeated raw responses and captures are retained; earlier scene results and the fixed 140-cell audit are not replaced.

## Final complete-suite verification

After the native allocation-counter repair, Unity 6000.6.0f1 ran the **complete non-Explicit project suite: 1,035 passed, 0 failed**, 133.9535878 seconds. The result enumerates 1,042 tests with seven Explicit cases excluded by the ordinary run. [Final full-suite XML](evidence/viewer-core/final-suite-results.xml). No tests were disabled, ignored or weakened to obtain this result. Existing unrelated PC, peripheral, desktop, save, economy and speech tests remain green.

The model/scene Explicit cases were run separately where stated: the before/after ten-profile comparisons, the fixed 140-generation audit, and the real scene persistence/outage/cross-stream/performance fixture. The other legacy model/recorded-speech Explicit utilities and real-microphone test are not claimed as executed by this final full-suite run. Human ten-minute Session A remains unexecuted. The preceding full run before the diagnostic correction also passed 1,033 tests; two calibration regressions account for the final increase.

Independent reviews of D/E/F and the final technical integration have no outstanding actionable findings after the recorded fixes. This technical verdict does not override the explicit semantic/social quality failure above. Initial unrelated working-tree edits were restored byte-for-byte from the pre-task backup and excluded from the stage commits.

## SOCIAL QUALITY PASS

A correction pass, not a new feature stage: no new Viewer Core systems, no architecture rewrite, no second model call. C# now decides **what** a selected viewer is socially doing and **which facts exist** before generation; the model decides only **how** that viewer says it.

### Architectural changes

- `ViewerUtterancePlan` / `ViewerUtterancePlanner` (plain C#, [ViewerUtterancePlan.cs](../Assets/Game/Scripts/Viewers/ViewerUtterancePlan.cs)): one plan per approved reaction, built from the intent and its situation at generation start and cached on that situation, so the prompt and the validator see the same envelope. All chance is a hash of the reaction id; the selector's random stream is untouched.
- `ChatContextBuilder` renders the plan instead of the free "MOMENT" prose: `VIEWER IS/WHO`, `RELATIONSHIP`, `FACTS` (with `STREAMER SAID`, `OTHER VIEWER SAID (name, not you, not the streamer)`, `MEMORY`, `PROMISE`), `NOT KNOWN`, `TOPIC`, `SOCIAL ACTION`, `TARGET`. The channel name no longer enters the prompt (the model had borrowed the fixture name "audit" as a game). The system text now leads with **DO NOT INVENT SPECIFIC GAME FACTS**.
- `ReactionSelector` takes an optional relationship lookup; `ViewerCore` passes `Community.Tier`. `ViewerCore.SituationFor` makes the callback decision and reserves only the chosen fact.
- `ChatOutputValidator` adds structured grounding checks against the plan (below). Existing lexical guards are unchanged apart from the two refinements listed under *Changed contracts*.
- `SocialHabits` on `ViewerProfile` (authored data) and an editor-only Reaction Monitor session recorder for Session A.

### Utterance plan

`ViewerId`, `Intent` (React, Tease, Question, Answer, Concern, Disagree, Acknowledge, Callback, ThankResponse, SilenceCheck, TechnicalComment), `Manner` (the action a callback is delivered as), `Target` (streamer / chat / another viewer), `Topic`, `AllowedFacts`, `RelevantMemoryId`, `RelevantPromiseId`, `RelationshipTone`, `CurrentEventId`, `Language`, `ReplyTarget`, `GameChoice`. Candidate actions come from what actually happened (a direct question, a thanks, a reported failure or win, filler, setup talk, silence, a device change, support, a reply); authored habits and the relationship reweight them. Teasing is opt-in (only viewers who prefer it) and technical remarks require a hardware/setup interest, which removed forced hardware themes from non-technical viewers.

### Allowed-fact model

Every generation receives a bounded envelope of true statements: stream duration and audience, content hint, the quoted speech or other-viewer line, who is addressed, and explicit negatives where the audit showed hallucinations — "Nobody knows when it will end", "Those were filler words; nothing notable happened", "The streamer … is still silent right now", "The streamer's microphone (theirs, not yours) was just unplugged, so their voice now sounds much worse", "No picture, sound, PC or settings problem has been reported; the hardware is unknown", "You know of no purchase, upgrade or hardware change" (only for viewers without promise knowledge), "They are thanking you by name; what for is not said", "The streamer is talking about themselves: it happened to them, not to you". A memory or promise appears only as a planned callback, with the only time phrase the game can back ("yesterday", "a few days ago").

The validator enforces the envelope where it can deterministically: a time/performance quantity (`5 минут`, `полчаса`, `60 fps`), a hardware model or long number (`4090`, `R9 380`) or a brand outside a hardware moment must appear in the plan's facts, quoted speech, visible chat or names (`10 минут` is fine when the stream is ten minutes old; `1v5`, `10/10` are not claims); `опять/снова` needs the streamer's words, visible chat or a planned callback (`попробуй снова` passes). Numbers are not banned globally.

### Relationship behaviour

`Sentiment` plus familiarity map to Wary (≤ −25), Neutral, Friendly (≥ 25), Loyal (≥ 60 with 5 visits or 3 acknowledgements). Before the prompt it changes: direct-address eligibility (Wary may let a pleasantry pass: 0.5 vs 0.92; a real question still gets 0.8; Friendly 0.95, Loyal 0.97), pick weight (0.75 / 1 / 1.1 / 1.25), delay (Wary ×1.15, Loyal direct ×0.8), action weights (Wary never reassures, more Disagree/sharper Tease; Friendly/Loyal answer and ask back more, Loyal shows concern), callback willingness (×0.5 / 1 / 1.25 / 1.6, cap 0.75) and who answers a skeptic's published line (friends push back ×2 selection, ×4 Disagree). A small intimacy policy rejects romance in every state, devotion (`любимый стример`, `скучал`, `missed you`) below Loyal and hearts below Friendly.

### Callback planning

Relevance only creates a candidate (same canonical subject; unrelated moments retrieve nothing). The planner then rolls the viewer's authored `CallbackInterest` × relationship scale, deterministically per reaction; a published callback blocks another for that viewer for 600 stream seconds, on top of the existing per-fact 120-minute cooldown and three-reference cap. Without a planned callback no historical fact reaches the prompt and a historical claim is rejected.

### Profile distinctness

Only the voices that collided in the blind review received `SocialHabits` (the personality text is unchanged): NightOwl prefers Tease/SilenceCheck and keeps score (callback interest 0.55); kritik228 prefers Disagree (unimpressed, not teasing) and avoids concern/questions (0.15); ByteCat prefers TechnicalComment and ignores apartment/life talk; Sovetnik prefers Answer (advice) and avoids questions; ZinaIvanovna prefers Concern/Question (0.45); mika_draws prefers Acknowledge, avoids questions/pushback and ignores hardware/money; PixelFox prefers Question. ArcadeKid, Jonas and doshirak keep defaults. The asset change is exactly ten `Habits` blocks regenerated by the authoring menu.

### Changed contracts (explicit)

- Stage E: a heard (HeardStreamer) memory may now be recalled without an attribution word (`помню твой финал`, `а ты вчера как проиграл-то`); claiming to have *seen* it (`видел…`) is rejected. The old rule rejected 6 of 9 correct planned callbacks in run 1. One assertion in `ViewerMemoryTests` was updated accordingly and a seen-claim rejection added.
- A planned callback about the moment's own subject may omit the subject word (memory and promise references); a different named subject still fails and the known outcome still cannot flip.
- `ChatDirectorTests.ValidModelOutputBecomesTheViewersChatLine`: the fake model line changed from `ахах опять` to `ахах тут я` because an unsupported "again" is now rejected on the runtime path; the test's purpose is unchanged.
- Explicit scene acceptance now asserts the witness has the memory as a **callback candidate** in the trace (and the absent viewer none), because a MEMORY prompt is a bounded C# roll; whenever MEMORY is present it is the correct fact.

### Before / after audit metrics

The same fixed 14-context × 10-profile matrix, one real generation per cell, no retries, through the production planner and callback decision. Four complete runs were executed as fixes were made; all are retained. Run 4 is the committed code. Defects are counted with a stricter rubric than the original list of 27, so the original 140 rows were re-annotated with it ([semantic review](evidence/viewer-core/social-quality/semantic-review.md)).

| Measure | Before | Run 1 | Run 2 | Run 3 | **Run 4** |
|---|---:|---:|---:|---:|---:|
| Invented facts/details (a) | 21 | 11 | 7 | 4 | **1** |
| Unsupported history/recurrence (b) | 5 | 3 | 3 | 5 | **5** |
| Speaker-role confusion (c) | 3 | 1 | 1 | 0 | **0** |
| Relationship-incompatible (d) | 3 | 1 | 0 | 0 | **0** |
| Grounding/role/relationship total | 32 | 16 | 11 | 9 | **6** |
| Incoherent / non sequitur (e) | 9 | 7 | 5 | 9 | **9** |
| All accepted defects | 41 | 23 | 16 | 18 | **15** |
| Rejected → fallback | 5 (3.6%) | 9 (6.4%) | 7 (5.0%) | 3 (2.1%) | **8 (5.7%)** |
| Exact duplicates | 1 | 0 | 0 | 0 | **0** |
| Assistant-phrase proxy | 0 | 0 | 1 | 0 | **0** |
| Mean length (chars / words) | 37.7 / 6.9 | 42.7 / 7.7 | 41.5 / 7.3 | 39.6 / 7.2 | **40.9 / 7.2** |
| Latency median / p90 / p95 / max (ms) | 319/442/481/814 | 371/500/555/1068 | 355/459/485/935 | 334/446/488/767 | **338/455/489/703** |
| Prompt chars mean / max | 3,208 / 3,489 | 3,550 / 4,040 | 3,553 / 3,957 | 3,552 / 3,957 | **3,552 / 3,957** |

Final run, especially unacceptable categories: invented purchases/hardware **0 accepted** (1 caught: `AMD R9 380`), invented elapsed/remaining time **0** (1 caught: `полчаса`), impossible relationship claims **0**, claims of having seen unseen history **0**, wrong speaker identity **0**. The earlier concrete defects did not recur: no RTX 4090, no "five minutes left", no "finally spoke" during silence, no "my mic died", no wary viewer in love. Runs 2–4 are within noise of each other on the total, so no further improvement is claimed from run 3 to run 4 beyond the removed recurrence and time claims.

Raw evidence: final run [complete per-cell records](evidence/viewer-core/social-quality/run4/viewer-social-quality-raw.jsonl) (prompts, request bodies, raw HTTP responses, validation, publication, plan) and [readable table](evidence/viewer-core/social-quality/run4/all-raw.md); runs [1](evidence/viewer-core/social-quality/run1/all-raw.md), [2](evidence/viewer-core/social-quality/run2/all-raw.md), [3](evidence/viewer-core/social-quality/run3/all-raw.md) as complete readable tables and metrics (their full JSONL is retained at `E:/GO-Live-ViewerCore-social-20260926/audit-v2*`).

### Relationship A/B

Same viewer and event, Wary/Neutral/Friendly/Loyal, deterministic selection over 400 seeds plus 5 real generations per state for PixelFox and NightOwl on a direct question and a failure report (80 generations per run). Wary → Loyal: direct reaction rate 0.903 → 0.988 (PixelFox) and 0.883 → 0.985 (NightOwl); mean delay 4.64 → 3.16 s and 3.70 → 2.53 s; PixelFox asking back 29% → 43%; concern on a failure 0 (Wary) vs 21–24% (Neutral–Loyal); NightOwl's teasing share 51% (Wary) → 20% (Loyal) as answers rise. Text follows: wary lines are cool or curt, friendly ones ask back, loyal ones are warm; NightOwl's teasing goes from `да ну что ты за геймер` to `зато покупать научишься`. Friendly and Loyal wording overlaps; invented history still appears in A/B lines. [Selection data](evidence/viewer-core/social-quality/run4/relationship-ab-selection.json), [raw generations](evidence/viewer-core/social-quality/run4/relationship-ab-raw.jsonl), [readable](evidence/viewer-core/social-quality/run4/relationship-ab.md).

### Memory A/B

A (witnessed, relevant), B (never witnessed), C (witnessed, unrelated), NightOwl and ZinaIvanovna, 8 paired ids each. Candidates 16/0/0; planned callbacks 9/0/0 (bounded). Run 1: 6 of the 9 planned callbacks were rejected by the old attribution rule. Run 4: **9/9 published from the model, 8 specific and relevant** (`вчера же проиграли`, `Вчера было так жалко, а сегодня новый шанс. Удачи!`). B never names the loss but NightOwl invents counts from the streamer's "снова"; C never mentions the final. [Raw](evidence/viewer-core/social-quality/run4/memory-ab-raw.jsonl), [readable](evidence/viewer-core/social-quality/run4/memory-ab.md).

### Verification

- Complete non-Explicit Unity suite: **1,069 passed, 0 failed** (1,035 before + 34 new), 9 Explicit skipped, 133.5 s ([final suite XML](evidence/viewer-core/social-quality/final-suite-results.xml)). New regressions: [ViewerUtterancePlanTests](../Assets/Game/Tests/Editor/ViewerUtterancePlanTests.cs) — plan construction/caching, action by moment and habit, relationship-driven action/eligibility/delay, defending against a skeptic, intimacy ceiling, ungrounded numbers/hardware, grounded numbers from speech/chat/names, speaker-role and setup facts, sparse relationship-scaled callbacks, runtime candidate isolation, authored silence, distinct habits, heard-not-seen recall, implicit-subject promise/memory references, elapsed time vs history, recurrence support.
- The preceding full run on the same code minus the recurrence check had two failures in `DesktopFlowPlayModeTests` (a 72-px Game-view capture and an aggregate audience chat counter of 0); both passed on an isolated re-run and do not touch Viewer Core code (evidence retained at `E:/GO-Live-ViewerCore-social-20260926/full-suite-3.xml`, `desktopflow-rerun.xml`).
- Explicit real-scene acceptance (persistence, outage, cross-stream isolation, performance) on the final code: **passed** ([result](evidence/viewer-core/social-quality/scene-acceptance.xml), [trace](evidence/viewer-core/social-quality/scene-acceptance-play.jsonl)). An earlier execution in a batch with other Explicit tests timed out at the model-recovery wait (its trace was deleted with `Temp/`); that wait needs one validated model line, and stricter validation makes a fallback there slightly more likely.
- Performance on the same workstation: Viewer Tick p50/p95/max **0.0081 / 0.0100 / 0.435 ms** (before 0.0082 / 0.0107 / 0.532), allocation count mean 1.7 (unchanged), queue max 5/6. Planning runs once per generation, not per frame; there is no verifier model, embedding or per-viewer inference. Prompts grew by about 340 characters; median model latency moved 319 → 338 ms in the fixed audit.

### Remaining failures

- About 9 incoherent or non-sequitur lines per 140 persist (`установить камеры на баланс`). This is the current 8B Q4 model at temperature 0.85; grounding does not fix it.
- Softer unsupported history presumptions without `опять/снова` remain (`не в первый раз`, `тысяча раз`, `третий подряд`, `когда вы играете в стратегии`): 5 per 140. Counting words and habits are not enforced deterministically.
- Persona-driven advice can still assert a problem the facts deny (`это не стрим, а всего лишь тест`); invented game titles dropped but can recur (`«Рогейн»` in run 2).
- Grammatical gender of viewer and streamer is inconsistent; Friendly vs Loyal wording overlaps; lexical guards have known false positives (a quoted `жесть`, `прошлых турниров`).
- Session A (human, real microphone, ten minutes) is **not yet performed**; it is required before Viewer Core can be called complete. The Reaction Monitor has a new **Record session** toggle that writes every decision, chat line and Game-view hitch to `Logs/ViewerSessions/session-*.jsonl` for that session.

No 9.5/10 is claimed.

## Remaining acceptance and product limits

- The social quality pass above fixes most grounding, role and relationship defects in the fixed audit; incoherent lines and softer history presumptions remain, and profile distinctions are partly recognizable. Lexical guards do not prove arbitrary natural-language claims.
- Session A requires a person speaking for ten minutes and remains explicitly unexecuted; its exact path is above. Scripted recognized speech does not validate microphone transcription quality.
- Performance evidence is from this workstation's Unity Editor, not a release build or minimum-spec device. Development diagnostic strings allocate; final embedded inference packaging is not implemented.
- Presence and in-flight requests are transient by design. Promises use a small conservative vocabulary; absent original listeners do not automatically learn the outcome. Lifetime first-follow/first-subscription flags are not a renewal/billing system.
- No quality score of 9.5/10 and no completed game-quality milestone is claimed. A–C are preserved and nothing is merged to main.

## Commit sequence

| Work | Commit |
|---|---|
| Preserved A — deterministic reactions | `4a4eb06` |
| Preserved B — local chat director | `5af07d1` |
| Preserved C — permanent profiles | `39710e5` |
| Handoff validation and narrow guards | `86145b5` |
| D — presence and relationships | `d85158a` |
| E — witnessed memory | `b688a16` |
| F — promises and social callbacks | `5ecf9fb` |
| G — living community integration | `6356107` |
| Social quality pass — grounded utterance planning, relationship behaviour, callbacks, habits, regressions | `a55c10a` |
| Social quality pass — audit V2 harness, raw evidence and this report section | the commit adding `docs/evidence/viewer-core/social-quality` |

The branch remains `claude/sharp-wozniak-iakuny`; no squash or merge to main.
