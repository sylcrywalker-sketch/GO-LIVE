# Speech model comparison: 20260926-182411

Take: 95.2 s at 48000 Hz, 25 marked phrases. Oracle boundaries = the speaker's Space marks.
All ru configurations force Russian, including any deliberately English control phrases. Transcript means production adapter output after artifact/no-speech filtering, not native tokens. Content recall is a lexical heuristic; review conversational meaning manually. RAM is Windows process PrivateUsage before/after real first-phrase warm-up; GPU memory is total device-0 nvidia-smi usage, not per-process or peak. Delta units are MiB; -1 means unavailable. Warm-up text and cold latency are recorded separately in raw.jsonl. GPU requested does not prove GPU execution: retain the Unity/native backend log.
Configurations suffixed tail500 are benchmark-only endpoint experiments: 500 ms zeros appended AFTER production AudioPreparation, without changing VAD samples/boundaries or capture delay. Their RTF denominator remains original marked audio duration. Unsuffixed configurations use unchanged production preparation.

## Production VAD segmentation with small-q5_1-ru-gpu-tail500

26 segments for 25 phrases (settings: min speech 160 ms, silence timeout 650 ms, pre-roll 250 ms, floor 0.012).

| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |
|---:|---:|---:|---:|---|---|---:|---|
| 1 | 0.59 | 5.34 | 2.98 |  | 1 | 340 | Всем привет парни, как дела, как настроение, что сегодня делали? |
| 2 | 8.03 | 11.84 | 2.02 |  | 2 | 380 | Привет, привет, дорогой друг. Как вообще? Расскажите, что думаете. |
| 3 | 13.41 | 15.10 | 0.86 |  | 3 | 400 | Вы можете мне хоть что-то отвезти? |
| 4 | 16.57 | 17.78 | 0.66 |  | 4 | 0 | Так, секунду. |
| 5 | 19.45 | 20.92 | 1.04 |  | 5 | 0 | А ты во что сегодня играл? |
| 6 | 21.46 | 24.66 | 1.54 |  | 6 | 360 | Чат, а что сегодня поиграем? |
| 7 | 25.77 | 28.28 | 1.32 |  | 7 | 0 | Ребят, как вам звук сегодня? Нормально слышно? |
| 8 | 29.57 | 31.60 | 1.12 |  | 8 | 380 | Жесть. Устал, наверное. |
| 9 | 32.57 | 35.52 | 1.86 |  | 9 | 0 | Сегодня просто болтаем, никуда не торопимся. |
| 10 | 36.55 | 38.72 | 0.96 |  | 10 | 260 | Блин, опять все зависло. Капец. |
| 11 | 39.93 | 41.76 | 1.26 |  | 11 | 0 | Если я сейчас проиграю, я удаляю игру. |
| 12 | 42.99 | 44.92 | 1.26 |  | 12 | 0 | Спасибо за фолу, очень приятно |
| 13 | 47.17 | 49.16 | 1.40 |  | 13 | 380 | Затрак куплю новую видеокарту. |
| 14 | 49.70 | 50.34 | 0.50 |  | 13 | 280 | Обещаю. |
| 15 | 51.49 | 53.60 | 1.02 |  | 14 | 0 | Короче, ну, это самое. |
| 16 | 54.87 | 57.26 | 0.78 |  | 15 | 280 | Микка ты тут? Как ты? |
| 17 | 58.31 | 60.22 | 1.06 |  | 16 | 360 | Найт Уолл опять начнёсмена |
| 18 | 61.55 | 62.88 | 0.78 |  | 17 | 180 | А вы откуда вообще, ребят? |
| 19 | 64.19 | 65.78 | 1.04 |  | 18 | 160 | Что делаете сегодня вечером? |
| 20 | 67.47 | 69.32 | 1.08 |  | 19 | 60 | Пойду чай икуналью и сейчас вернусь. |
| 21 | 70.57 | 72.40 | 1.12 |  | 20 | 320 | Ну что, как вам мой новый микрофон? |
| 22 | 73.23 | 74.88 | 1.02 |  | 21 | 0 | Слушайте, а какие игры вы любите? |
| 23 | 76.55 | 78.50 | 1.08 |  | 22 | 400 | стрим только начался, сейчас разогреемся |
| 24 | 81.21 | 82.60 | 0.82 |  | 23 | 400 | Ладно. |
| 25 | 84.39 | 86.48 | 1.32 |  | 24 | 340 | Привет, Шат. Как сегодня? |
| 26 | 90.39 | 91.74 | 0.76 |  | 25 | 0 | Привет. Как ты? |

- phrase 13 «Завтра куплю новую видеокарту, обещаю.» → 2 segments

## Production VAD segmentation with small-q5_1-ru-gpu-beam-tail500

26 segments for 25 phrases (settings: min speech 160 ms, silence timeout 650 ms, pre-roll 250 ms, floor 0.012).

| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |
|---:|---:|---:|---:|---|---|---:|---|
| 1 | 0.59 | 5.34 | 2.98 |  | 1 | 340 | Всем привет парни, как дела, как настроение, что сегодня делали? |
| 2 | 8.03 | 11.84 | 2.02 |  | 2 | 380 | Привет, привет, дорогой друг. Как вообще? Расскажите, что думаете. |
| 3 | 13.41 | 15.10 | 0.86 |  | 3 | 400 | Вы можете мне хоть что-то отвезти? |
| 4 | 16.57 | 17.78 | 0.66 |  | 4 | 0 | Так, секунду. |
| 5 | 19.45 | 20.92 | 1.04 |  | 5 | 0 | А ты во что сегодня играл? |
| 6 | 21.46 | 24.66 | 1.54 |  | 6 | 360 | Чат, во что сегодня поиграем? |
| 7 | 25.77 | 28.28 | 1.32 |  | 7 | 0 | Ребят, как вам звук сегодня? Нормально слышно? |
| 8 | 29.57 | 31.60 | 1.12 |  | 8 | 380 | Жесть. Устал, наверное. |
| 9 | 32.57 | 35.52 | 1.86 |  | 9 | 0 | Сегодня просто болтаем, никуда не торопимся. |
| 10 | 36.55 | 38.72 | 0.96 |  | 10 | 260 | Блин, опять все зависло. Капец. |
| 11 | 39.93 | 41.76 | 1.26 |  | 11 | 0 | И если я сейчас проиграю, я удаляю игру. |
| 12 | 42.99 | 44.92 | 1.26 |  | 12 | 0 | Спасибо за фолу, очень приятно |
| 13 | 47.17 | 49.16 | 1.40 |  | 13 | 380 | Затрак куплю новую видеокарту. |
| 14 | 49.70 | 50.34 | 0.50 |  | 13 | 280 | Обещаю. |
| 15 | 51.49 | 53.60 | 1.02 |  | 14 | 0 | Короче, ну, это самое. |
| 16 | 54.87 | 57.26 | 0.78 |  | 15 | 280 | Мека тетут, как ты? |
| 17 | 58.31 | 60.22 | 1.06 |  | 16 | 360 | Найт Уолл опять начиная смена |
| 18 | 61.55 | 62.88 | 0.78 |  | 17 | 180 | А вы откуда вообще, ребят? |
| 19 | 64.19 | 65.78 | 1.04 |  | 18 | 160 | Что делаете сегодня вечером? |
| 20 | 67.47 | 69.32 | 1.08 |  | 19 | 60 | Пойду чай икуналью и сейчас вернусь |
| 21 | 70.57 | 72.40 | 1.12 |  | 20 | 320 | Ну что, как вам мой новый микрофон? |
| 22 | 73.23 | 74.88 | 1.02 |  | 21 | 0 | Слушайте, а какие игры вы любите? |
| 23 | 76.55 | 78.50 | 1.08 |  | 22 | 400 | стрим только начался, сейчас разогреемся |
| 24 | 81.21 | 82.60 | 0.82 |  | 23 | 400 | Ладно |
| 25 | 84.39 | 86.48 | 1.32 |  | 24 | 340 | Привет, шат! Как ты сегодня делаешь это? |
| 26 | 90.39 | 91.74 | 0.76 |  | 25 | 0 | Hello, how are you? |

- phrase 13 «Завтра куплю новую видеокарту, обещаю.» → 2 segments

## Production VAD segmentation with turbo-q5_0-ru-gpu-tail500

26 segments for 25 phrases (settings: min speech 160 ms, silence timeout 650 ms, pre-roll 250 ms, floor 0.012).

| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |
|---:|---:|---:|---:|---|---|---:|---|
| 1 | 0.59 | 5.34 | 2.98 |  | 1 | 340 | Всем привет парни! Как дела? Как настроение? Что сегодня делали? |
| 2 | 8.03 | 11.84 | 2.02 |  | 2 | 380 | привет привет дорогой друг как вообще расскажите что думаете |
| 3 | 13.41 | 15.10 | 0.86 |  | 3 | 400 | вы можете мне хоть что-то ответить |
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
| 21 | 70.57 | 72.40 | 1.12 |  | 20 | 320 | ну что как у мой новый микрофон |
| 22 | 73.23 | 74.88 | 1.02 |  | 21 | 0 | слушайте а какие игры вы любите |
| 23 | 76.55 | 78.50 | 1.08 |  | 22 | 400 | стрим только начался сейчас разогреемся |
| 24 | 81.21 | 82.60 | 0.82 |  | 23 | 400 | Эээ, ладно. |
| 25 | 84.39 | 86.48 | 1.32 |  | 24 | 340 | Привет, чад! Как дела сегодня? |
| 26 | 90.39 | 91.74 | 0.76 |  | 25 | 0 | Привет, как ты? |

- phrase 13 «Завтра куплю новую видеокарту, обещаю.» → 2 segments

## Summary

CER/WER: character/word error rate after lowercasing, ё→е and dropping punctuation. Content recall: share of the reference's content words (3+ letters, not filler) whose first 4 letters appear in the transcript. Invented words: transcript words matching no reference word by the same rule. Act match: the Viewer Core speech acts of the transcript equal those of the reference text. Latency: recognition only (oracle phrase audio), after one warm-up call.

| Config | Load s | +RAM MB | +GPU MB | Mean latency s | p90 s | RTF | CER | WER | Content recall | Invented words | Act match |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| small-q5_1-ru-gpu-tail500 | 0.31 | 1078 | 518 | 0.07 | 0.08 | 0.02 | 0.13 | 0.19 | 0.83 | 17 | 22/25 |
| small-q5_1-ru-gpu-beam-tail500 | 0.15 | 648 | 606 | 0.13 | 0.17 | 0.04 | 0.05 | 0.11 | 0.92 | 9 | 23/25 |
| turbo-q5_0-ru-gpu-tail500 | 0.67 | 1640 | 940 | 0.14 | 0.15 | 0.04 | 0.10 | 0.11 | 0.89 | 10 | 22/25 |

## Per phrase

### 1. «Всем привет, парни! Как дела? Как настроение? Что сегодня делали?» (5.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Всем привет парни, как дела, как настроение, что сегодня делали? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.08 |
| small-q5_1-ru-gpu-beam-tail500 | Всем привет парни! Как дела? Как настроение? Что сегодня делали? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.16 |
| turbo-q5_0-ru-gpu-tail500 | Всем привет парни! Как дела? Как настроение? Что сегодня делали? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.15 |

### 2. «Привет, привет, дорогой друг. Как вы вообще? Расскажите, что думаете.» (6.7 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Привет, привет дорогой друг! Как вообще? Расскажите что думаете? | ru | 0.05 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.09 |
| small-q5_1-ru-gpu-beam-tail500 | Привет, привет, дорогой друг! Как вообще? Расскажите, что думаете? | ru | 0.05 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.20 |
| turbo-q5_0-ru-gpu-tail500 | привет привет дорогой друг как вообще расскажите что думаете | ru | 0.05 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.14 |

### 3. «Вы можете мне хоть что-то ответить?» (3.2 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Вы можете мне хоть что-то отвезти? | ru | 0.09 | 1.00 | QuestionToChat | 0.06 |
| small-q5_1-ru-gpu-beam-tail500 | Вы можете мне хоть что-то отвезти? | ru | 0.09 | 1.00 | QuestionToChat | 0.12 |
| turbo-q5_0-ru-gpu-tail500 | Вы можете мне хоть что-то ответить? | ru | 0.00 | 1.00 | QuestionToChat | 0.13 |

### 4. «Так, секунду.» (2.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Так, секунду. | ru | 0.00 | 1.00 | Filler | 0.05 |
| small-q5_1-ru-gpu-beam-tail500 | Так, секунду | ru | 0.00 | 1.00 | Filler | 0.08 |
| turbo-q5_0-ru-gpu-tail500 | так секунду | ru | 0.00 | 1.00 | Filler | 0.13 |

### 5. «А ты во что сегодня играл?» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.05 |
| small-q5_1-ru-gpu-beam-tail500 | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.09 |
| turbo-q5_0-ru-gpu-tail500 | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.13 |

### 6. «Чат, во что сегодня поиграем?» (3.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Чат, а что сегодня поиграем? | ru | 0.07 | 1.00 | QuestionToChat, GameplayQuestion | 0.07 |
| small-q5_1-ru-gpu-beam-tail500 | Чат, а что сегодня поиграем? | ru | 0.07 | 1.00 | QuestionToChat, GameplayQuestion | 0.12 |
| turbo-q5_0-ru-gpu-tail500 | Чат, во что сегодня поиграем? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.14 |

### 7. «Ребят, как вам звук сегодня, нормально слышно?» (3.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Ребят, как вам звук сегодня? Нормально слышно? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.08 |
| small-q5_1-ru-gpu-beam-tail500 | Ребят, как вам звук сегодня? Нормально слышно? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.16 |
| turbo-q5_0-ru-gpu-tail500 | Ребят, как вам звук сегодня? Нормально слышно? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.15 |

### 8. «Жесть. Устал, наверное?» (3.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Жесть. Устал, наверное. | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.06 |
| small-q5_1-ru-gpu-beam-tail500 | Жесть, устал, наверное. | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.11 |
| turbo-q5_0-ru-gpu-tail500 | жесть устал наверное | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.13 |

### 9. «Сегодня просто болтаем, никуда не торопимся.» (3.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Сегодня просто болтаем, никуда не торопимся. | ru | 0.00 | 1.00 | Statement | 0.07 |
| small-q5_1-ru-gpu-beam-tail500 | сегодня просто болтаем никуда не торопимся | ru | 0.00 | 1.00 | Statement | 0.14 |
| turbo-q5_0-ru-gpu-tail500 | Сегодня просто болтаем, никуда не торопимся | ru | 0.00 | 1.00 | Statement | 0.14 |

### 10. «Блин, опять всё зависло, капец.» (3.4 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Блин, опять все зависло. Капец. | ru | 0.00 | 1.00 | Statement | 0.07 |
| small-q5_1-ru-gpu-beam-tail500 | Блин, опять всё зависло, капец. | ru | 0.00 | 1.00 | Statement | 0.13 |
| turbo-q5_0-ru-gpu-tail500 | Блин, опять все зависло Капец | ru | 0.00 | 1.00 | Statement | 0.14 |

### 11. «Если я сейчас проиграю, я удаляю игру.» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | И если я сейчас проиграю, я удаляю игру. | ru | 0.06 | 1.00 | Statement | 0.08 |
| small-q5_1-ru-gpu-beam-tail500 | И если я сейчас проиграю, я удаляю игру. | ru | 0.06 | 1.00 | Statement | 0.16 |
| turbo-q5_0-ru-gpu-tail500 | Если я сейчас проиграю, я удаляю игру. | ru | 0.00 | 1.00 | Statement | 0.14 |

### 12. «Спасибо за фоллоу, очень приятно!» (2.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Спасибо за фолу, очень приятно. | ru | 0.06 | 0.75 | Statement | 0.06 |
| small-q5_1-ru-gpu-beam-tail500 | Спасибо за фоллоу, очень приятно. | ru | 0.00 | 1.00 | Statement | 0.12 |
| turbo-q5_0-ru-gpu-tail500 | Спасибо за фоллоу, очень приятно | ru | 0.00 | 1.00 | Statement | 0.13 |

### 13. «Завтра куплю новую видеокарту, обещаю.» (5.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Затрак куплю новую видеокарту. Обещаю. | ru | 0.06 | 0.80 | PromiseCandidate | 0.08 |
| small-q5_1-ru-gpu-beam-tail500 | Затрак куплю новую видеокарту, обещаю. | ru | 0.06 | 0.80 | PromiseCandidate | 0.17 |
| turbo-q5_0-ru-gpu-tail500 | Завтра куплю новую видеокарту Обещаю | ru | 0.00 | 1.00 | PromiseCandidate | 0.14 |

### 14. «Короче, ну, это самое...» (3.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Короче, ну, это самая. | ru | 0.11 | 0.50 | Statement | 0.06 |
| small-q5_1-ru-gpu-beam-tail500 | Короче, ну, это самая. | ru | 0.11 | 0.50 | Statement | 0.10 |
| turbo-q5_0-ru-gpu-tail500 | короче ну это самое | ru | 0.00 | 1.00 | Statement | 0.13 |

### 15. «Мика, ты тут? Как ты?» (3.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Микка, ты тут? Как ты? | ru | 0.06 | 0.50 | QuestionToViewer, PersonalQuestion | 0.06 |
| small-q5_1-ru-gpu-beam-tail500 | "Микка, ты тут, как ты?" | ru | 0.06 | 0.50 | QuestionToViewer, PersonalQuestion | 0.12 |
| turbo-q5_0-ru-gpu-tail500 | Мика, ты тут? Как ты? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.14 |

### 16. «Найт Оул, опять ночная смена?» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Найт Вол, опять ночная смена. | ru | 0.07 | 0.80 | Statement (ref QuestionToChat) | 0.07 |
| small-q5_1-ru-gpu-beam-tail500 | Найт Вол, опять ночная смена. | ru | 0.07 | 0.80 | Statement (ref QuestionToChat) | 0.13 |
| turbo-q5_0-ru-gpu-tail500 | Найт Волл, опять ночная смена | ru | 0.07 | 0.80 | Statement (ref QuestionToChat) | 0.13 |

### 17. «А вы откуда вообще, ребят?» (2.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | А вы откуда вообще, ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.06 |
| small-q5_1-ru-gpu-beam-tail500 | А вы откуда вообще, ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.11 |
| turbo-q5_0-ru-gpu-tail500 | А вы откуда вообще, ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.13 |

### 18. «Что делаете сегодня вечером?» (3.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.06 |
| small-q5_1-ru-gpu-beam-tail500 | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.10 |
| turbo-q5_0-ru-gpu-tail500 | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.13 |

### 19. «Пойду чайку налью, сейчас вернусь.» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Пойду чай икуналью и сейчас вернусь. | ru | 0.16 | 0.80 | Statement | 0.08 |
| small-q5_1-ru-gpu-beam-tail500 | Пойду чай икуна лью и сейчас вернусь. | ru | 0.19 | 0.80 | Statement | 0.17 |
| turbo-q5_0-ru-gpu-tail500 | Пойду чайку налью, сейчас вернусь | ru | 0.00 | 1.00 | Statement | 0.14 |

### 20. «Ну что, как вам мой новый микрофон?» (2.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Ну что, как вам мой новый микрофон? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.07 |
| small-q5_1-ru-gpu-beam-tail500 | Ну что, как вам мой новый микрофон? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.13 |
| turbo-q5_0-ru-gpu-tail500 | Ну что, как вам мой новый микрофон? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.14 |

### 21. «Слушайте, а какие игры вы любите?» (3.0 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.06 |
| small-q5_1-ru-gpu-beam-tail500 | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.13 |
| turbo-q5_0-ru-gpu-tail500 | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.13 |

### 22. «Стрим только начался, сейчас разогреемся.» (4.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.07 |
| small-q5_1-ru-gpu-beam-tail500 | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.14 |
| turbo-q5_0-ru-gpu-tail500 | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.14 |

### 23. «Эээ... ладно.» (2.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Ладно | ru | 0.44 | 0.50 | Filler | 0.04 |
| small-q5_1-ru-gpu-beam-tail500 | Ладно | ru | 0.44 | 0.50 | Filler | 0.09 |
| turbo-q5_0-ru-gpu-tail500 | Ладно. | ru | 0.44 | 0.50 | Filler | 0.12 |

### 24. «Hello chat, how are you doing today?» (3.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Привет, Шат! Как вы сегодня делаете это? | ru | 1.00 | 0.00 | Greeting, QuestionToChat, PersonalQuestion | 0.07 |
| small-q5_1-ru-gpu-beam-tail500 | Hello chat, how are you doing today? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.13 |
| turbo-q5_0-ru-gpu-tail500 | Привет, чад! Как дела сегодня? | ru | 0.91 | 0.00 | Greeting, QuestionToChat, PersonalQuestion | 0.13 |

### 25. «Хай, hello, how are you?» (4.7 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| small-q5_1-ru-gpu-tail500 | Привет! | ru | 1.00 | 0.00 | Greeting (ref Greeting, QuestionToChat, PersonalQuestion) | 0.11 |
| small-q5_1-ru-gpu-beam-tail500 | Хай! Hello! How are you? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.13 |
| turbo-q5_0-ru-gpu-tail500 | Привет. Привет. Как ты? | ru | 0.90 | 0.00 | Greeting, QuestionToViewer, PersonalQuestion (ref Greeting, QuestionToChat, PersonalQuestion) | 0.20 |
