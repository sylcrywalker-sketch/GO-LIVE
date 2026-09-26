# First 75-second recorded replay — failed, retained

Actual GL scene and shipping voice pipeline, one natural anonymous viewer, actual configured local chat model. Three unchanged slices of the recorded microphone corpus were paced in real time. This was not new microphone capture. No audience seats, identities, daily states, response probabilities or model answers were injected.

| Input | Returned transcript | Classification / response | Timing |
|---|---|---|---|
| Opening, mark 1 at 2 s | Всем привет парни, как дела? Как настроение? Что сегодня делали? | PersonalQuestion; selected `anon.0.Ded_ru`, English persona | Recognition 0.884 s after phrase audio end; published 3.752 s after audio end; model generation 0.645 s |
| Filler, mark 4 at 25 s | Так, секунду. | Filler; rejected below threshold; no response | Recognition 0.764 s after audio end |
| Follow-up, mark 8 at 39 s | Жесть. Устал, наверное. | Statement with context-sensitive continuation eligibility; selection resulted in silence | Recognition 0.778 s after audio end |

The opening preserved all questions. The selected day was `gen.stress`: stressful workday, stressed mood, low energy, currently switching off. The plan contained this day solely as ViewerDay, and explicitly treated the streamer's hardware as unknown. Actual returned and published model text was **“slept too much”**. It contained no hard streamer fact, but contradicted the assigned activity. Existing day-activity patterns only recognized Russian. This concrete English consistency defect was added to the correction regressions; the run is not a semantic success.

The follow-up produced no answer in this attempt, so the explicit acceptance assertion failed. The path remains probabilistic; this single outcome is not proof of a broken continuation rule. Its failed result is preserved, not replaced by a successful attempt. Full prompts, day, plan, outputs, rejection, speech and timing are in `raw.jsonl`; `published` rows record actual visible text because the earlier reaction-log event precedes its text assignment.

Unity: 1 explicit test, 0 passed, 1 failed, 82.747 s including setup/teardown. Replay duration: 75 real seconds. No screenshot was captured after the failed assertion.
