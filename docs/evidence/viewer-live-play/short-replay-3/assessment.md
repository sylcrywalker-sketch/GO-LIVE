# Diagnostic 75-second recorded replay — failed follow-up, retained

Same scene, models, recorded slices and real-time pacing, with additional read-only thread/presence telemetry. No tuning or selection override. This is not fresh microphone capture.

At the opening, three viewers were present. All original questions were recognized: **“Всем привет парни, как дела? Как настроение? Что сегодня делали?”**; PersonalQuestion remained primary. Selected `anon.1.pixel_ru` had an errands day, tired mood, low energy, finally sitting down. Actual returned and published text: **“устал после дождей по городу бегать”**. It describes the viewer's own day with a small weather embellishment; no streamer equipment, purchase, money or history was invented. Model latency was 0.889 s. The line appeared 2.917 s after the phrase's audio end (0.913 s recognition/polling, then selection/generation/presentation).

“Так, секунду.” caused no reply. At **“Жесть. Устал, наверное.”**, the same partner was still watching, both epochs were 1, the thread had one published turn, and its age was 32.662 s. The configured window is 40 s. The follow-up again resulted in silence and the explicit assertion failed.

A separate deterministic diagnostic with real one-seat ephemeral rosters, the exact punctuation-free statement, filler, timing and exhausted ordinary budget yielded **64 same-viewer continuations across 100 fixed seeds**. The actual GL asset deserialized to window 40 s, maximum 5 answers, gap 6 s. Both diagnostic tests passed. This supports the configured probabilistic path, but does not turn any of the three failed real-model observations into a successful acceptance. The cause of each random selection outcome was not forced or rewritten.

Unity: 1 explicit test, 0 passed, 1 failed, 82.855 s including setup/teardown. Replay duration 75.003 s. Full raw evidence remains unchanged. In this diagnostic run only, the first speech snapshot has `threadAge: Infinity` because no thread existed; subsequent telemetry is finite. The fixture now emits `-1` for an absent thread. Pre-audio reaction timestamps use `-1` as well. No screenshot was captured after failure.
