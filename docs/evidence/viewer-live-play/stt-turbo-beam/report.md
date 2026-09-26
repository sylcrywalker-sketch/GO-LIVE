# Speech model comparison: 20260926-182411

Take: 95.2 s at 48000 Hz, 25 marked phrases. Oracle boundaries = the speaker's Space marks.
All ru configurations force Russian, including any deliberately English control phrases. Transcript means production adapter output after artifact/no-speech filtering, not native tokens. Content recall is a lexical heuristic; review conversational meaning manually. RAM is Windows process PrivateUsage before/after real first-phrase warm-up; GPU memory is total device-0 nvidia-smi usage, not per-process or peak. Delta units are MiB; -1 means unavailable. Warm-up text and cold latency are recorded separately in raw.jsonl. GPU requested does not prove GPU execution: retain the Unity/native backend log.
Configurations suffixed tail500 are benchmark-only endpoint experiments: 500 ms zeros appended AFTER production AudioPreparation, without changing VAD samples/boundaries or capture delay. Their RTF denominator remains original marked audio duration. Unsuffixed configurations use unchanged production preparation.

## Production VAD segmentation with turbo-q5_0-ru-gpu-beam

26 segments for 25 phrases (settings: min speech 160 ms, silence timeout 650 ms, pre-roll 250 ms, floor 0.012).

| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |
|---:|---:|---:|---:|---|---|---:|---|
| 1 | 0.59 | 5.34 | 2.98 |  | 1 | 340 | Всем привет, парни! Как дела? Как настроение? Что сегодня делали? |
| 2 | 8.03 | 11.84 | 2.02 |  | 2 | 380 | привет привет дорогой друг как вообще расскажите что думаете |
| 3 | 13.41 | 15.10 | 0.86 |  | 3 | 400 | Вы можете мне хоть что-то ответить? |
| 4 | 16.57 | 17.78 | 0.66 |  | 4 | 0 | так секунду |
| 5 | 19.45 | 20.92 | 1.04 |  | 5 | 0 | а то что сегодня играл |
| 6 | 21.46 | 24.66 | 1.54 |  | 6 | 360 | Чат, во что сегодня поиграем? |
| 7 | 25.77 | 28.28 | 1.32 |  | 7 | 0 | ребят как он звук сегодня нормально слышно |
| 8 | 29.57 | 31.60 | 1.12 |  | 8 | 380 | Жесть. Устал, наверное. |
| 9 | 32.57 | 35.52 | 1.86 |  | 9 | 0 | сегодня просто болтаем никуда не торопимся |
| 10 | 36.55 | 38.72 | 0.96 |  | 10 | 260 | блин опять все завис |
| 11 | 39.93 | 41.76 | 1.26 |  | 11 | 0 | если я сейчас проиграю, я удаляю игру |
| 12 | 42.99 | 44.92 | 1.26 |  | 12 | 0 | Спасибо за фоллоу, очень приятно. |
| 13 | 47.17 | 49.16 | 1.40 |  | 13 | 380 | завтра куплю новую видеокарту |
| 14 | 49.70 | 50.34 | 0.50 |  | 13 | 280 | Обещаю. |
| 15 | 51.49 | 53.60 | 1.02 |  | 14 | 0 | короче ну это самое |
| 16 | 54.87 | 57.26 | 0.78 |  | 15 | 280 | Мика, ты тут? Как ты? |
| 17 | 58.31 | 60.22 | 1.06 |  | 16 | 360 | night wall опять ночная смена |
| 18 | 61.55 | 62.88 | 0.78 |  | 17 | 180 | а вы откуда вообще ребят |
| 19 | 64.19 | 65.78 | 1.04 |  | 18 | 160 | что делаете сегодня вечером |
| 20 | 67.47 | 69.32 | 1.08 |  | 19 | 60 | Пойду чайку налью, сейчас вернусь. |
| 21 | 70.57 | 72.40 | 1.12 |  | 20 | 320 | Ну что, как вам мой новый микрофон? |
| 22 | 73.23 | 74.88 | 1.02 |  | 21 | 0 | слушайте а какие игры вы любите |
| 23 | 76.55 | 78.50 | 1.08 |  | 22 | 400 | стрим только начался сейчас разогреемся |
| 24 | 81.21 | 82.60 | 0.82 |  | 23 | 400 | Эээ, ладно. |
| 25 | 84.39 | 86.48 | 1.32 |  | 24 | 340 | Hello chat, how are you doing today? |
| 26 | 90.39 | 91.74 | 0.76 |  | 25 | 0 | Hello, how are you? |

- phrase 13 «Завтра куплю новую видеокарту, обещаю.» → 2 segments

## Production VAD segmentation with turbo-q5_0-ru-gpu-beam-tail500

26 segments for 25 phrases (settings: min speech 160 ms, silence timeout 650 ms, pre-roll 250 ms, floor 0.012).

| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |
|---:|---:|---:|---:|---|---|---:|---|
| 1 | 0.59 | 5.34 | 2.98 |  | 1 | 340 | Всем привет, парни! Как дела? Как настроение? Что сегодня делали? |
| 2 | 8.03 | 11.84 | 2.02 |  | 2 | 380 | привет привет дорогой друг как вообще расскажите что думаете |
| 3 | 13.41 | 15.10 | 0.86 |  | 3 | 400 | Вы можете мне хоть что-то ответить? |
| 4 | 16.57 | 17.78 | 0.66 |  | 4 | 0 | так секунду |
| 5 | 19.45 | 20.92 | 1.04 |  | 5 | 0 | а то что сегодня играл |
| 6 | 21.46 | 24.66 | 1.54 |  | 6 | 360 | Чат, во что сегодня поиграем? |
| 7 | 25.77 | 28.28 | 1.32 |  | 7 | 0 | ребят как он звук сегодня нормально слышно |
| 8 | 29.57 | 31.60 | 1.12 |  | 8 | 380 | жесть устал наверное |
| 9 | 32.57 | 35.52 | 1.86 |  | 9 | 0 | сегодня просто болтаем никуда не торопимся |
| 10 | 36.55 | 38.72 | 0.96 |  | 10 | 260 | блин опять все завис |
| 11 | 39.93 | 41.76 | 1.26 |  | 11 | 0 | если я сейчас проиграю, я удаляю игру |
| 12 | 42.99 | 44.92 | 1.26 |  | 12 | 0 | Спасибо за фоллоу, очень приятно. |
| 13 | 47.17 | 49.16 | 1.40 |  | 13 | 380 | завтра куплю новую видеокарту |
| 14 | 49.70 | 50.34 | 0.50 |  | 13 | 280 | Обещаю. |
| 15 | 51.49 | 53.60 | 1.02 |  | 14 | 0 | короче ну это самое |
| 16 | 54.87 | 57.26 | 0.78 |  | 15 | 280 | Мика, ты тут? Как ты? |
| 17 | 58.31 | 60.22 | 1.06 |  | 16 | 360 | night wall опять ночная смена |
| 18 | 61.55 | 62.88 | 0.78 |  | 17 | 180 | а вы откуда вообще ребят |
| 19 | 64.19 | 65.78 | 1.04 |  | 18 | 160 | что делаете сегодня вечером |
| 20 | 67.47 | 69.32 | 1.08 |  | 19 | 60 | Пойду чайку налью, сейчас вернусь. |
| 21 | 70.57 | 72.40 | 1.12 |  | 20 | 320 | Ну что, как вам мой новый микрофон? |
| 22 | 73.23 | 74.88 | 1.02 |  | 21 | 0 | слушайте а какие игры вы любите |
| 23 | 76.55 | 78.50 | 1.08 |  | 22 | 400 | стрим только начался сейчас разогреемся |
| 24 | 81.21 | 82.60 | 0.82 |  | 23 | 400 | Эээ, ладно. |
| 25 | 84.39 | 86.48 | 1.32 |  | 24 | 340 | Hello chat, how are you doing today? |
| 26 | 90.39 | 91.74 | 0.76 |  | 25 | 0 | Hello, how are you? |

- phrase 13 «Завтра куплю новую видеокарту, обещаю.» → 2 segments

## Summary

CER/WER: character/word error rate after lowercasing, ё→е and dropping punctuation. Content recall: share of the reference's content words (3+ letters, not filler) whose first 4 letters appear in the transcript. Invented words: transcript words matching no reference word by the same rule. Act match: the Viewer Core speech acts of the transcript equal those of the reference text. Latency: recognition only (oracle phrase audio), after one warm-up call.

| Config | Load s | +RAM MB | +GPU MB | Mean latency s | p90 s | RTF | CER | WER | Content recall | Invented words | Act match |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| turbo-q5_0-ru-gpu-beam | 0.55 | 1957 | 1023 | 0.19 | 0.19 | 0.05 | 0.04 | 0.06 | 0.95 | 4 | 22/25 |
| turbo-q5_0-ru-gpu-beam-tail500 | 0.32 | 1169 | 1012 | 0.17 | 0.19 | 0.05 | 0.06 | 0.08 | 0.93 | 4 | 22/25 |

## Per phrase

### 1. «Всем привет, парни! Как дела? Как настроение? Что сегодня делали?» (5.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Всем привет, парни! Как дела? Как настроение? Что сегодня делали? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.19 |
| turbo-q5_0-ru-gpu-beam-tail500 | Всем привет, парни! Как дела? Как настроение? Что сегодня делали? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.20 |

### 2. «Привет, привет, дорогой друг. Как вы вообще? Расскажите, что думаете.» (6.7 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Привет, привет, дорогой друг. Как вы вообще? Расскажите, что думаете. | ru | 0.00 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.76 |
| turbo-q5_0-ru-gpu-beam-tail500 | Привет, привет, дорогой друг. Как вы вообще? Расскажите, что думаете. | ru | 0.00 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.20 |

### 3. «Вы можете мне хоть что-то ответить?» (3.2 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Вы можете мне хоть что-то ответить? | ru | 0.00 | 1.00 | QuestionToChat | 0.16 |
| turbo-q5_0-ru-gpu-beam-tail500 | Вы можете мне хоть что-то ответить? | ru | 0.00 | 1.00 | QuestionToChat | 0.16 |

### 4. «Так, секунду.» (2.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | так секунду | ru | 0.00 | 1.00 | Filler | 0.15 |
| turbo-q5_0-ru-gpu-beam-tail500 | так секунду | ru | 0.00 | 1.00 | Filler | 0.16 |

### 5. «А ты во что сегодня играл?» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.15 |
| turbo-q5_0-ru-gpu-beam-tail500 | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.15 |

### 6. «Чат, во что сегодня поиграем?» (3.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Чат, во что сегодня поиграем? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.16 |
| turbo-q5_0-ru-gpu-beam-tail500 | Чат, во что сегодня поиграем? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.16 |

### 7. «Ребят, как вам звук сегодня, нормально слышно?» (3.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Ребят, как вам звук сегодня? Нормально слышно? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.19 |
| turbo-q5_0-ru-gpu-beam-tail500 | Ребят, как вам звук сегодня? Нормально слышно? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.19 |

### 8. «Жесть. Устал, наверное?» (3.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Жесть Устал, наверное | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.15 |
| turbo-q5_0-ru-gpu-beam-tail500 | Жесть Устал, наверное | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.16 |

### 9. «Сегодня просто болтаем, никуда не торопимся.» (3.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Сегодня просто болтаем, никуда не торопимся | ru | 0.00 | 1.00 | Statement | 0.17 |
| turbo-q5_0-ru-gpu-beam-tail500 | Сегодня просто болтаем, никуда не торопимся | ru | 0.00 | 1.00 | Statement | 0.17 |

### 10. «Блин, опять всё зависло, капец.» (3.4 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Блин, опять все зависло Капец | ru | 0.00 | 1.00 | Statement | 0.16 |
| turbo-q5_0-ru-gpu-beam-tail500 | Блин, опять все зависло Капец | ru | 0.00 | 1.00 | Statement | 0.17 |

### 11. «Если я сейчас проиграю, я удаляю игру.» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Если я сейчас проиграю, я удаляю игру. | ru | 0.00 | 1.00 | Statement | 0.18 |
| turbo-q5_0-ru-gpu-beam-tail500 | Если я сейчас проиграю, я удаляю игру. | ru | 0.00 | 1.00 | Statement | 0.18 |

### 12. «Спасибо за фоллоу, очень приятно!» (2.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Спасибо за фоллоу, очень приятно | ru | 0.00 | 1.00 | Statement | 0.16 |
| turbo-q5_0-ru-gpu-beam-tail500 | Спасибо за фоллоу, очень приятно | ru | 0.00 | 1.00 | Statement | 0.16 |

### 13. «Завтра куплю новую видеокарту, обещаю.» (5.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Завтра куплю новую видеокарту Обещаю | ru | 0.00 | 1.00 | PromiseCandidate | 0.18 |
| turbo-q5_0-ru-gpu-beam-tail500 | Завтра куплю новую видеокарту Обещаю | ru | 0.00 | 1.00 | PromiseCandidate | 0.19 |

### 14. «Короче, ну, это самое...» (3.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Короче, ну, это самое... | ru | 0.00 | 1.00 | Statement | 0.15 |
| turbo-q5_0-ru-gpu-beam-tail500 | Короче, ну, это самое... | ru | 0.00 | 1.00 | Statement | 0.15 |

### 15. «Мика, ты тут? Как ты?» (3.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | «Мика, ты тут?» «Как ты?» | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.18 |
| turbo-q5_0-ru-gpu-beam-tail500 | «Мика, ты тут?» «Как ты?» | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.18 |

### 16. «Найт Оул, опять ночная смена?» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Найтволл, опять ночная смена | ru | 0.07 | 0.80 | Statement (ref QuestionToChat) | 0.16 |
| turbo-q5_0-ru-gpu-beam-tail500 | Найтволл, опять ночная смена | ru | 0.07 | 0.80 | Statement (ref QuestionToChat) | 0.16 |

### 17. «А вы откуда вообще, ребят?» (2.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | А вы откуда вообще, ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.15 |
| turbo-q5_0-ru-gpu-beam-tail500 | А вы откуда вообще, ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.16 |

### 18. «Что делаете сегодня вечером?» (3.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.15 |
| turbo-q5_0-ru-gpu-beam-tail500 | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.16 |

### 19. «Пойду чайку налью, сейчас вернусь.» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Пойду чайку налью, сейчас вернусь | ru | 0.00 | 1.00 | Statement | 0.18 |
| turbo-q5_0-ru-gpu-beam-tail500 | Пойду чайку налью, сейчас вернусь | ru | 0.00 | 1.00 | Statement | 0.18 |

### 20. «Ну что, как вам мой новый микрофон?» (2.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Ну что, как вам мой новый микрофон? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.16 |
| turbo-q5_0-ru-gpu-beam-tail500 | Ну что, как вам мой новый микрофон? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.17 |

### 21. «Слушайте, а какие игры вы любите?» (3.0 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.17 |
| turbo-q5_0-ru-gpu-beam-tail500 | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.17 |

### 22. «Стрим только начался, сейчас разогреемся.» (4.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.18 |
| turbo-q5_0-ru-gpu-beam-tail500 | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.18 |

### 23. «Эээ... ладно.» (2.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Эээ, ладно. | ru | 0.00 | 1.00 | Filler | 0.14 |
| turbo-q5_0-ru-gpu-beam-tail500 | Ладно. | ru | 0.44 | 0.50 | Filler | 0.15 |

### 24. «Hello chat, how are you doing today?» (3.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Hello chat, how are you doing today? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.16 |
| turbo-q5_0-ru-gpu-beam-tail500 | Hello chat, how are you doing today? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.16 |

### 25. «Хай, hello, how are you?» (4.7 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| turbo-q5_0-ru-gpu-beam | Привет. Привет, как ты? | ru | 0.90 | 0.00 | Greeting, QuestionToViewer, PersonalQuestion (ref Greeting, QuestionToChat, PersonalQuestion) | 0.16 |
| turbo-q5_0-ru-gpu-beam-tail500 | Привет. Привет, как ты? | ru | 0.90 | 0.00 | Greeting, QuestionToViewer, PersonalQuestion (ref Greeting, QuestionToChat, PersonalQuestion) | 0.16 |
