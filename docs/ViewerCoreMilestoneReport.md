# Viewer Core milestone — continuation report

Status: **in progress**, continuing `claude/sharp-wozniak-iakuny`. A–C commits are preserved: A `4a4eb06`, B `5af07d1`, C `39710e5`. Nothing merged to main. This report does not yet claim the living-community quality gate is met.

## Handoff audit of actual A–C code

`VoiceInputBehaviour` captures real audio. Plain `VoiceRecognition` coordinates VAD and the recognition worker; results are drained on the game thread. `StreamSpeechFeed` accepts nonduplicate phrases only while Live and rejects speech ending before this broadcast. It is independent of the fictional microphone item.

`StreamEventSource` normalizes speech, accepted donation receipts, aggregate counter deltas, silence/away periods and peripheral changes. `SpeechRelevance` assigns cues/topics/relevance and matches known name forms. `ReactionSelector` chooses a watching participant and delay with deterministic randomness and audience-scaled chat rhythm. `ChatDirector` runs bounded asynchronous generation through `IViewerLanguageModel`, validates text and publishes to `StreamChat`; the existing stream overlay reads that chat.

`AudienceSimulation` is authoritative for total viewers and aggregate outcomes. `StreamSession` accepts donation receipts; `DonationPayout` credits the wallet. `DesktopState` applies completion to Trich/Outline. None of the text-generation pipeline creates money, followers or subscriptions.

Stage C supplies ten authored permanent profiles, structured style, interests, schedules, affinities and initial sentiment. The community asset is configured but runtime attendance is not yet connected; this is the intended D seam. `AudienceRoster` currently exposes join/leave, but shrink reconciliation must be added before runtime named viewers are enabled.

The current backend is `OpenAiCompatibleChatModel`, with a local-only endpoint, request timeout, cancellation, bounded output and JSON text contract. `ChatDirector` polls task results on the game thread, backs off after transport failures and uses fallback or silence. Broadcast end/load cancels and removes pending publication paths. A generation must also retain its viewer's presence epoch once D allows leave/return.

Two concrete pre-existing issues were reproduced with failing regressions: nullable speech-topic checks allowed unsupported support claims on non-speech moments, and two generated lines could both pass duplicate validation before their delayed publication. Both receive narrow fixes; A–C are not rewritten.

## Baseline verification after C

Unity 6000.6.0f1 complete non-Explicit suite: **900 passed, 0 failed**, 134.14 seconds. Four Explicit cases require a person/microphone, recorded voice or a local model and were excluded by the normal suite. Existing reaction/director/profile target: **79/79 passed**. The handoff's quoted 125 targeted total is not used as evidence; these are the actual results in this checkout.

Evidence: [complete baseline](<E:/GO-Live-ViewerCore-audit-20260926/stage-c-full-suite.xml>), [baseline targeted](<E:/GO-Live-ViewerCore-audit-20260926/stage-c-targeted.xml>), [two failing handoff regressions](<E:/GO-Live-ViewerCore-audit-20260926/handoff-red.xml>).

## Current development model

Local server inventory confirms `mistralai/ministral-3-8b-instruct-2512`, GGUF **Q4_K_M**, loaded context **4096**. Current settings: temperature 0.85, top-p 0.95, maximum 64 output tokens, 6-second timeout, one concurrent request and queue capacity six. LM Studio is a development runtime, not shippable game packaging.

Implementation and acceptance are tracked in [the continuation plan](superpowers/plans/2026-09-26-viewer-core-continuation.md). Raw model comparisons, stages D–G, performance, blind review, acceptance and final commit evidence will be appended as performed.

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

Verification: eight initial RED cases; the subsequent integration run exposed a real fuzzy-name social mutation and missing schedule validation (fixed), plus two incorrect expectations in new tests (actual email domain and lifetime visits versus current seats). Independent review caught thanks to an absent known viewer being misattributed to the last donor. A failing regression proved it; cached known-name matching now prevents that inference, with an independent conservative check in the community. Re-review approved. Final targeted **194/194 passed**, including viewer/director/speech/audience and desktop persistence/completion checks. Evidence: [D final results](<E:/GO-Live-ViewerCore-audit-20260926/stage-d-final.xml>). Event-time knowledge isolation follows in E; D does not claim to implement memory.

## Embedded Local Inference Migration

LM Studio plus the loaded Ministral 3 8B Q4_K_M model is the **development backend**. Shipping cannot require players to install it. `IViewerLanguageModel` is the replacement boundary: request text, token budget, cancellation and a typed result. AudienceSimulation, StreamSession, normalized facts, deterministic selection, community state, save data, validation and the visible chat remain backend-independent. The LM Studio CLI menu is editor-only tooling.

Two candidate packaging paths are a private child process with a loopback protocol, or a native inference library behind a worker-thread adapter. llama.cpp's server supports CPU/GPU inference, OpenAI-compatible chat completions and schema-constrained JSON; that makes the first path plausible without changing the domains. This is an integration candidate, not a validated product choice. [Primary server documentation](https://github.com/ggml-org/llama.cpp/blob/master/tools/server/README.md).

Production work would add lifecycle ownership (start/readiness/exit/cancellation/crash recovery), signed platform binaries, model acquisition/version/hash checks, tokenizer and chat-template compatibility, a restricted local endpoint or native ABI, driver/backend selection, and memory/thread budgets shared with Unity and speech recognition. It must be benchmarked under gameplay load, including CPU fallback and hardware below this workstation. Packaging also needs a review of runtime/model redistribution terms and notices. The publisher provides a GGUF model and Q4_K_M usage examples, but that alone does not validate the game's build/distribution path. [Publisher model card](https://huggingface.co/mistralai/Ministral-3-8B-Instruct-2512-GGUF).

Eight billion weights at four bits have a theoretical floor of roughly 4 GB before quantization metadata, non-four-bit tensors, KV cache and work buffers. Actual package/RAM/VRAM requirements must be measured for the exact shipped file, context and backend; do not advertise that floor as a minimum GPU requirement. Context and generation budgets remain bounded; one shared inference worker is sufficient for this chat design. Smaller compatible models can be auditioned later through the same adapter contract. No embedded runtime is implemented in this milestone.

## Human microphone acceptance — prepared, UNEXECUTED

Session A needs a person speaking for 10+ minutes; an autonomous replay does not stand in for that acceptance. Existing real-device Explicit test checks RU/EN admission and post-stop isolation, but is shorter and does not establish the ten-minute conversational experience.

1. Open `Assets/Game/Scenes/GL.unity` in Unity, select a 1920×1080 Game view, start the configured local model with `GO! LIVE → Viewer Core → Start Local Model (LM Studio)`, then enter Play Mode.
2. Boot the starter PC, sit at the monitor, create Outline/Trich accounts, install/open Streamly and connect the channel code. Choose a supported quality and go Live. Enable real voice input in the existing voice controls. The fictional desk microphone is optional equipment, unrelated to OS capture.
3. Open `GO! LIVE → Viewer Core → Reaction Monitor`. Confirm actual recognized phrases appear as speech events, then as selection/silence and ordinary model/fallback output. Start a real ten-minute timer; retain a screen recording and monitor trace.
4. Minutes 0–2: greet chat, then use filler such as `так, секунду, ага` with natural pauses. Minutes 2–4: ask which game to play and address a **currently present** viewer by name. Check that only the intended viewer receives acknowledgement.
5. Minutes 4–6: describe a mistake and a success without prescribing the response. Leave at least 75 seconds of natural silence. Minutes 6–8: remove/reconnect the in-game microphone while the real OS microphone remains available; broadcast and recognition must continue independently.
6. Minutes 8–10+: speak a few English phrases, ask a final question, then stop the broadcast and speak once more. New speech must not enter the stopped stream. Check for filler suppression, plausible questions, quiet periods, no repetitive bot chatter and no inference-related frame freezes. Capture RU/EN chat readability and log any incorrect name, memory or invented-fact claims.
7. Optionally run the existing Explicit `RealMicrophoneRussianAndEnglishReachTheStreamOnlyWhileLive` in Test Runner to retain a separate device-path regression result. It does not replace the conversational ten-minute session.

Acceptance workstation for the autonomous audit: Ryzen 7 7800X3D (8 cores / 16 threads), RTX 5070 with 12,227 MiB reported VRAM, driver 616.56. Latency on this machine is not a minimum-spec performance guarantee.
