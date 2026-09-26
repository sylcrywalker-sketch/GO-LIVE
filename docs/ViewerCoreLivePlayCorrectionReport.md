# Viewer Core live-play correction — implementation and acceptance evidence

Historical report for `c8c5c3a`. The subsequent punctuation-independent continuation correction and its new fixed attempts are documented separately in [Low-audience conversation correction](ViewerLowAudienceConversationReport.md); the failed evidence below is preserved.

This continues the existing working tree at `a030b9f` on `claude/sharp-wozniak-iakuny`. Claude's corpus recorder, decoding controls, speech acts, conversation path and authored daily life are preserved. No main merge. This report does **not** declare Viewer Core complete.

## Recorded speech and selection gate

Source: `Logs/VoiceCorpus/20260926-182411/raw.wav`, 95.17 seconds, 48 kHz mono PCM, 25 original speaker-marked phrases. SHA-256: `1eff759b65feb95d5d787cef1af011faa005d9567817ffe23bce124d0ca72a66`. The last two phrases are deliberate English/mixed controls; all RU configurations still force Russian. They are reported separately from the 23 Russian phrases when judging meaning.

All model inputs come from this same unchanged recording. This is recorded microphone evidence, **not a new live microphone acceptance**. Audio and benchmark weights remain local under gitignored `Logs/`. Transcripts below mean the returned production adapter text after its existing artifact/no-speech filtering, not unfiltered native token output.

Selection prioritizes conversational meaning, question/address preservation, conditional/promise content and invented words. CER/WER, lexical recall and exact speech-act flag agreement are supporting diagnostics, not semantic verdicts. One speaker/take cannot establish general recognition accuracy.

Official multilingual model weights came from [whisper.cpp's model repository](https://huggingface.co/ggerganov/whisper.cpp/tree/main); their complete downloaded sizes and SHA-256 hashes were verified against its LFS metadata. The native Windows package uses whisper.cpp 1.7.5 with Vulkan; tiny/base support the same GPU path as small/turbo. The test machine is Ryzen 7 7800X3D / RTX 5070 (12 GiB).

The first seven-configuration run is retained as preliminary evidence. Its inherited one-second silence warm-up was rejected as too short by the backend; its managed Mono private-memory readings were also unsupported (all zero). The corrected comparison warms with the same real first phrase, records cold text/latency separately, then scores every phrase. RAM now uses Windows `GetProcessMemoryInfo.PrivateUsage`. VRAM is the before/after total device reading from `nvidia-smi`; both are observed deltas, not peaks or isolated model ownership. Native logs confirm Vulkan execution. Measurement order, driver caches, managed allocations and other applications can affect deltas.

The production VAD produces 26 segments for 25 marks, consistently across models. The promise phrase is split into the purchase statement and a separately recognized “Обещаю.” after a pause. No segment hits the maximum length. Segmentation is assessed independently from manually marked phrase accuracy.

## Verified progress

- Initial current-tree Unity compilation succeeded before edits. A byte-exact backup of the original modified/untracked files and diff is retained at `E:/GO-Live-LivePlay-audit-20260926-continue`.
- Voice regression RED: 25 cases, 14 failures reproduced (WAV bounds/pre-write validation and diagnostic callback disposal). GREEN: those cases plus the existing voice pipeline, 54 passed / 0 failed.
- Corrected comparison: ten configurations, 250 marked-phrase results and 182 VAD results, no missing configurations or backend failures.
- Controlled decoder-tail experiment: three configurations, all 75 marked phrases and 78 VAD segments. Appending 500 ms of zero samples after preparation improved some turbo endings without waiting for more microphone audio. This is explicitly experimental evidence until a production change is verified.

## Selected shipping configuration

**`ggml-large-v3-turbo-q5_0.bin`, GPU, beam search, forced RU baseline.** This is the smallest semantically adequate candidate **among those tested**, not a claim about every available model. The 574,041,195-byte (547.4 MiB) verified file is the only model in `Assets/StreamingAssets/Whisper`; alternatives stay in `Logs/SpeechModels`. Weights are gitignored. A fresh checkout restores the shipping model through **GO! LIVE → Voice → Install selected turbo speech model (548 MiB)**, which verifies SHA-256 before installation. Include the installed model when building the player.

| Candidate / fair GPU decoding | Main semantic findings | Decision |
|---|---|---|
| tiny, greedy/beam | Greeting address becomes “партнем”; inserts negation into the games question; distorts the request to answer and the named question. | Reject |
| base, greedy/beam | Some requests improve, but the purchase promise becomes “Затрку пленового видеокарту”; the Mika personal question loses its address/structure. | Reject |
| small-q5_1, greedy/beam | Good opening and most personal questions, but consistently substitutes “отвезти” for “ответить” and corrupts “завтра”. | Reject |
| turbo-q5_0, GPU beam, contextual preparation | Preserves the opening, request to answer, personal questions, conditional and purchase promise in the Russian recording. The nickname “Найт Оул” remains “Night Wall”/“Найт Волл”. | Selected; nickname limitation retained |

Decoder context was isolated with controlled experiments. VAD capture boundaries stay unchanged: recognition preparation resamples to 16 kHz, keeps the existing 1.1-second backend minimum, then surrounds the phrase with 8,000 zero samples on each side. These zeros add no microphone wait. Leading context repaired the short games question; trailing context also kept “Нормально слышно?”. The same preparation was then tested on the smaller GPU contenders; their meaning-changing errors remained. No transcript substitutions or phrase-specific prompts were added.

The final benchmark used the **actual StreamingAssets model**, the production preparation and unsuffixed `turbo-q5_0-ru-gpu-beam` configuration: 25 marked phrases plus 26 VAD segments. Model load was 0.75 s, first warm-up recognition 0.28 s, mean marked-phrase recognition 0.17 s / p90 0.19 s. Observed process-private RAM delta was 1,944 MiB and device VRAM delta 1,017 MiB; these are not peak requirements. VAD closure still waits the configured 650 ms. Chat generation/presentation latency is measured separately in short acceptance.

Across the 23 Russian marked phrases, mean per-phrase WER is 3.48%, lexical content recall 96.96%, and the lexical invention heuristic flags one word (the nickname substitution). The full 25-phrase summary includes the deliberately forced-RU English controls and therefore has worse lexical scores. These are distinct populations, not omitted failed Russian phrases. Punctuation is not always retained, especially in the tiredness follow-up.

Both `VoiceInputBehaviour` defaults and `GL.unity` use the selected file, GPU and beam decoding. New/invalid voice-language settings force the UI language (RU or EN); a deliberately saved player choice, including Auto, remains available. Benchmarks and the RU acceptance explicitly use Russian. The OS microphone path remains independent of `PcPeripherals`.

Production-wiring RED reproduced 8 failures in 12 cases. The corrected focused run passed **67/67**: 66 deterministic voice/configuration cases and the explicit production corpus comparison. Independent review subsequently found abandoned diagnostic audio on disable and a callback-disable worker lifecycle issue. Three regression cases reproduced them; the corrected deterministic voice/configuration run passed **69/69** and re-review approved it.

Evidence: [every phrase and its semantic finding](evidence/viewer-live-play/stt-semantic-audit.md), [final raw transcripts/timings](evidence/viewer-live-play/stt-production-final/raw.jsonl), [final metrics and VAD boundaries](evidence/viewer-live-play/stt-production-final/report.md), [ten-configuration comparison](evidence/viewer-live-play/stt-warm-ten-configs/report.md), [fair padded smaller contenders](evidence/viewer-live-play/stt-padded-small-contenders/report.md), [verified model manifest](evidence/viewer-live-play/model-download-manifest.json), [Russian-only lexical diagnostics](evidence/viewer-live-play/russian-23-metrics.json).

## Daily context and short conversations

The existing daily-life selection and authored profiles remain. A permanent viewer keeps the same activity during a game day, including across broadcasts; anonymous viewers keep it within a broadcast. The state is derived, not added to saves. Core and utterance planning now share the relevance gate: personal questions and relevant short follow-ups receive the selected viewer's day; unrelated gameplay receives no day context.

Daily context and the viewer's previous generated personal lines do not authorize streamer hardware, quantities, purchases or history. Structured game facts, witnessed memories and promises retain their separate ownership. The existing validator remains a conservative lexical guard, not a semantic guarantee against every possible vague false sentence.

The maximum conversation length counts the initial published answer. Initial answers and continuations reserve a pending turn per viewer/visit, and publication, cancellation, queue rejection or expiry releases it. Every scheduling path preserves continuation identity, including fallback selection. The reservation is transient ordinary C# state and uses the existing director completion event. Presence epochs, cooldowns, ordinary chat rhythm and the configured conversation window remain in effect. Unrelated gameplay and filler do not extend a personal thread. Punctuation-free personal replies such as the actual recognized “Жесть. Устал, наверное.” can continue it.

The model warm-up now occupies the existing generation budget, runs once per broadcast, and is cancelled at end/load/restart. Cancelled warm-up and ordinary requests retain their slots until they actually finish; retired ordinary results cannot publish or update metrics, and a cancelled warm-up cannot overwrite the next broadcast's measurements.

Initial daily/conversation RED reproduced 15 failures in 31 cases. Expanded verification exposed three further regressions, then pending-turn and full-queue release tests reproduced two separate reservation defects. Independent review exposed four further issues: fallback continuation identity, pending openings, cancelled ordinary worker accounting, and the actual English “slept too much” daily contradiction. Eight failing cases reproduced them. After repair, expanded verification passed **370/370, zero failures**. Eight additional contraction controls passed without production changes, disproving one review concern. The scoped re-review approved all four fixes. [Results](evidence/viewer-live-play/viewer-review-green.xml), [corrected independent review](evidence/viewer-live-play/daily-independent-review.md), [implementation and RED/GREEN record](evidence/viewer-live-play/daily-implementation-report.md).

## Short scene replay and human acceptance

Three 75-second attempts paced unchanged real-microphone corpus marks 1, 4 and 8 at seconds 2, 25 and 39 through the GL scene's own recognition/feed/core pipeline and configured local chat adapter. The hardware capture bridge was disabled before broadcast start; these are **recorded replays, not fresh human microphone sessions**. Audience counts, viewer identity, daily states, response probabilities and model answers were not injected.

All attempts preserved the opening questions and classified them as PersonalQuestion; “Так, секунду.” caused no response. Each opening had one personal answer from the actual local model. The first exposed a daily-consistency defect and is retained as failed evidence. The later two supplied personal content without invented hard streamer facts. **None of the three attempts produced the requested same-viewer follow-up, so the explicit end-to-end acceptance remains failed.** No successful run was substituted for these results.

| Attempt | Selected viewer / day | Actual opening model output | Outcome |
|---|---|---|---|
| [1: raw evidence and assessment](evidence/viewer-live-play/short-replay-1/assessment.md) | `Ded_ru`, English; stressed workday | “slept too much” | Daily contradiction reproduced and repaired; follow-up silence |
| [2: raw evidence and assessment](evidence/viewer-live-play/short-replay-2/assessment.md) | `pixel_play`, Russian; bored uneventful day | “все на ура! сижу дома с чаем и гадаю что потупить” | Personal content, upbeat wording looser than assigned mood; follow-up silence |
| [3: raw evidence and assessment](evidence/viewer-live-play/short-replay-3/assessment.md) | `pixel_ru`, Russian; errands, tired, low energy | “устал после дождей по городу бегать” | Personal content; partner still present, same visit, one turn, age 32.66 s; follow-up silence |

Opening audio-end-to-visible-answer latency was about 2.9–3.8 s across these attempts. Recognition/polling for the three phrases was about 0.76–0.94 s; full request, returned text, day, plan, validation outcome and timing are retained in each attempt's raw JSONL. “Raw model output” here means the adapter-returned generated text; its HTTP JSON wrapper is not recorded. These few samples are not a latency percentile or a reliability estimate.

The late follow-up was recognized as “Жесть. Устал, наверное.” without a question mark. Its contextual continuation path retains the existing 0.6 chance for a statement (questions use a higher chance). To investigate the repeated silence, a separate diagnostic reproduced the exact phrase after an answer at second 10 and filler at 27 with an exhausted ordinary budget: **64 same-viewer continuations in 100 fixed seeds**. The GL asset actually loads a 40-second window, five-answer maximum and six-second gap. Both diagnostic tests passed; this supports the probabilistic rule but does not establish a successful live acceptance or prove the cause of each failed attempt. [Diagnostic results](evidence/viewer-live-play/viewer-runtime-diagnostic.xml).

The [60–90 second fresh microphone procedure](ViewerCoreShortMicrophoneAcceptance.md) is ready. It has **not been performed**, and no ten-minute session is requested. Viewer Core is not declared complete.

## Final verification

Complete Unity EditMode run with **no test filter**: **1,174 passed, zero failures**, 137.087 s. The other 11 discovered cases were all explicitly marked `Explicit`; no ordinary test was ignored or skipped. This includes the existing scene journeys, save/load, peripheral readiness, voice, viewer and new regression tests. The run produced 81 local capture artifacts under `E:/GO-Live-LivePlay-audit-20260926-continue/full-suite-captures`. [Full machine-readable results](evidence/viewer-live-play/full-nonexplicit.xml).

```powershell
& 'C:/Program Files/Unity/Hub/Editor/6000.6.0f1/Editor/Unity.exe' -batchmode -projectPath 'E:/GO! Live' -runTests -testPlatform EditMode -testResults 'E:/GO-Live-LivePlay-audit-20260926-continue/full-nonexplicit.xml' -logFile 'E:/GO-Live-LivePlay-audit-20260926-continue/full-nonexplicit.log'
```

Independent whole-change code review found no blocking source defects. Its one diagnostic edge case is listed below. The green non-Explicit suite and code review do **not** change the failed short-replay acceptance described above. [Whole-change review](evidence/viewer-live-play/final-independent-review.md).

The original unrelated changes to both generated `.csproj` files, TMP fallback font asset and VersionControlSettings are restored byte-exact from the initial backup and excluded from this correction. Feature work remains on `claude/sharp-wozniak-iakuny`; no main merge.

## Remaining limits

- This one speaker/corpus still misrecognizes the NightOwl nickname and sometimes drops interrogative punctuation.
- Daily consistency is a conservative lexical guard, with narrow English work/sleep/gaming patterns; it is not general semantic validation. Model tone/content and actual follow-up reliability still need human acceptance.
- The development whole-session WAV recorder assumes a stable device sample rate. Switching to another supported rate mid-recording can mix rates in `capture.wav`; per-phrase files retain their own rates. Independent review classified this diagnostic edge case as non-blocking.
- Unity emits existing obsolete-API/DEVELOPMENT_BUILD diagnostics and local licensing noise. Passing test results are reported separately from those warnings.
