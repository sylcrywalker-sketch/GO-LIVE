# Independent final test and scene evidence assessment

## Result

The final non-Explicit suite passed: **1,213 passed, zero failed, 11 Explicit cases skipped** in `full-final.xml`. Both review-added mixed-clause personal-question regressions passed.

The three predetermined scene attempts are **not an overall acceptance pass**. Strict XML results are **one pass and two failures**. Two eligible, present conversation partners both continued under the missing-punctuation condition, but attempt 3's generated continuation was semantically poor. These two observations are not a statistical reliability estimate.

| Attempt / RNG state | Broadcast seed | Strict XML | Observed continuation |
| --- | --- | --- | --- |
| 1 | 10451216379200822465 | Failed: no follow-up | Opening partner had left; no continuation was permitted. |
| 2 | 10905525725756348110 | Passed | Same present pavlik43; coherent with day and own previous message. |
| 3 | 2092789425003139053 | Failed: screenshot navigation | Same present arbuz; conversation assertions completed, but generated text was unclear/off-topic. |

Each raw log contains exactly the three recorded phrases. The final phrase is unchanged in all three: `Жесть. Устал, наверное.`; its global primary act remains Statement. Durations were 75.002, 75.004 and 75.003 real seconds. All three rejected `Так, секунду.` without publishing a response to it. States 1/2/3 match the predeclared manifest; all attempted outcomes are retained. This assessment did not run Unity or repeat any attempt.

## Content and eligibility

**Attempt 1:** Kotik_30 answered the opening with `ахах, как котёнок после работы` at 9.546 seconds. This is a vague but personal reference to work, consistent with the supplied stressful-work/low-energy day; it is not merely a greeting. At the final phrase (42.924 seconds), the same thread was 33.376 seconds old with one turn, but `threadWatching=false`, `threadEpoch=1`, and `rosterEpoch=0`. The viewer was already absent at the filler phrase. Silence therefore preserves the required presence rule; it does not establish a continuation failure for an eligible partner. The XML nevertheless correctly remains a failed acceptance attempt.

Noob228's `а это уже не просто чат?` and zhenya17's `ну и ладно, пусть будет так` belong to `chatter.1` and `chatter.2`, not speech responses. They must not be counted as opening answers or filler reactions. The empty/Malformed warm-up is recorded separately; the actual opening request succeeded through the model.

**Attempt 2:** pavlik43's `ниче не делал` follows the supplied lazy-day/chill/lying-with-phone state. The later `даже не встал` is a plausible dry reply to the streamer's tiredness remark and remains consistent with the same day and explicit own-line context. The partner was still watching, epoch 1 matched, thread age was 33.592 seconds, and turn count was one. The published continuation arrived about 2.30 seconds after recognition. Both replies came from the actual model; no ambient lines were published.

**Attempt 3:** Two allowed opening replies appeared: vortex_28's `классы сегодня` (awkward Russian, but grounded in the supplied classes day) and arbuz's `спокойно лежу с телефоном` (directly consistent with the lazy-day state). Arbuz was the last published conversational responder and therefore correctly owned the thread. At the final phrase, arbuz was watching, epoch 1 matched, age was 29.140 seconds, and turn count was one. The same viewer's follow-up was published about 1.98 seconds after recognition.

However, `круто, что так долго выдержат` does not clearly answer the tiredness remark or continue `спокойно лежу с телефоном`. The model received the correct day, own line and Answer plan, and the output was allowed and displayed, so this is an observed generation/semantic-quality limitation. It is not a clean semantic acceptance. No identifiable fabricated hardware model, purchase, amount or prior-stream fact appears in these published replies; that narrower observation does not make the unclear reply semantically valid.

Attempt 3 wrote its `accepted` record only for the automated conversation assertions, then failed inside `CaptureApp`/`Click` on `desktop.app.streamly`. The XML stack identifies DesktopFlowPlayModeTests.cs lines 758 and 670. The record must not be promoted to a full test pass or manual semantic approval. No ambient messages were published in this attempt.

## Limits and artifacts

Inspected directly: `full-final.xml`, `attempt-1.xml`, `attempt-2.xml`, `attempt-3.xml`, each `attempt-N/raw.jsonl`, the final predicate/planner/selector changes, and the predeclared attempt manifest. Message attribution uses event keys and intent IDs plus `published` records, because the earlier `reaction` log record has an empty text field before publication finalizes it.

All scene attempts replay the existing recorded microphone corpus through the scene pipeline. They are not new live microphone captures. Fresh human microphone acceptance remains prepared but unperformed. The narrow code change has passed automated tests; the three scene results and the semantic limitation must remain visible, and they do not justify declaring Viewer Core complete.
