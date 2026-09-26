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
