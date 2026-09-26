# Social quality pass — manual semantic review

Developer annotation of every retained generation, not a human panel or a calibrated classifier. No line was removed, retried or replaced. Each run is a complete, unfiltered execution of the same fixed matrix; runs 1–3 are intermediate code states and are reported, not hidden. Validator acceptance establishes only the implemented checks.

## Rubric

A row is counted when the **published model line** (validator-accepted) contains a clear defect of one of these kinds. Rejected lines that fell back are counted separately as *caught*.

| Code | Defect |
|---|---|
| a | Invented fact or detail about the stream/streamer: hardware, settings, problems, causes, titles, counts, times, statements not made |
| b | Unsupported history or recurrence: "again", "always", "not the first time", counts of earlier occurrences, habits of the streamer |
| c | Speaker-role confusion: the viewer speaks as the streamer, owns the streamer's equipment or outcome, or quotes something nobody said |
| d | Relationship-incompatible claim: closeness or familiarity the game state does not support |
| e | Incoherent line or non sequitur that misses the moment |

This rubric is **stricter** than the one used for the original audit's list of 27. To compare like with like, the original 140 generations were re-annotated with it (below). Forced audition cells that the selector would normally suppress (filler) are still counted.

## Summary

| | Before (original audit) | V2 run 1 | V2 run 2 | V2 run 3 | V2 run 4 (final code) |
|---|---:|---:|---:|---:|---:|
| a invented facts/details | 21 | 11 | 7 | 4 | **1** |
| b unsupported history/recurrence | 5 | 3 | 3 | 5 | **5** |
| c speaker role | 3 | 1 | 1 | 0 | **0** |
| d relationship-incompatible | 3 | 1 | 0 | 0 | **0** |
| **grounding/role/relationship (a–d)** | **32** | 16 | 11 | 9 | **6** |
| e incoherent / non sequitur | 9 | 7 | 5 | 9 | **9** |
| **all accepted defects** | **41** | 23 | 16 | 18 | **15** |
| caught by validator (rejected → fallback) | 5 | 9 | 7 | 3 | 8 |

Runs 2–4 are within sampling noise of each other for the total (15–18). The structural categories fell steadily; incoherence (e) did not improve and is a limitation of the current 8B Q4 model at temperature 0.85, not of grounding.

## Before — original audit re-annotated

The original review's 26 accepted rows (row 130 was rejected) keep their categories: 3 e, 4 c, 9 e, 11 a, 20 a, 22 a, 23 a, 26 c, 33 a, 44 c, 50 e, 51 b, 53 e, 59 a, 60 a, 70 a, 71 a, 80 a, 83 a, 90 a, 93 a, 103 e, 123 a, 124 a, 136 a, 140 a. The stricter rubric adds 15 accepted rows from the same raw table ([original raw table](../all-raw.md)):

| Row | Code | Line |
|---:|---|---|
| 16 | b | `снова на ту же лажу?` |
| 18 | a | `Мне нравится идея с *Random*` (no option named) |
| 21 | e | `ты что блин на каком дрочике?` |
| 32 | b | `Опять задержка или так?` |
| 57 | e | `успокоился бы` during continuing silence |
| 61 | e | `пока не скажут как поиграть в покер` |
| 65 | a | invented title `«Эджел Ситти»` |
| 66 | b | `давай я снова уточню` |
| 67 | a | garbled title `кардданс` |
| 72 | d | `😍` at neutral relationship |
| 75 | b | `Как я всегда говорю` |
| 82 | d | wary PixelFox `влюбилась 😍` |
| 92 | d | regular PixelFox `Вот и зашла по случайности` |
| 109 | e | forced food answer to tournament preparation |
| 132 | a | invented models `GTX 3060 Ti или RTX 4070` |

## V2 run 1 (first planner version)

| Row | Code | Line |
|---:|---|---|
| 1 | e | `сделай карту в следующий раз` |
| 7 | e | `ты опять в ботах` |
| 20 | a | `…лучше запустить что-то на DirectX 12 Ultimate.` |
| 41 | a | `даже динамик отвалил? лох` (the microphone, not a speaker) |
| 42 | b | `Майкрофон опять отваливается?…` |
| 43 | a | `кодек сжатия или оптоволокно на конец кабеля забило` |
| 45 | b | `…Это опять он снова отвалился?` |
| 61 | b | `снова это же самое` |
| 62 | a | invented titles `«Ментальный босс» или «Стейнкёрс»` |
| 63 | a | `хз, на том же ноуте и так зависло` |
| 65 | a | invented title `«Портрета»` |
| 69 | e | `…крашбоксов на хабби…` |
| 73 | a | `хз откуда ты узнал что у меня ноутбук` |
| 78 | e | `Спасибо за ответ! У меня тоже хорошее настроение сейчас.` |
| 79 | e | `ты в депрессии? или просто так?` (to thanks) |
| 80 | a | `…стоит настроить звук немного выше.` |
| 90 | a | `…Настройки по умолчанию всё ещё странно работают.` |
| 92 | d | regular PixelFox `Даже не знала что есть такой стрим` |
| 93 | a | `…и как же так с разрешением?` |
| 114 | c | `eat all the snacks first u said bro` |
| 118 | e | Jonas forces his biography instead of answering |
| 119 | e | `кто у нас выиграл? мне бы выжить за аренду…` |
| 125 | a | `новая видеокарта` — caused by a wrong plan fact ("you have not seen any purchase") given to a promise witness; fixed |

Run 1 also exposed that the original Stage E rule (heard facts must be attributed with "ты говорил") rejected 6 of 9 planned memory callbacks that were exactly right (`а ты вчера как проиграл-то`, `а вот и снова финал, как вчера`). See *Changes after each run*.

## V2 run 2

| Row | Code | Line |
|---:|---|---|
| 5 | b | `…Вы же всегда все проверяете.` |
| 9 | a | `наверняка на это был талон или скидка` |
| 29 | a | `а ты на чем так отстаешь что не выиграл?` (the streamer won) |
| 31 | b | `опять припал к микрофону` |
| 33 | e | `файлы в консоли тоже можно сортировать` |
| 43 | a | `кодек не вытягивает` (the microphone was unplugged) |
| 47 | b | `опять с микрофоном... :/` |
| 49 | e | `…заплатишь за это в виде шума и раздраженных соседей` |
| 50 | a | `Проблема не в мике — скорее всего виноват выход или сама колонка.` |
| 61 | e | `Что ты уже сожрал?` |
| 70 | a | invented title `«Рогейн»` despite the genre-only instruction |
| 79 | e | `на халяву не орешь, если ты бот?…` |
| 82 | c | `Ты что, смотришь стрим только для меня?` |
| 88 | a | `…играть в игры на стриме так поздно.` (time of day unknown) |
| 90 | a | `…настройка FPS и графики забивает мне глаза.` |
| 111 | e | `научиться отращивать волосы` |

Caught: `Прошло уже три минуты без слов...` was rejected by the historical guard although it was grounded (a false positive on the verb "прошло"); fixed before run 3.

## V2 run 3

| Row | Code | Line |
|---:|---|---|
| 2 | e | `Странно, я же думал что у тебя все на ладан дымит` |
| 3 | e | `нафиг эта фидбэк система в игре то же самое` |
| 21 | e | `ты ещё не поняла что это` |
| 30 | a | `…статистический выброс, а в среднем у них процент побед ниже.` |
| 31 | e | `все умами занимается` |
| 33 | b | `снова паузы по 10 секунд?…` |
| 49 | e | `даже подушки в натугах не плачут как ты` |
| 50 | b | `Это уже третья неисправность микрофона за неделю, да?` |
| 51 | a | `уже перекусил и все равно` |
| 56 | e | `наверно всё лапки на блоке` |
| 59 | e | `…загружаться с лысой диеты` |
| 60 | b | `Уже опять пошёл в этот режим «невидимки»?` |
| 61 | b | `Уже давно не менял игры?` |
| 69 | b | `снова те же опционы?…` |
| 70 | e | `Для рисования лучше попробуйте…` (answer to which game to play) |
| 83 | a | `тут всегда так же лагает?` |
| 100 | a | `…особенно если любишь эксперименты с настройками.` |
| 127 | e | gentle Mika: `сперва бы сдохла а потом уже думала` |

## V2 run 4 — final code

| Row | Code | Line |
|---:|---|---|
| 32 | e | `Ну у вас же как-то приятно говорит, вроде) Не замечаешь ли ты что-нибудь странное?` |
| 33 | e | `второе` |
| 39 | e | `всё, прикинь, уже «аг» в шоке` |
| 60 | b | `Уже не в первый раз такой молчанием занимается...` |
| 61 | e | `вам что, все так и не нравится?` |
| 65 | b | `Мне очень нравится, когда вы играете в стратегии…` |
| 80 | b | `Спасибо, что обратили внимание на мои советы — хотя бы сейчас!` |
| 87 | b | `тысяча раз уже спрашивал` |
| 90 | a | `…это не стрим, а всего лишь тест.` |
| 93 | e | `дай я проверю лаг на ваттметре, пошло же?` |
| 97 | b | `да и вот уже третий подряд «)` |
| 103 | e | `установить камеры на баланс` |
| 119 | e | forced food answer to tournament preparation |
| 131 | e | `ну на ней качутся скучающие геймеры` |
| 133 | e | `…дешевая рюкзачная схема на глаза попалась…` |

Caught in run 4: `опять заговаривается…` and `только не фпс бои опять` (unsupported recurrence), `…проиграешь за полчаса` and `AMD R9 380` (ungrounded specifics), two profanity, one money and one language rejection.

### Especially unacceptable categories — final run 4

| Category | Accepted | Caught |
|---|---:|---:|
| Invented purchases / hardware models | 0 | 1 (`R9 380`) |
| Invented elapsed / remaining time | 0 | 1 (`полчаса`) |
| Impossible relationship claims | 0 | 0 |
| Firsthand claims of having seen unseen history | 0 | 0 |
| Softer unsupported history (counts, "not the first time", habits) | 5 | 2 |
| Wrong speaker identity | 0 | 0 |

The softer history presumptions without `опять/снова` (`не в первый раз`, `тысяча раз`, `третий подряд`, `когда вы играете в стратегии`) remain the one recurring grounding category. They are documented as accepted remaining failures, not solved.

## Changes after each run (all runs retained)

- After run 1: heard facts may be recalled without an attribution word but never claimed as *seen*; a planned callback about the moment's own subject may omit the subject word (memory and promise); explicit "no picture/sound/PC/settings problem has been reported" fact for non-setup moments; "don't imply before" rule; genre-not-title for game-choice answers; "they are thanking you by name; what for is not said"; "it happened to them, not to you" for first-person reports; the wrong purchase fact is now given only to viewers with no promise knowledge; brand names allowed only in a hardware moment.
- After run 2: the microphone fact states the actual cause (unplugged) instead of "why is unknown"; the historical guard matches `прошлый` forms, not the verb `прошло`.
- After run 3: plan-grounded recurrence check — `опять/снова` needs the streamer's words, visible chat or a planned callback (`попробуй снова` is a suggestion and passes).

## Relationship A/B

Same viewer, same event, four controlled states (Wary −50 / Neutral 0 / Friendly 40 / Loyal 80 with 8 visits and 5 acknowledgements). Selection and planning are deterministic over 400 fixed seeds and identical in every run ([selection data](run4/relationship-ab-selection.json)). Generation uses the same five reaction ids per state.

| Viewer / moment | Tier | Reaction rate | Mean delay | Planned actions |
|---|---|---:|---:|---|
| PixelFox, direct question | Wary | 0.903 | 4.64 s | Answer 255, Question 106 (29% ask back) |
| | Neutral | 0.968 | 3.92 s | Answer 236, Question 151 (39%) |
| | Friendly | 0.980 | 3.54 s | Answer 222, Question 170 (43%) |
| | Loyal | 0.988 | 3.16 s | Answer 226, Question 169 (43%) |
| PixelFox, failure report | Wary | 0.190 | 5.64 s | React 61, Question 15, **Concern 0** |
| | Neutral | 0.210 | 4.99 s | React 44, Question 22, Concern 18 |
| | Loyal | 0.223 | 4.57 s | React 41, Question 27, Concern 21 |
| NightOwl, direct question | Wary | 0.883 | 3.70 s | Tease 181, Answer 156, Question 16 |
| | Neutral | 0.968 | 3.14 s | Answer 224, Tease 125, Question 38 |
| | Loyal | 0.985 | 2.53 s | Answer 251, Tease 80, Question 63 |

Callback willingness: PixelFox 0.175 / 0.35 / 0.44 / 0.56, NightOwl 0.275 / 0.55 / 0.69 / 0.75 (cap).

Generated text (run 4, [raw](run4/relationship-ab.md)): wary PixelFox is noncommittal or cool (`Наверное, не хуже обычного, а так скучно как всегда)`, `Давно не натыкала на этот канал`); neutral/friendly lines are positive and friendly ones ask back (`Вот уже давно не заходила — как дела?`); loyal lines are warm (`Да! Как всегда круто начинается, да?)`). Wary NightOwl is curt (`зачем спрашиваешь меня`), loyal NightOwl turns the question back (`да ладно, а ты как думаешь?`); his failure teases go from sharp (`да ну что ты за геймер`) to warm (`зато покупать научишься`). No state produced affection above its tier; the old "wary PixelFox is in love" defect did not recur in 4 × 80 A/B generations or 4 × 140 matrix generations.

Friendly and Loyal text is not reliably distinguishable from each other; the deterministic behaviour (delay, callback willingness, concern share) differs more than the wording. A/B lines still contain invented history (`Второй раз за сегодняшний стрим`, `я же говорила что лучше не продавать`, `первый раз за месяц … «Хоббит»`) — the same residual category as the matrix.

## Memory A/B

A: witnessed the reported final loss yesterday, current moment about the final. B: same moment, never witnessed. C: witnessed, unrelated moment (which game to play). NightOwl and ZinaIvanovna, 8 paired reaction ids each.

| Condition | Callback candidates | Planned callbacks (deterministic) | Run 1 published callbacks | Run 4 published callbacks |
|---|---:|---:|---:|---:|
| A | 16/16 | 9/16 | 3/9 model, 6/9 rejected by the old attribution rule | **9/9 model** |
| B | 0/16 | 0 | — | — |
| C | 0/16 | 0 | — | — |

Run 4 callbacks: `вчера же проиграли`, `снова проиграли в финале?`, `вчера ты проиграл не на гениальность`, `неужели опять промазала`, `давай не повторяйся уже` (NightOwl); `Как жаль, что так получилось вчера... Надеюсь, это только случайность.`, `Вчера было так жалко, а сегодня новый шанс. Удачи!`, `В прошлый раз все так же напряженно выглядело...` (Zina) — 8 of 9 specific and relevant, one vague (`Какая у вас ответственность за такие события, да?`). The original audit had no convincing callback in ten witness cells.

B never references the specific loss, but NightOwl invents counts (`в третий раз за вечер`, `это в третий раз за неделю`) from the streamer's own "снова". C never mentions the final. Callbacks remain bounded: 9 of 16 relevant opportunities, per-fact cooldown/lifetime caps, and one published callback per viewer per 600 stream seconds.

## Other measures (final run 4)

- Assistant-phrase proxy 0/140; manual advice-desk tone: `Я бы посоветовал изучить гайд…` (120) — Sovetnik's authored habit, not counted as a defect.
- Exact duplicates 0/140; no same-context pair above 0.5 word Jaccard. The old `нафиг спасибо` collision did not recur.
- Gendered grammar for the viewer or the streamer is still inconsistent (`ты ещё не поняла`, `моя милая`); it is outside the rubric and unchanged.
