# Real microphone: 60–90 seconds after the conversation quality pass

**Prepared, not performed.** The controlled benchmark in [the quality report](ViewerCoreConversationQualityReport.md) does not replace this check. Viewer Core is not accepted yet and nothing is merged to main.

## Setup

1. Open `GL`. Keep Whisper turbo. The chat model is whatever `Assets/Game/Config/Viewers/ViewerCore.asset` names: this pass left it on `mistralai/ministral-3-8b-instruct-2512`. If you decide to try the recommended `google/gemma-3-4b`, change only that `Model` line, unload Ministral in LM Studio, load Gemma with the same 4096 context, and record which model the session used.
2. Enable Russian recognition, connect Trich and start the stream with 2–3 viewers present. kritik228 or ZinaIvanovna should be among them for step 3; if neither is present, name whoever is. An absent viewer is not fixed by deleting saves or inventing a reply.
3. In **GO! LIVE → Viewer Core → Reaction Monitor** enable **Record session** (and **+ mic audio** if you want the audio for the review).

## Script

| Time | Say / do | What must happen |
|---|---|---|
| 0–20 s | «Как у вас дела, что сегодня делали?» and wait. | Usually two different answers, staggered. Each viewer states their own mood or day in plain words. No two answers are the same "норм". |
| 20–45 s | Reply to the last viewer who answered: «А почему так?» if they sounded unhappy or tired, otherwise «И как, получилось?». | The same viewer answers that question: an actual reason or result from their day, not a greeting, "как обычно" or a question back. |
| 45–60 s | «Понятно. Часто так?» | The same viewer again. A short answer that fits what they said a moment ago. |
| 60–80 s | «Критик, а ты что сегодня делал?» (or «Зина Ивановна, а что вы сегодня делали?», or another present viewer's name). | That viewer answers with what they did today. Nobody else answers instead of them. |
| 80–90 s | «Так, секунду». Pause, then stop recording. | Normally silence. Background chatter is judged separately. |

The usual limits still apply: six seconds between one viewer's answers, a 40-second conversation window, at most five turns, presence and the current visit. A rate-limited step may stay silent; do not repeat the take to get a nicer result. Keep bad answers and silences.

## What to check in `Logs/ViewerSessions`

For each streamer line: recognized text, `conversationTarget` (`group`, then `[active-thread]`, then `[explicit]`), selected viewer, `questionPurpose` (expected `Mood`/`TodayActivity`, then `ReasonForMood` or `Explanation`, then a generic follow-up with day facts, then `TodayActivity`), `directAnswerFacts`, `previousViewerLine`, published text, source (`LanguageModel` or `Fallback`) and delays.

A fallback line must answer the question from the same day (for example «весь день катал ранкед и сливал»). A stock line such as «норм, а ты как?» after a direct question is a failure.

## Pass criterion

**Does it read like a short conversation with a few real people?** Each answer must answer the question, stay true to the supplied day, read as natural Russian chat, and follow from the previous line. Correct routing and green tests are not enough. If it fails, keep the log and report which line broke the exchange; Viewer Core stays unaccepted.
