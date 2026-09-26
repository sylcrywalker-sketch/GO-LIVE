# Memory A/B raw generations

All attempts in generation order. Accepted = validator acceptance only. Nothing removed or replaced. A witnessed+relevant, B never witnessed, C witnessed+unrelated; paired reaction ids.

| # | Viewer | Context | Tier | Plan | Raw output | Accepted / reason | Published | ms |
|---:|---|---|---|---|---|---|---|---:|
| 1 | NightOwl | A witnessed relevant | Neutral | Callback(Question) | всегда проигрываешь в финале? | accepted | LanguageModel: всегда проигрываешь в финале? | 341 |
| 2 | NightOwl | A witnessed relevant | Neutral | Tease | спасение в последней минуте | accepted | LanguageModel: спасение в последней минуте | 263 |
| 3 | NightOwl | A witnessed relevant | Neutral | Tease | еще раз? ну ты даёшь | accepted | LanguageModel: еще раз? ну ты даёшь | 265 |
| 4 | NightOwl | A witnessed relevant | Neutral | Callback(Question) | а вчерашний конец-то где? | accepted | LanguageModel: а вчерашний конец-то где? | 291 |
| 5 | NightOwl | A witnessed relevant | Neutral | Callback(Question) | всегда проигрываете? | accepted | LanguageModel: всегда проигрываете? | 201 |
| 6 | NightOwl | A witnessed relevant | Neutral | Callback(Answer) | ну как и обычно — не получится | accepted | LanguageModel: ну как и обычно — не получится | 246 |
| 7 | NightOwl | A witnessed relevant | Neutral | Answer | ещё один шанс на ошибку | accepted | LanguageModel: ещё один шанс на ошибку | 195 |
| 8 | NightOwl | A witnessed relevant | Neutral | Callback(Tease) | уже два финала подряд и оба проигрыша | accepted | LanguageModel: уже два финала подряд и оба проигрыша | 315 |
| 9 | NightOwl | B no memory | Neutral | Question | уже в финале и боишься? | accepted | LanguageModel: уже в финале и боишься? | 216 |
| 10 | NightOwl | B no memory | Neutral | Tease | спорим, что не получится | accepted | LanguageModel: спорим, что не получится | 263 |
| 11 | NightOwl | B no memory | Neutral | Tease | опять турнир? а не выйти на улицу? | accepted | LanguageModel: опять турнир? а не выйти на улицу? | 299 |
| 12 | NightOwl | B no memory | Neutral | Question | в третий раз? а что поменялось? | accepted | LanguageModel: в третий раз? а что поменялось? | 262 |
| 13 | NightOwl | B no memory | Neutral | Question | надо же, и опять к финишу | accepted | LanguageModel: надо же, и опять к финишу | 238 |
| 14 | NightOwl | B no memory | Neutral | Answer | ещё бы в третий раз попал | accepted | LanguageModel: ещё бы в третий раз попал | 239 |
| 15 | NightOwl | B no memory | Neutral | Answer | шанс пятьдесят на пятьдесят | accepted | LanguageModel: шанс пятьдесят на пятьдесят | 213 |
| 16 | NightOwl | B no memory | Neutral | Tease | ты что, в последний раз проиграл на старте | accepted | LanguageModel: ты что, в последний раз проиграл на старте | 285 |
| 17 | NightOwl | C witnessed unrelated | Neutral | Question | а я уже тут скучаю по прошлому сеансу | unsupported historical claim | Fallback: а сам как думаешь | 349 |
| 18 | NightOwl | C witnessed unrelated | Neutral | Tease | давай лучше выберем что-нибудь с анимациями на миксе | accepted | LanguageModel: давай лучше выберем что-нибудь с анимациями на миксе | 372 |
| 19 | NightOwl | C witnessed unrelated | Neutral | Tease | а ты уже нашёл что-то понравившееся? | accepted | LanguageModel: а ты уже нашёл что-то понравившееся? | 320 |
| 20 | NightOwl | C witnessed unrelated | Neutral | Question | я бы выбрал то что не ломается | accepted | LanguageModel: я бы выбрал то что не ломается | 283 |
| 21 | NightOwl | C witnessed unrelated | Neutral | Question | Что на сегодня уже прошли? | unsupported historical claim | Fallback: без понятия | 264 |
| 22 | NightOwl | C witnessed unrelated | Neutral | Answer | еще эти аркады с приставки | accepted | LanguageModel: еще эти аркады с приставки | 247 |
| 23 | NightOwl | C witnessed unrelated | Neutral | Answer | ну так нечего тупой вопрос задавать | accepted | LanguageModel: ну так нечего тупой вопрос задавать | 190 |
| 24 | NightOwl | C witnessed unrelated | Neutral | Tease | да ну и хрен с ними, запускай что угодно | accepted | LanguageModel: да ну и хрен с ними, запускай что угодно | 325 |
| 25 | ZinaIvanovna | A witnessed relevant | Friendly | Callback(Answer) | Какие волнения! Вчерашний финал был сложным – надеюсь, сегодня удача на вашей стороне. | accepted | LanguageModel: Какие волнения! Вчерашний финал был сложным – надеюсь, сегодня удача на вашей стороне. | 522 |
| 26 | ZinaIvanovna | A witnessed relevant | Friendly | Callback(Question) | Вчерашний финал был очень напряжённый, давайте надеяться на удачу! | accepted | LanguageModel: Вчерашний финал был очень напряжённый, давайте надеяться на удачу! | 370 |
| 27 | ZinaIvanovna | A witnessed relevant | Friendly | Callback(Answer) | Как же надеюсь, что сегодня всё получится! Вы так хорошо играли вчера...)) | accepted | LanguageModel: Как же надеюсь, что сегодня всё получится! Вы так хорошо играли вчера...)) | 469 |
| 28 | ZinaIvanovna | A witnessed relevant | Friendly | Answer | Какой финал, дорогой! Удачи вам в этом соревновании!) | accepted | LanguageModel: Какой финал, дорогой! Удачи вам в этом соревновании!) | 378 |
| 29 | ZinaIvanovna | A witnessed relevant | Friendly | Answer | Вам нужно просто поверить в удачу и не паниковать. | accepted | LanguageModel: Вам нужно просто поверить в удачу и не паниковать. | 328 |
| 30 | ZinaIvanovna | A witnessed relevant | Friendly | Answer | Кажется, всегда хочется верить в удачу и сегодня не исключение. | accepted | LanguageModel: Кажется, всегда хочется верить в удачу и сегодня не исключение. | 365 |
| 31 | ZinaIvanovna | A witnessed relevant | Friendly | Question | Вроде и подготовка была хорошая, но ведь удача тоже нужна в таких моментах. | accepted | LanguageModel: Вроде и подготовка была хорошая, но ведь удача тоже нужна в таких моментах. | 374 |
| 32 | ZinaIvanovna | A witnessed relevant | Friendly | Callback(Question) | Вчера было так грустно, а сейчас снова финал — надеюсь на удачу! | accepted | LanguageModel: Вчера было так грустно, а сейчас снова финал — надеюсь на удачу! | 459 |
| 33 | ZinaIvanovna | B no memory | Friendly | Answer | Вам повезло, на этот раз — давайте, только не переутомляйтесь! | accepted | LanguageModel: Вам повезло, на этот раз — давайте, только не переутомляйтесь! | 387 |
| 34 | ZinaIvanovna | B no memory | Friendly | Question | А на что, собственно, играете? В этот раз, я имею в виду. | accepted | LanguageModel: А на что, собственно, играете? В этот раз, я имею в виду. | 342 |
| 35 | ZinaIvanovna | B no memory | Friendly | Answer | Какая радость, что ты снова здесь, в финале! Надеюсь, всё получится — и дай бог здоровья! | accepted | LanguageModel: Какая радость, что ты снова здесь, в финале! Надеюсь, всё получится — и дай бог здоровья! | 469 |
| 36 | ZinaIvanovna | B no memory | Friendly | Answer | Как же хочется, чтоб на этот раз получилось!)} | markup or prompt leakage | Fallback: не знаю даже | 320 |
| 37 | ZinaIvanovna | B no memory | Friendly | Answer | Может, вы просто везенье как-то к вам приклеилось, милый! | accepted | LanguageModel: Может, вы просто везенье как-то к вам приклеилось, милый! | 407 |
| 38 | ZinaIvanovna | B no memory | Friendly | Answer | Давайте, у Вас такой талант — просто повезёт, если получится!)) | accepted | LanguageModel: Давайте, у Вас такой талант — просто повезёт, если получится!)) | 388 |
| 39 | ZinaIvanovna | B no memory | Friendly | Question | У Вас уже есть какой-то план? Или просто надежда? | accepted | LanguageModel: У Вас уже есть какой-то план? Или просто надежда? | 298 |
| 40 | ZinaIvanovna | B no memory | Friendly | Question | Сынок, у Вас же все получилось уже не раз? Верьте в свои силы. | accepted | LanguageModel: Сынок, у Вас же все получилось уже не раз? Верьте в свои силы. | 435 |
| 41 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Конечно! Мне нравятся квесты с интересными загадками – они пробуждают воображение. | accepted | LanguageModel: Конечно! Мне нравятся квесты с интересными загадками – они пробуждают воображение. | 438 |
| 42 | ZinaIvanovna | C witnessed unrelated | Friendly | Question | Может быть, сыграем в что-то расслабляющее и не очень сложное? | accepted | LanguageModel: Может быть, сыграем в что-то расслабляющее и не очень сложное? | 328 |
| 43 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Как же выбирать-то, молодой человек? Давайте что-нибудь тихое и размеренное, на настроение. | accepted | LanguageModel: Как же выбирать-то, молодой человек? Давайте что-нибудь тихое и размеренное, на настроение. | 453 |
| 44 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Классические приключения с интересным сюжетом мне нравятся больше всех.)) | accepted | LanguageModel: Классические приключения с интересным сюжетом мне нравятся больше всех.)) | 411 |
| 45 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Ну что ж, если бы я знала — поиграла бы в шахматы или «Что? Где? Когда?». | accepted | LanguageModel: Ну что ж, если бы я знала — поиграла бы в шахматы или «Что? Где? Когда?». | 485 |
| 46 | ZinaIvanovna | C witnessed unrelated | Friendly | Answer | Мне нравятся стратегии с управлением ресурсами и дипломатией)) | accepted | LanguageModel: Мне нравятся стратегии с управлением ресурсами и дипломатией)) | 361 |
| 47 | ZinaIvanovna | C witnessed unrelated | Friendly | Question | Вопрос, может быть, лучше выбрать что-нибудь поуютнее и спокойнее? | accepted | LanguageModel: Вопрос, может быть, лучше выбрать что-нибудь поуютнее и спокойнее? | 394 |
| 48 | ZinaIvanovna | C witnessed unrelated | Friendly | Question | Наверное, лучше выбрать что-нибудь знакомое всем? Сынок, у вас есть фавориты? | accepted | LanguageModel: Наверное, лучше выбрать что-нибудь знакомое всем? Сынок, у вас есть фавориты? | 431 |
