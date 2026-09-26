# Per-phrase semantic audit — final production STT

Phrase-by-phrase semantic review against the original marked text, performed by the coding agent; no correctness verdict is inferred from WER alone. Final configuration: turbo-q5_0 GPU beam, forced RU, production 500 ms zero context per side. This is replay of the recorded microphone corpus, not fresh capture.

| # | Original mark | Marked-slice transcript | Production-VAD transcript | Semantic finding |
|---:|---|---|---|---|
| 1 | Всем привет, парни! Как дела? Как настроение? Что сегодня делали? | Всем привет, парни! Как дела? Как настроение? Что сегодня делали? | Всем привет парни! Как дела? Как настроение? Что сегодня делали? | PASS: greeting, plural address and all three personal questions survive. |
| 2 | Привет, привет, дорогой друг. Как вы вообще? Расскажите, что думаете. | привет привет дорогой друг как вообще расскажите что думаете | Привет, привет, дорогой друг! Как вы вообще? Расскажите, что думаете? | PASS: greeting and invitation to talk survive. |
| 3 | Вы можете мне хоть что-то ответить? | Вы можете мне хоть что-то ответить? | Вы можете мне хоть что-то ответить? | PASS: request to answer survives; smaller models changed the requested action. |
| 4 | Так, секунду. | Так, секунду. | Так, секунду. | PASS: brief pause/filler survives; chat should normally remain silent. |
| 5 | А ты во что сегодня играл? | А ты во что сегодня играл? | А ты во что сегодня играл? | PASS: second-person question about games survives after contextual preparation. |
| 6 | Чат, во что сегодня поиграем? | Чат, во что сегодня поиграем? | Чат, во что сегодня поиграем? | PASS: question to chat about the next game survives. |
| 7 | Ребят, как вам звук сегодня, нормально слышно? | Ребят, как вам звук сегодня? Нормально слышно? | Ребята, как вам звук сегодня? Нормально слышно? | PASS: both audio-quality questions survive. |
| 8 | Жесть. Устал, наверное? | Жесть. Устал, наверное. | Жесть. Устал, наверное. | CONTENT PASS: tiredness follow-up survives, but the question mark is absent; downstream handling must not depend on punctuation alone. |
| 9 | Сегодня просто болтаем, никуда не торопимся. | Сегодня просто болтаем, никуда не торопимся. | сегодня просто болтаем никуда не торопимся | PASS: casual conversation and no-rush meaning survive. |
| 10 | Блин, опять всё зависло, капец. | Блин, опять все зависло. Капец. | Блин, опять все зависло. Капец. | PASS: freeze/frustration meaning and ending survive. |
| 11 | Если я сейчас проиграю, я удаляю игру. | Если я сейчас проиграю, я удаляю игру. | Если я сейчас проиграю, я удаляю игру. | PASS: conditional remains conditional, not a claim that the game was already lost. |
| 12 | Спасибо за фоллоу, очень приятно! | Спасибо за фоллоу, очень приятно. | Спасибо за фоллоу, очень приятно. | PASS: gratitude for a follow survives. |
| 13 | Завтра куплю новую видеокарту, обещаю. | Завтра куплю новую видеокарту Обещаю | Завтра куплю новую видеокарту. / Обещаю. | PASS: tomorrow/purchase/promise survive. VAD emits the promise as two segments separated by the real pause. |
| 14 | Короче, ну, это самое... | короче ну это самое | короче ну это самое | PASS: filler survives; no useful chat response required. |
| 15 | Мика, ты тут? Как ты? | Мика, ты тут? Как ты? | Мика, ты тут? Как ты? | PASS: Mika address, presence question and personal question survive. |
| 16 | Найт Оул, опять ночная смена? | Найт Волл, опять ночная смена | night wall опять ночная смена | PARTIAL: night-shift topic survives; proper nickname Night Owl is misrecognized as Night Wall. Do not claim reliable named address here. |
| 17 | А вы откуда вообще, ребят? | А вы откуда вообще, ребят? | А вы откуда вообще, ребят? | PASS: plural question about where viewers are from survives. |
| 18 | Что делаете сегодня вечером? | Что делаете сегодня вечером? | Что делаете сегодня вечером? | PASS: question about evening plans survives. |
| 19 | Пойду чайку налью, сейчас вернусь. | Пойду чайку налью, сейчас вернусь | Пойду чайку налью, сейчас вернусь. | PASS: leaving briefly to pour tea and returning survive. |
| 20 | Ну что, как вам мой новый микрофон? | Ну что, как вам мой новый микрофон? | Ну что, как вам мой новый микрофон? | PASS: request for an opinion about the new microphone survives. |
| 21 | Слушайте, а какие игры вы любите? | Слушайте, а какие игры вы любите? | Слушайте, а какие игры вы любите? | PASS: question about preferred games survives. |
| 22 | Стрим только начался, сейчас разогреемся. | Стрим только начался, сейчас разогреемся. | Стрим только начался, сейчас разогреемся. | PASS: stream-start/warm-up statement survives. |
| 23 | Эээ... ладно. | Ладно. | Эээ... ладно. | PASS: low-information filler remains low-information despite omitted hesitation in the marked slice. |
| 24 | Hello chat, how are you doing today? | Привет, чат! Как дела сегодня? | Hello chat, how are you doing today? | EN CONTROL: forced-RU marked slice translates the question; VAD preserves English. Excluded from RU semantic gate. |
| 25 | Хай, hello, how are you? | Здравствуйте! Здравствуйте! Здравствуйте! Как ты себя чувствуешь? | Hello, how are you? | MIXED CONTROL: marked slice invents/repeats greetings under forced RU; VAD preserves the English question. Excluded from RU semantic gate. |
