# Speech model comparison: 20260926-182411

Take: 95.2 s at 48000 Hz, 25 marked phrases. Oracle boundaries = the speaker's Space marks.
All ru configurations force Russian, including any deliberately English control phrases. Content recall is a lexical heuristic; review conversational meaning manually. RAM is process-private before/after warm-up, GPU memory is total device-0 nvidia-smi usage, not per-process or peak. GPU requested does not prove GPU execution: retain the Unity/native backend log.

## Production VAD segmentation with tiny-ru-gpu

26 segments for 25 phrases (settings: min speech 160 ms, silence timeout 650 ms, pre-roll 250 ms, floor 0.012).

| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |
|---:|---:|---:|---:|---|---|---:|---|
| 1 | 0.59 | 5.34 | 2.98 |  | 1 | 340 | Всем привет партнем, как делан, как настроение, что сегодня делали. |
| 2 | 8.03 | 11.84 | 2.02 |  | 2 | 380 | Привет, привет, дорогой друг. Как вообще? Расскажите, что думаете? |
| 3 | 13.41 | 15.10 | 0.86 |  | 3 | 400 | Вы можете мне хоть что-то ответить. |
| 4 | 16.57 | 17.78 | 0.66 |  | 4 | 0 | Так, секунду. |
| 5 | 19.45 | 20.92 | 1.04 |  | 5 | 0 | А ты во что сегодня не играл? |
| 6 | 21.46 | 24.66 | 1.54 |  | 6 | 360 | Чато, что сегодня поиграем. |
| 7 | 25.77 | 28.28 | 1.32 |  | 7 | 0 | Ребят, как он звук сегодня. Нормально слышно. |
| 8 | 29.57 | 31.60 | 1.12 |  | 8 | 380 | Жесть. Устал, наверное. |
| 9 | 32.57 | 35.52 | 1.86 |  | 9 | 0 | Сегодня просто болтаем, никуда не торопимся. |
| 10 | 36.55 | 38.72 | 0.96 |  | 10 | 260 | Блин, опять всё зависло, капец. |
| 11 | 39.93 | 41.76 | 1.26 |  | 11 | 0 | И сейчас проиграй я уда ли угру. |
| 12 | 42.99 | 44.92 | 1.26 |  | 12 | 0 | Спасибо за фролу очень приятно! |
| 13 | 47.17 | 49.16 | 1.40 |  | 13 | 380 | Затракуплю новую видео карту. |
| 14 | 49.70 | 50.34 | 0.50 |  | 13 | 280 | Обещаю. |
| 15 | 51.49 | 53.60 | 1.02 |  | 14 | 0 | Короче, ну это самое. |
| 16 | 54.87 | 57.26 | 0.78 |  | 15 | 280 | Микат detут, как ты. |
| 17 | 58.31 | 60.22 | 1.06 |  | 16 | 360 | На этого лопетничная смена. |
| 18 | 61.55 | 62.88 | 0.78 |  | 17 | 180 | А вы откуда вообще ребят? |
| 19 | 64.19 | 65.78 | 1.04 |  | 18 | 160 | Что делать сегодня вечером? |
| 20 | 67.47 | 69.32 | 1.08 |  | 19 | 60 | Пойдучи и куналью сейчас вернусь. |
| 21 | 70.57 | 72.40 | 1.12 |  | 20 | 320 | Ну что, как у мой новый микрофон? |
| 22 | 73.23 | 74.88 | 1.02 |  | 21 | 0 | Слушайте, а какие игровы любят? |
| 23 | 76.55 | 78.50 | 1.08 |  | 22 | 400 | Стрем только начаться сейчас разогреемся. |
| 24 | 81.21 | 82.60 | 0.82 |  | 23 | 400 | Ладно. |
| 25 | 84.39 | 86.48 | 1.32 |  | 24 | 340 | Всем привет! Как вы делаете? |
| 26 | 90.39 | 91.74 | 0.76 |  | 25 | 0 | Алло, how are you? |

- phrase 13 «Завтра куплю новую видеокарту, обещаю.» → 2 segments

## Production VAD segmentation with base-ru-gpu

26 segments for 25 phrases (settings: min speech 160 ms, silence timeout 650 ms, pre-roll 250 ms, floor 0.012).

| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |
|---:|---:|---:|---:|---|---|---:|---|
| 1 | 0.59 | 5.34 | 2.98 |  | 1 | 340 | Всем привет, парни! Как делаем? Как настроение? Что сегодня делали? |
| 2 | 8.03 | 11.84 | 2.02 |  | 2 | 380 | Привет, привет, дорогой друг! Как вообще? Расскажите, что думаете? |
| 3 | 13.41 | 15.10 | 0.86 |  | 3 | 400 | Вы можете мне хоть что-то ответить? |
| 4 | 16.57 | 17.78 | 0.66 |  | 4 | 0 | Так, секунду. |
| 5 | 19.45 | 20.92 | 1.04 |  | 5 | 0 | А ты во что сегодня играл? |
| 6 | 21.46 | 24.66 | 1.54 |  | 6 | 360 | Чат, а что сегодня поиграем? |
| 7 | 25.77 | 28.28 | 1.32 |  | 7 | 0 | Ребят, как он звук сегодня? Нормально слышно. |
| 8 | 29.57 | 31.60 | 1.12 |  | 8 | 380 | Жесть. Устал наверное. |
| 9 | 32.57 | 35.52 | 1.86 |  | 9 | 0 | Сегодня просто болтаем, никуда не торопимся. |
| 10 | 36.55 | 38.72 | 0.96 |  | 10 | 260 | Блин, опять всё зависло, копец. |
| 11 | 39.93 | 41.76 | 1.26 |  | 11 | 0 | И сейчас проиграю, я удаляю игру. |
| 12 | 42.99 | 44.92 | 1.26 |  | 12 | 0 | Спасибо за фолу, очень прият. |
| 13 | 47.17 | 49.16 | 1.40 |  | 13 | 380 | Затрку пленового видеокарту. |
| 14 | 49.70 | 50.34 | 0.50 |  | 13 | 280 | Обещаю. |
| 15 | 51.49 | 53.60 | 1.02 |  | 14 | 0 | Короче, ну это самое |
| 16 | 54.87 | 57.26 | 0.78 |  | 15 | 280 | Микотетут, как ты. |
| 17 | 58.31 | 60.22 | 1.06 |  | 16 | 360 | На эту ОЛО, опять начинает смена? |
| 18 | 61.55 | 62.88 | 0.78 |  | 17 | 180 | А вы откуда вообще ребят? |
| 19 | 64.19 | 65.78 | 1.04 |  | 18 | 160 | Что делаете сегодня вечером? |
| 20 | 67.47 | 69.32 | 1.08 |  | 19 | 60 | Пойду, чай, икунули её сейчас вернусь. |
| 21 | 70.57 | 72.40 | 1.12 |  | 20 | 320 | Ну что как у мой новый микрофон? |
| 22 | 73.23 | 74.88 | 1.02 |  | 21 | 0 | Слушайте, а какие игровы любите? |
| 23 | 76.55 | 78.50 | 1.08 |  | 22 | 400 | В трем только начался, сейчас разогреемся. |
| 24 | 81.21 | 82.60 | 0.82 |  | 23 | 400 | Ладно. |
| 25 | 84.39 | 86.48 | 1.32 |  | 24 | 340 | Привет, чат! Как ты делаешь сегодня? |
| 26 | 90.39 | 91.74 | 0.76 |  | 25 | 0 | -Алло, а вы? |

- phrase 13 «Завтра куплю новую видеокарту, обещаю.» → 2 segments

## Production VAD segmentation with small-q5_1-ru-gpu

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

## Production VAD segmentation with turbo-q5_0-ru-gpu

26 segments for 25 phrases (settings: min speech 160 ms, silence timeout 650 ms, pre-roll 250 ms, floor 0.012).

| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |
|---:|---:|---:|---:|---|---|---:|---|
| 1 | 0.59 | 5.34 | 2.98 |  | 1 | 340 | Всем привет парни! Как дела? Как настроение? Что сегодня делали? |
| 2 | 8.03 | 11.84 | 2.02 |  | 2 | 380 | привет привет дорогой друг как вообще расскажите что думаете |
| 3 | 13.41 | 15.10 | 0.86 |  | 3 | 400 | вы можете мне хоть что-то ответить |
| 4 | 16.57 | 17.78 | 0.66 |  | 4 | 0 | так секунду |
| 5 | 19.45 | 20.92 | 1.04 |  | 5 | 0 | а то что сегодня играл |
| 6 | 21.46 | 24.66 | 1.54 |  | 6 | 360 | Чат, во что сегодня поиграем? |
| 7 | 25.77 | 28.28 | 1.32 |  | 7 | 0 | ребят как он звук сегодня |
| 8 | 29.57 | 31.60 | 1.12 |  | 8 | 380 | жесть устал наверное |
| 9 | 32.57 | 35.52 | 1.86 |  | 9 | 0 | сегодня просто болтаем никуда не |
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
| tiny-ru | 0.45 | 0 | 0 | 0.37 | 0.41 | 0.10 | 0.15 | 0.30 | 0.79 | 21 | 16/25 |
| tiny-ru-gpu | 0.16 | 0 | 247 | 0.16 | 0.63 | 0.04 | 0.15 | 0.30 | 0.79 | 21 | 16/25 |
| base-ru | 0.10 | 0 | 0 | 0.88 | 0.90 | 0.24 | 0.12 | 0.22 | 0.83 | 14 | 21/25 |
| base-ru-gpu | 0.10 | 0 | 289 | 0.07 | 0.05 | 0.02 | 0.12 | 0.22 | 0.83 | 14 | 21/25 |
| small-q5_1-ru | 0.10 | 0 | 0 | 3.30 | 3.35 | 0.90 | 0.15 | 0.21 | 0.81 | 17 | 21/25 |
| small-q5_1-ru-gpu | 0.13 | 0 | 383 | 0.16 | 0.08 | 0.04 | 0.14 | 0.20 | 0.82 | 17 | 21/25 |
| turbo-q5_0-ru-gpu | 0.65 | 0 | 369 | 0.24 | 0.20 | 0.07 | 0.10 | 0.11 | 0.89 | 10 | 22/25 |

## Per phrase

### 1. «Всем привет, парни! Как дела? Как настроение? Что сегодня делали?» (5.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Всем привет партнем! Как дела? Как настроение? Что сегодня делали? | ru | 0.05 | 0.86 | Greeting, QuestionToChat, PersonalQuestion | 0.41 |
| tiny-ru-gpu | Всем привет партнем! Как дела? Как настроение? Что сегодня делали? | ru | 0.05 | 0.86 | Greeting, QuestionToChat, PersonalQuestion | 1.46 |
| base-ru | Всем привет, парни! Как делам? Как настроение? Что сегодня делали? | ru | 0.02 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.90 |
| base-ru-gpu | Всем привет, парни! Как делам? Как настроение? Что сегодня делали? | ru | 0.02 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.05 |
| small-q5_1-ru | Всем привет парни, как дела, как настроение, что сегодня делали? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 3.40 |
| small-q5_1-ru-gpu | Всем привет парни, как дела, как настроение, что сегодня делали? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 1.08 |
| turbo-q5_0-ru-gpu | Всем привет парни! Как дела? Как настроение? Что сегодня делали? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 1.32 |

### 2. «Привет, привет, дорогой друг. Как вы вообще? Расскажите, что думаете.» (6.7 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Привет, привет, дорогой друг. Как вообще? Расскажите что думаете? | ru | 0.05 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.40 |
| tiny-ru-gpu | Привет, привет, дорогой друг. Как вообще? Расскажите что думаете? | ru | 0.05 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.04 |
| base-ru | Привет, дорогой друг! Как вообще? Расскажите, что думаете? | ru | 0.16 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.86 |
| base-ru-gpu | Привет, дорогой друг! Как вообще? Расскажите, что думаете? | ru | 0.16 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.05 |
| small-q5_1-ru | Привет, привет дорогой друг! Как вообще? Расскажите что думаете? | ru | 0.05 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 3.38 |
| small-q5_1-ru-gpu | Привет, привет дорогой друг! Как вообще? Расскажите что думаете? | ru | 0.05 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.08 |
| turbo-q5_0-ru-gpu | привет привет дорогой друг как вообще расскажите что думаете | ru | 0.05 | 1.00 | Greeting, QuestionToChat, OpinionRequest | 0.20 |

### 3. «Вы можете мне хоть что-то ответить?» (3.2 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Выложите мне хоть что-то ответить. | ru | 0.09 | 0.67 | QuestionToChat | 0.36 |
| tiny-ru-gpu | Выложите мне хоть что-то ответить. | ru | 0.09 | 0.67 | QuestionToChat | 0.02 |
| base-ru | Вы можете мне хоть что-то отвезти? | ru | 0.09 | 1.00 | QuestionToChat | 0.94 |
| base-ru-gpu | Вы можете мне хоть что-то отвезти? | ru | 0.09 | 1.00 | QuestionToChat | 0.04 |
| small-q5_1-ru | Вы можете мне хоть что-то отвезти? | ru | 0.09 | 1.00 | QuestionToChat | 3.31 |
| small-q5_1-ru-gpu | Вы можете мне хоть что-то отвезти? | ru | 0.09 | 1.00 | QuestionToChat | 0.06 |
| turbo-q5_0-ru-gpu | Вы можете мне хоть что-то ответить? | ru | 0.00 | 1.00 | QuestionToChat | 0.19 |

### 4. «Так, секунду.» (2.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | так секунду | ru | 0.00 | 1.00 | Filler | 0.34 |
| tiny-ru-gpu | так секунду | ru | 0.00 | 1.00 | Filler | 0.01 |
| base-ru | Так, секунду. | ru | 0.00 | 1.00 | Filler | 0.85 |
| base-ru-gpu | Так, секунду. | ru | 0.00 | 1.00 | Filler | 0.03 |
| small-q5_1-ru | Так, секунду. | ru | 0.00 | 1.00 | Filler | 3.27 |
| small-q5_1-ru-gpu | Так, секунду. | ru | 0.00 | 1.00 | Filler | 0.05 |
| turbo-q5_0-ru-gpu | так секунду | ru | 0.00 | 1.00 | Filler | 0.13 |

### 5. «А ты во что сегодня играл?» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | А ты во что сегодня не играл? | ru | 0.12 | 1.00 | QuestionToViewer, PersonalQuestion | 0.35 |
| tiny-ru-gpu | А ты во что сегодня не играл? | ru | 0.12 | 1.00 | QuestionToViewer, PersonalQuestion | 0.02 |
| base-ru | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.83 |
| base-ru-gpu | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.03 |
| small-q5_1-ru | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 3.25 |
| small-q5_1-ru-gpu | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.05 |
| turbo-q5_0-ru-gpu | А ты во что сегодня играл? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.14 |

### 6. «Чат, во что сегодня поиграем?» (3.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Чато, что сегодня поиграем. | ru | 0.07 | 1.00 | Statement (ref QuestionToChat, GameplayQuestion) | 0.36 |
| tiny-ru-gpu | Чато, что сегодня поиграем. | ru | 0.07 | 1.00 | Statement (ref QuestionToChat, GameplayQuestion) | 0.02 |
| base-ru | Чат, а что сегодня поиграем? | ru | 0.07 | 1.00 | QuestionToChat, GameplayQuestion | 0.88 |
| base-ru-gpu | Чат, а что сегодня поиграем? | ru | 0.07 | 1.00 | QuestionToChat, GameplayQuestion | 0.04 |
| small-q5_1-ru | Чат, а что сегодня поиграем? | ru | 0.07 | 1.00 | QuestionToChat, GameplayQuestion | 3.31 |
| small-q5_1-ru-gpu | Чат, а что сегодня поиграем? | ru | 0.07 | 1.00 | QuestionToChat, GameplayQuestion | 0.07 |
| turbo-q5_0-ru-gpu | Чат, во что сегодня поиграем? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.15 |

### 7. «Ребят, как вам звук сегодня, нормально слышно?» (3.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Ребята, как он звук сегодня? Нормально слышно. | ru | 0.09 | 0.83 | QuestionToChat (ref QuestionToChat, OpinionRequest) | 0.36 |
| tiny-ru-gpu | Ребята, как он звук сегодня? Нормально слышно. | ru | 0.09 | 0.83 | QuestionToChat (ref QuestionToChat, OpinionRequest) | 0.03 |
| base-ru | Ребята, как он звук сегодня? Нормально слышно. | ru | 0.09 | 0.83 | QuestionToChat (ref QuestionToChat, OpinionRequest) | 0.88 |
| base-ru-gpu | Ребята, как он звук сегодня? Нормально слышно. | ru | 0.09 | 0.83 | QuestionToChat (ref QuestionToChat, OpinionRequest) | 0.04 |
| small-q5_1-ru | Ребят, как вам звук сегодня? Нормально слышно? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 3.31 |
| small-q5_1-ru-gpu | Ребят, как вам звук сегодня? Нормально слышно? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.08 |
| turbo-q5_0-ru-gpu | Ребят, как вам звук сегодня? Нормально слышно? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.15 |

### 8. «Жесть. Устал, наверное?» (3.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Жесть, устал, наверное. | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.36 |
| tiny-ru-gpu | Жесть. Устал наверное. | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.02 |
| base-ru | Жесть. Устал наверное. | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.85 |
| base-ru-gpu | Жесть. Устал наверное. | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.03 |
| small-q5_1-ru | Жесть. Устал, наверное. | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 3.32 |
| small-q5_1-ru-gpu | Жесть. Устал, наверное. | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.06 |
| turbo-q5_0-ru-gpu | жесть устал наверное | ru | 0.00 | 1.00 | Statement (ref QuestionToChat, PersonalQuestion) | 0.14 |

### 9. «Сегодня просто болтаем, никуда не торопимся.» (3.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Сегодня просто болтаем, никуда не торопимся. | ru | 0.00 | 1.00 | Statement | 0.36 |
| tiny-ru-gpu | Сегодня просто болтаем, никуда не торопимся. | ru | 0.00 | 1.00 | Statement | 0.03 |
| base-ru | Сегодня просто болтаем, никуда не торопимся. | ru | 0.00 | 1.00 | Statement | 0.86 |
| base-ru-gpu | Сегодня просто болтаем, никуда не торопимся. | ru | 0.00 | 1.00 | Statement | 0.04 |
| small-q5_1-ru | Сегодня просто болтаем, никуда не торопимся. | ru | 0.00 | 1.00 | Statement | 3.32 |
| small-q5_1-ru-gpu | Сегодня просто болтаем, никуда не торопимся. | ru | 0.00 | 1.00 | Statement | 0.07 |
| turbo-q5_0-ru-gpu | Сегодня просто болтаем, никуда не торопимся | ru | 0.00 | 1.00 | Statement | 0.15 |

### 10. «Блин, опять всё зависло, капец.» (3.4 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Блин, опять все зависло. Капец. | ru | 0.00 | 1.00 | Statement | 0.38 |
| tiny-ru-gpu | Блин, опять все зависло. Капец. | ru | 0.00 | 1.00 | Statement | 0.03 |
| base-ru | Блин, опять всё зависло, копец. | ru | 0.04 | 0.75 | Statement | 0.90 |
| base-ru-gpu | Блин, опять всё зависло, копец. | ru | 0.04 | 0.75 | Statement | 0.03 |
| small-q5_1-ru | Блин, опять все зависло. | ru | 0.21 | 0.75 | Statement | 3.27 |
| small-q5_1-ru-gpu | Блин, опять все зависло. Капец. | ru | 0.00 | 1.00 | Statement | 0.07 |
| turbo-q5_0-ru-gpu | Блин, опять все зависло Капец | ru | 0.00 | 1.00 | Statement | 0.15 |

### 11. «Если я сейчас проиграю, я удаляю игру.» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | и сейчас проиграя я удаляю игру. | ru | 0.17 | 0.80 | Statement | 0.44 |
| tiny-ru-gpu | и сейчас проиграя я удаляю игру. | ru | 0.17 | 0.80 | Statement | 1.21 |
| base-ru | И сейчас проиграю, я удаляю игру. | ru | 0.14 | 0.80 | Statement | 0.89 |
| base-ru-gpu | И сейчас проиграю, я удаляю игру. | ru | 0.14 | 0.80 | Statement | 0.04 |
| small-q5_1-ru | И если я сейчас проиграю, я удаляю игру. | ru | 0.06 | 1.00 | Statement | 3.31 |
| small-q5_1-ru-gpu | И если я сейчас проиграю, я удаляю игру. | ru | 0.06 | 1.00 | Statement | 0.08 |
| turbo-q5_0-ru-gpu | Если я сейчас проиграю, я удаляю игру. | ru | 0.00 | 1.00 | Statement | 0.15 |

### 12. «Спасибо за фоллоу, очень приятно!» (2.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Спасибо за фролу очень приятно. | ru | 0.10 | 0.75 | Statement | 0.36 |
| tiny-ru-gpu | Спасибо за фролу очень приятно! | ru | 0.10 | 0.75 | Statement | 0.02 |
| base-ru | Спасибо за фолу, очень прият! | ru | 0.13 | 0.75 | Statement | 0.83 |
| base-ru-gpu | Спасибо за фолу, очень прият! | ru | 0.13 | 0.75 | Statement | 0.03 |
| small-q5_1-ru | Спасибо за фолу, очень приятно. | ru | 0.06 | 0.75 | Statement | 3.29 |
| small-q5_1-ru-gpu | Спасибо за фолу, очень приятно. | ru | 0.06 | 0.75 | Statement | 0.06 |
| turbo-q5_0-ru-gpu | Спасибо за фоллоу, очень приятно | ru | 0.00 | 1.00 | Statement | 0.14 |

### 13. «Завтра куплю новую видеокарту, обещаю.» (5.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Затракуплю новую видео карту. | ru | 0.28 | 0.40 | Statement (ref PromiseCandidate) | 0.35 |
| tiny-ru-gpu | Затракуплю новую видео карту. | ru | 0.28 | 0.40 | Statement (ref PromiseCandidate) | 0.02 |
| base-ru | Затрку пленового видеокарту обещаю. | ru | 0.25 | 0.40 | PromiseCandidate | 0.85 |
| base-ru-gpu | Затрку пленового видеокарту обещаю. | ru | 0.25 | 0.40 | PromiseCandidate | 0.04 |
| small-q5_1-ru | Затрак куплю новую видеокарту. | ru | 0.25 | 0.60 | Statement (ref PromiseCandidate) | 3.31 |
| small-q5_1-ru-gpu | Затрак куплю новую видеокарту. | ru | 0.25 | 0.60 | Statement (ref PromiseCandidate) | 0.07 |
| turbo-q5_0-ru-gpu | Завтра куплю новую видеокарту Обещаю | ru | 0.00 | 1.00 | PromiseCandidate | 0.15 |

### 14. «Короче, ну, это самое...» (3.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Короче, ну это самое. | ru | 0.00 | 1.00 | Statement | 0.36 |
| tiny-ru-gpu | Короче, ну это самое. | ru | 0.00 | 1.00 | Statement | 0.02 |
| base-ru | Короче, ну это самое. | ru | 0.00 | 1.00 | Statement | 0.81 |
| base-ru-gpu | Короче, ну это самое. | ru | 0.00 | 1.00 | Statement | 0.03 |
| small-q5_1-ru | Короче, ну, это самая. | ru | 0.11 | 0.50 | Statement | 3.30 |
| small-q5_1-ru-gpu | Короче, ну, это самая. | ru | 0.11 | 0.50 | Statement | 0.06 |
| turbo-q5_0-ru-gpu | короче ну это самое | ru | 0.00 | 1.00 | Statement | 0.14 |

### 15. «Мика, ты тут? Как ты?» (3.5 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Микат и тут, как-то. | ru | 0.22 | 1.00 | Statement (ref QuestionToViewer, PersonalQuestion) | 0.41 |
| tiny-ru-gpu | Микат и тут как-то. | ru | 0.22 | 1.00 | Statement (ref QuestionToViewer, PersonalQuestion) | 0.06 |
| base-ru | Мико-то тут как ты. | ru | 0.11 | 0.50 | QuestionToViewer, PersonalQuestion | 0.84 |
| base-ru-gpu | Мико-то тут как ты. | ru | 0.11 | 0.50 | QuestionToViewer, PersonalQuestion | 0.03 |
| small-q5_1-ru | Микка, ты тут? Как ты? | ru | 0.06 | 0.50 | QuestionToViewer, PersonalQuestion | 3.28 |
| small-q5_1-ru-gpu | Микка, ты тут? Как ты? | ru | 0.06 | 0.50 | QuestionToViewer, PersonalQuestion | 0.06 |
| turbo-q5_0-ru-gpu | Мика, ты тут? Как ты? | ru | 0.00 | 1.00 | QuestionToViewer, PersonalQuestion | 0.14 |

### 16. «Найт Оул, опять ночная смена?» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | На этого лопетничная смена. | ru | 0.37 | 0.20 | Statement (ref QuestionToChat) | 0.35 |
| tiny-ru-gpu | На этого лопетничная смена. | ru | 0.37 | 0.20 | Statement (ref QuestionToChat) | 0.02 |
| base-ru | На эту ОЛО, опять начинает смена? | ru | 0.33 | 0.40 | QuestionToChat | 0.85 |
| base-ru-gpu | На эту ОЛО, опять начинает смена? | ru | 0.33 | 0.40 | QuestionToChat | 0.04 |
| small-q5_1-ru | Найт Вол, опять ночная смена. | ru | 0.07 | 0.80 | Statement (ref QuestionToChat) | 3.27 |
| small-q5_1-ru-gpu | Найт Вол, опять ночная смена. | ru | 0.07 | 0.80 | Statement (ref QuestionToChat) | 0.07 |
| turbo-q5_0-ru-gpu | Найт Волл, опять ночная смена | ru | 0.07 | 0.80 | Statement (ref QuestionToChat) | 0.14 |

### 17. «А вы откуда вообще, ребят?» (2.3 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | А вы откуда вообще ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.35 |
| tiny-ru-gpu | А вы откуда вообще ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.02 |
| base-ru | А вы откуда вообще ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.82 |
| base-ru-gpu | А вы откуда вообще ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.03 |
| small-q5_1-ru | А вы откуда вообще, ребят? | ru | 0.00 | 1.00 | QuestionToChat | 3.28 |
| small-q5_1-ru-gpu | А вы откуда вообще, ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.06 |
| turbo-q5_0-ru-gpu | А вы откуда вообще, ребят? | ru | 0.00 | 1.00 | QuestionToChat | 0.14 |

### 18. «Что делаете сегодня вечером?» (3.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Что делайте сегодня вечером? | ru | 0.04 | 1.00 | QuestionToChat (ref QuestionToChat, PersonalQuestion) | 0.36 |
| tiny-ru-gpu | Что делайте сегодня вечером? | ru | 0.04 | 1.00 | QuestionToChat (ref QuestionToChat, PersonalQuestion) | 0.02 |
| base-ru | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.82 |
| base-ru-gpu | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.03 |
| small-q5_1-ru | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 3.21 |
| small-q5_1-ru-gpu | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.06 |
| turbo-q5_0-ru-gpu | Что делаете сегодня вечером? | ru | 0.00 | 1.00 | QuestionToChat, PersonalQuestion | 0.14 |

### 19. «Пойду чайку налью, сейчас вернусь.» (3.1 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Пойдучи и куналью сейчас вернусь. | ru | 0.19 | 0.60 | Statement | 0.35 |
| tiny-ru-gpu | Пойдучи и куналью сейчас вернусь. | ru | 0.19 | 0.60 | Statement | 0.03 |
| base-ru | Пойду, чай икунулью, сейчас вернусь. | ru | 0.13 | 0.80 | Statement | 0.83 |
| base-ru-gpu | Пойду, чай икунулью, сейчас вернусь. | ru | 0.13 | 0.80 | Statement | 0.04 |
| small-q5_1-ru | Пойду чай икуналью и сейчас вернусь. | ru | 0.16 | 0.80 | Statement | 3.30 |
| small-q5_1-ru-gpu | Пойду чай икуналью и сейчас вернусь. | ru | 0.16 | 0.80 | Statement | 0.08 |
| turbo-q5_0-ru-gpu | Пойду чайку налью, сейчас вернусь | ru | 0.00 | 1.00 | Statement | 0.15 |

### 20. «Ну что, как вам мой новый микрофон?» (2.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Ну что, как у мой новый микрофон? | ru | 0.09 | 0.75 | QuestionToChat (ref QuestionToChat, OpinionRequest) | 0.34 |
| tiny-ru-gpu | Ну что, как у мой новый микрофон? | ru | 0.09 | 0.75 | QuestionToChat (ref QuestionToChat, OpinionRequest) | 0.02 |
| base-ru | Ну что, как мой новый микрофон? | ru | 0.12 | 0.75 | QuestionToChat (ref QuestionToChat, OpinionRequest) | 0.82 |
| base-ru-gpu | Ну что, как мой новый микрофон? | ru | 0.12 | 0.75 | QuestionToChat (ref QuestionToChat, OpinionRequest) | 0.04 |
| small-q5_1-ru | Ну что, как вам мой новый микрофон? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 3.27 |
| small-q5_1-ru-gpu | Ну что, как вам мой новый микрофон? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.07 |
| turbo-q5_0-ru-gpu | Ну что, как вам мой новый микрофон? | ru | 0.00 | 1.00 | QuestionToChat, OpinionRequest | 0.20 |

### 21. «Слушайте, а какие игры вы любите?» (3.0 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Слушайте, а какие игровы любите? | ru | 0.06 | 0.75 | QuestionToChat, GameplayQuestion | 0.36 |
| tiny-ru-gpu | Слушайте, а какие игровы любите? | ru | 0.06 | 0.75 | QuestionToChat, GameplayQuestion | 0.03 |
| base-ru | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.83 |
| base-ru-gpu | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.03 |
| small-q5_1-ru | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 3.25 |
| small-q5_1-ru-gpu | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.06 |
| turbo-q5_0-ru-gpu | Слушайте, а какие игры вы любите? | ru | 0.00 | 1.00 | QuestionToChat, GameplayQuestion | 0.20 |

### 22. «Стрим только начался, сейчас разогреемся.» (4.6 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Стрем только начаться, сейчас разогреемся. | ru | 0.08 | 0.80 | Statement | 0.36 |
| tiny-ru-gpu | Стрем только начаться, сейчас разогреемся. | ru | 0.08 | 0.80 | Statement | 0.03 |
| base-ru | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.85 |
| base-ru-gpu | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.04 |
| small-q5_1-ru | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 3.29 |
| small-q5_1-ru-gpu | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.07 |
| turbo-q5_0-ru-gpu | Стрим только начался, сейчас разогреемся. | ru | 0.00 | 1.00 | Statement | 0.18 |

### 23. «Эээ... ладно.» (2.9 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Ладно. | ru | 0.44 | 0.50 | Filler | 0.36 |
| tiny-ru-gpu | Ладно. | ru | 0.44 | 0.50 | Filler | 0.03 |
| base-ru | Ладно. | ru | 0.44 | 0.50 | Filler | 1.57 |
| base-ru-gpu | Ладно. | ru | 0.44 | 0.50 | Filler | 0.61 |
| small-q5_1-ru | Ладно | ru | 0.44 | 0.50 | Filler | 3.22 |
| small-q5_1-ru-gpu | Ладно | ru | 0.44 | 0.50 | Filler | 0.04 |
| turbo-q5_0-ru-gpu | Ладно. | ru | 0.44 | 0.50 | Filler | 0.17 |

### 24. «Hello chat, how are you doing today?» (3.8 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Добрый день! | ru | 0.97 | 0.00 | Statement (ref Greeting, QuestionToChat, PersonalQuestion) | 0.41 |
| tiny-ru-gpu | Добрый день! | ru | 0.97 | 0.00 | Statement (ref Greeting, QuestionToChat, PersonalQuestion) | 0.63 |
| base-ru | Hello chat, how are you doing today? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.82 |
| base-ru-gpu | Hello chat, how are you doing today? | ru | 0.00 | 1.00 | Greeting, QuestionToChat, PersonalQuestion | 0.03 |
| small-q5_1-ru | Привет, Шат! Как вы сегодня делаете это? | ru | 1.00 | 0.00 | Greeting, QuestionToChat, PersonalQuestion | 3.31 |
| small-q5_1-ru-gpu | Привет, Шат! Как вы сегодня делаете это? | ru | 1.00 | 0.00 | Greeting, QuestionToChat, PersonalQuestion | 0.07 |
| turbo-q5_0-ru-gpu | Привет, чад! Как дела сегодня? | ru | 0.91 | 0.00 | Greeting, QuestionToChat, PersonalQuestion | 0.17 |

### 25. «Хай, hello, how are you?» (4.7 s)

| Config | Transcript | Lang | CER | Recall | Acts | s |
|---|---|---|---:|---:|---|---:|
| tiny-ru | Хай! Алло, how are you? | ru | 0.24 | 0.80 | Greeting, QuestionToChat, PersonalQuestion | 0.35 |
| tiny-ru-gpu | Хай! Алло, how are you? | ru | 0.24 | 0.80 | Greeting, QuestionToChat, PersonalQuestion | 0.03 |
| base-ru | Хай! | ru | 0.86 | 0.20 | Greeting (ref Greeting, QuestionToChat, PersonalQuestion) | 0.88 |
| base-ru-gpu | Хай! | ru | 0.86 | 0.20 | Greeting (ref Greeting, QuestionToChat, PersonalQuestion) | 0.39 |
| small-q5_1-ru | Привет! | ru | 1.00 | 0.00 | Greeting (ref Greeting, QuestionToChat, PersonalQuestion) | 3.35 |
| small-q5_1-ru-gpu | Привет! | ru | 1.00 | 0.00 | Greeting (ref Greeting, QuestionToChat, PersonalQuestion) | 1.39 |
| turbo-q5_0-ru-gpu | Привет. Привет. Как ты? | ru | 0.90 | 0.00 | Greeting, QuestionToViewer, PersonalQuestion (ref Greeting, QuestionToChat, PersonalQuestion) | 1.04 |
