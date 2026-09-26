# Memory A/B raw generations

All attempts in generation order. Accepted = validator acceptance only. Nothing removed or replaced. A witnessed+relevant, B never witnessed, C witnessed+unrelated; paired reaction ids.

| # | Viewer | Context | Tier | Plan | Raw output | Accepted / reason | Published | ms |
|---:|---|---|---|---|---|---|---|---:|
| 1 | NightOwl | A witnessed relevant | Neutral | Callback(Question) | ты ещё не в курсе | accepted | LanguageModel: ты ещё не в курсе | 296 |
| 2 | NightOwl | A witnessed relevant | Neutral | Tease | Турнир-то опять на раз. Даже не надеюсь | accepted | LanguageModel: Турнир-то опять на раз. Даже не надеюсь | 446 |
| 3 | NightOwl | A witnessed relevant | Neutral | Tease | безумно уверен в проигранном финале | accepted | LanguageModel: безумно уверен в проигранном финале | 370 |
| 4 | NightOwl | A witnessed relevant | Neutral | Callback(Question) | а ты вчера как проиграл-то | unsupported historical claim | Fallback: без понятия | 297 |
| 5 | NightOwl | A witnessed relevant | Neutral | Callback(Question) | прошлый финал был неудачный | unsupported historical claim | Fallback: без понятия | 205 |
| 6 | NightOwl | A witnessed relevant | Neutral | Callback(Answer) | а вот и снова финал, как вчера | unsupported historical claim | Fallback: сложно сказать | 345 |
| 7 | NightOwl | A witnessed relevant | Neutral | Answer | не афиширую, но ставлю на тебя | accepted | LanguageModel: не афиширую, но ставлю на тебя | 289 |
| 8 | NightOwl | A witnessed relevant | Neutral | Callback(Tease) | уже в финале опять ахах | accepted | LanguageModel: уже в финале опять ахах | 330 |
| 9 | NightOwl | B no memory | Neutral | Question | а вот я и не знала что начнется софизм | accepted | LanguageModel: а вот я и не знала что начнется софизм | 340 |
| 10 | NightOwl | B no memory | Neutral | Tease | конечно получится. потерпишь | accepted | LanguageModel: конечно получится. потерпишь | 310 |
| 11 | NightOwl | B no memory | Neutral | Tease | я в чате не играл, а ты снова проигрываешь | accepted | LanguageModel: я в чате не играл, а ты снова проигрываешь | 385 |
| 12 | NightOwl | B no memory | Neutral | Question | снова попадаешь в финал, а до сих пор не победила? хах | too long for this viewer | Fallback: без понятия | 456 |
| 13 | NightOwl | B no memory | Neutral | Question | зачем спрашиваешь ты сам?  | accepted | LanguageModel: зачем спрашиваешь ты сам? | 238 |
| 14 | NightOwl | B no memory | Neutral | Answer | даже я бы не рискнул | accepted | LanguageModel: даже я бы не рискнул | 243 |
| 15 | NightOwl | B no memory | Neutral | Answer | давай не надеюсь | accepted | LanguageModel: давай не надеюсь | 176 |
| 16 | NightOwl | B no memory | Neutral | Tease | а вот и не получится | accepted | LanguageModel: а вот и не получится | 222 |
| 17 | NightOwl | C witnessed unrelated | Neutral | Question | давай на что-нибудь с лыжинами и снегом | accepted | LanguageModel: давай на что-нибудь с лыжинами и снегом | 293 |
| 18 | NightOwl | C witnessed unrelated | Neutral | Tease | какой смысл спрашивать у чата | accepted | LanguageModel: какой смысл спрашивать у чата | 280 |
| 19 | NightOwl | C witnessed unrelated | Neutral | Tease | давай уже без чата решай | accepted | LanguageModel: давай уже без чата решай | 289 |
| 20 | NightOwl | C witnessed unrelated | Neutral | Question | переключишься на что-то поумнее? | accepted | LanguageModel: переключишься на что-то поумнее? | 372 |
| 21 | NightOwl | C witnessed unrelated | Neutral | Question | а ты внизу копать будешь? | accepted | LanguageModel: а ты внизу копать будешь? | 248 |
| 22 | NightOwl | C witnessed unrelated | Neutral | Answer | наверное херню с клонами и бабами | accepted | LanguageModel: наверное херню с клонами и бабами | 318 |
| 23 | NightOwl | C witnessed unrelated | Neutral | Answer | запускай хз какую нафиг | accepted | LanguageModel: запускай хз какую нафиг | 233 |
| 24 | NightOwl | C witnessed unrelated | Neutral | Tease | давай как в последний раз | accepted | LanguageModel: давай как в последний раз | 213 |
| 25 | ZinaIvanovna | A witnessed relevant | Friendly | Callback(Answer) | Давайте посмотрим, как это развернется. Удачи вам сегодня! | accepted | LanguageModel: Давайте посмотрим, как это развернется. Удачи вам сегодня! | 465 |
| 26 | ZinaIvanovna | A witnessed relevant | Friendly | Callback(Question) | Вы же вчера в финале проиграли, верно? Вот и сейчас нервничаете... | unsupported historical claim | Fallback: а сам как думаешь | 381 |
| 27 | ZinaIvanovna | A witnessed relevant | Friendly | Callback(Answer) | В прошлый раз мы проиграли — надеюсь, сегодня удача изменит. | unsupported historical claim | Fallback: а сам как думаешь | 375 |
| 28 | ZinaIvanovna | A witnessed relevant | Friendly | Answer | Хочется верить, что всё получится — так давайте вместе за него поболеем.)) | accepted | LanguageModel: Хочется верить, что всё получится — так давайте вместе за него поболеем.)) | 420 |
| 29 | ZinaIvanovna | A witnessed relevant | Friendly | Answer | Какой-то напряжённый взгляд. Но уверена, у Вас всё получится! | accepted | LanguageModel: Какой-то напряжённый взгляд. Но уверена, у Вас всё получится! | 367 |
| 30 | ZinaIvanovna | A witnessed relevant | Friendly | Answer | Уверена, что всё получится — у вас такой упорный взгляд на победу! | accepted | LanguageModel: Уверена, что всё получится — у вас такой упорный взгляд на победу! | 385 |
| 31 | ZinaIvanovna | A witnessed relevant | Friendly | Question | Как вы готовились к финалу и с каким настроением идёте? | accepted | LanguageModel: Как вы готовились к финалу и с каким настроением идёте? | 361 |
| 32 | ZinaIvanovna | A witnessed relevant | Friendly | Callback(Question) | Прошлое финиширование было печальным, а теперь у Вас такой шанс ещё раз. Желаю удачи. | unsupported historical claim | Fallback: не знаю даже | 517 |
| 33 | ZinaIvanovna | B no memory | Friendly | Answer | У Вас прекрасный шанс! Желаю удачи, как мне кажется. | accepted | LanguageModel: У Вас прекрасный шанс! Желаю удачи, как мне кажется. | 397 |
| 34 | ZinaIvanovna | B no memory | Friendly | Question | А вы как всегда очень серьёзно относитесь к турнирам, милый. Но удачи всё равно желаю | accepted | LanguageModel: А вы как всегда очень серьёзно относитесь к турнирам, милый. Но удачи всё равно желаю | 374 |
| 35 | ZinaIvanovna | B no memory | Friendly | Answer | Как я понимаю, всё зависит от упорства и удачи. Но думаю — сможет! | accepted | LanguageModel: Как я понимаю, всё зависит от упорства и удачи. Но думаю — сможет! | 449 |
| 36 | ZinaIvanovna | B no memory | Friendly | Answer | Давайте, надеюсь, всё получится, как всегда интересно посмотреть. | accepted | LanguageModel: Давайте, надеюсь, всё получится, как всегда интересно посмотреть. | 374 |
| 37 | ZinaIvanovna | B no memory | Friendly | Answer | Думаю, у вас всё получится, если не будете паниковать. | accepted | LanguageModel: Думаю, у вас всё получится, если не будете паниковать. | 382 |
| 38 | ZinaIvanovna | B no memory | Friendly | Answer | Спасибо, что спрашиваете нас! Надеюсь, получится — а то как же без победы? | accepted | LanguageModel: Спасибо, что спрашиваете нас! Надеюсь, получится — а то как же без победы? | 439 |
| 39 | ZinaIvanovna | B no memory | Friendly | Question | У вас всё получится, особенно если будете беречь силы и не забывать есть. | accepted | LanguageModel: У вас всё получится, особенно если будете беречь силы и не забывать есть. | 346 |
| 40 | ZinaIvanovna | B no memory | Friendly | Question | Какой турнир это? И можно ли сейчас узнать противников? | accepted | LanguageModel: Какой турнир это? И можно ли сейчас узнать противников? | 332 |
| 41 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Ну что ж, если хотите компанию - я бы выбрала что-то с логикой или головоломками. | accepted | LanguageModel: Ну что ж, если хотите компанию - я бы выбрала что-то с логикой или головоломками. | 483 |
| 42 | ZinaIvanovna | C witnessed unrelated | Friendly | Question | А может, всё же лучше что-нибудь тихое, чтобы не утомлять глаз? | accepted | LanguageModel: А может, всё же лучше что-нибудь тихое, чтобы не утомлять глаз? | 337 |
| 43 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | У меня нет любимой игры, но с удовольствием посмотрела бы что-то весёлое и непринужденное. ) | accepted | LanguageModel: У меня нет любимой игры, но с удовольствием посмотрела бы что-то весёлое и непринужденное. ) | 496 |
| 44 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Что ж, пусть будет то, что всем интересно. Главное, чтобы не слишком сложно было.)) | accepted | LanguageModel: Что ж, пусть будет то, что всем интересно. Главное, чтобы не слишком сложно было.)) | 473 |
| 45 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Мне кажется, «Стелс» или что-то тихое и умиротворяющее подойдет. | accepted | LanguageModel: Мне кажется, «Стелс» или что-то тихое и умиротворяющее подойдет. | 451 |
| 46 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Мне кажется, что лучше выбрать что-то с хорошими графикой и звуком, чтобы было приятно смотреть. Нам же так веселее вместе!) | accepted | LanguageModel: Мне кажется, что лучше выбрать что-то с хорошими графикой и звуком, чтобы было приятно смотреть. Нам же так веселее вместе!) | 597 |
| 47 | ZinaIvanovna | C witnessed unrelated | Friendly | Question | А что, если мы все вместе посоветуемся, и он выберет? | accepted | LanguageModel: А что, если мы все вместе посоветуемся, и он выберет? | 761 |
| 48 | ZinaIvanovna | C witnessed unrelated | Friendly | Question | А может, пусть послушаем ваши рекомендации из собственного опыта?) | accepted | LanguageModel: А может, пусть послушаем ваши рекомендации из собственного опыта?) | 403 |
