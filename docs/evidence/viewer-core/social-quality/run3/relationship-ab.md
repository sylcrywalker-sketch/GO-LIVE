# Relationship A/B raw generations

All attempts in generation order. Accepted = validator acceptance only. Nothing removed or replaced. Same viewer and event per block; reaction ids are paired across states.

| # | Viewer | Context | Tier | Plan | Raw output | Accepted / reason | Published | ms |
|---:|---|---|---|---|---|---|---|---:|
| 1 | PixelFox | direct question / Wary | Wary | Answer | Ну ты как? Я тут минут тридцать намучилась с настроением | accepted | LanguageModel: Ну ты как? Я тут минут тридцать намучилась с настроением | 398 |
| 2 | PixelFox | direct question / Wary | Wary | Answer | Действительно странный стрим. А зачем вообще? | accepted | LanguageModel: Действительно странный стрим. А зачем вообще? | 309 |
| 3 | PixelFox | direct question / Wary | Wary | Answer | Сегодняшний — как обычно. Интересно, что будет дальше. | accepted | LanguageModel: Сегодняшний — как обычно. Интересно, что будет дальше. | 341 |
| 4 | PixelFox | direct question / Wary | Wary | Answer | Хм, сегодняшний стрим как обычно — странный и неинтересный. | accepted | LanguageModel: Хм, сегодняшний стрим как обычно — странный и неинтересный. | 763 |
| 5 | PixelFox | direct question / Wary | Wary | Question | Давай я задам вопрос о том, что ты делаешь? 🎮 | accepted | LanguageModel: Давай я задам вопрос о том, что ты делаешь? 🎮 | 415 |
| 6 | PixelFox | direct question / Neutral | Neutral | Answer | Давно не заходила, как всегда круто смотрится! | accepted | LanguageModel: Давно не заходила, как всегда круто смотрится! | 420 |
| 7 | PixelFox | direct question / Neutral | Neutral | Question | Да, тут! А ты в каком режиме играешь? | accepted | LanguageModel: Да, тут! А ты в каком режиме играешь? | 317 |
| 8 | PixelFox | direct question / Neutral | Neutral | Answer | Кхм, ага, я тут давно уже! Нравлюсь сегодняшний процесс? | accepted | LanguageModel: Кхм, ага, я тут давно уже! Нравлюсь сегодняшний процесс? | 386 |
| 9 | PixelFox | direct question / Neutral | Neutral | Answer | Ой, да! Не думала, что ты ещё так же круто играешь 😄 | accepted | LanguageModel: Ой, да! Не думала, что ты ещё так же круто играешь 😄 | 377 |
| 10 | PixelFox | direct question / Neutral | Neutral | Question | Стрим какой-то волшебный выглядит! Как игра называется? | accepted | LanguageModel: Стрим какой-то волшебный выглядит! Как игра называется? | 357 |
| 11 | PixelFox | direct question / Friendly | Friendly | Answer | Да, вижу! Спасибо что спросили, очень приятно тут быть) | accepted | LanguageModel: Да, вижу! Спасибо что спросили, очень приятно тут быть) | 389 |
| 12 | PixelFox | direct question / Friendly | Friendly | Question | Давай я попробую, мне кажется всё круто!)} | markup or prompt leakage | Fallback: чего | 335 |
| 13 | PixelFox | direct question / Friendly | Friendly | Question | Дааа, как раз зашла! Нравится твой стиль игры сегодня 👀 | accepted | LanguageModel: Дааа, как раз зашла! Нравится твой стиль игры сегодня 👀 | 407 |
| 14 | PixelFox | direct question / Friendly | Friendly | Answer | Ух ты, а я даже не подумала зайти! Но тут оказалась случайно и радуюсь | accepted | LanguageModel: Ух ты, а я даже не подумала зайти! Но тут оказалась случайно и радуюсь | 409 |
| 15 | PixelFox | direct question / Friendly | Friendly | Question | даа, только сейчас заметил! Стрем кажется супер классным 👍 | accepted | LanguageModel: даа, только сейчас заметил! Стрем кажется супер классным 👍 | 359 |
| 16 | PixelFox | direct question / Loyal | Loyal | Answer | да, я тут уже минут пять! Похоже на что-то из «Зелёных зондов»? | accepted | LanguageModel: да, я тут уже минут пять! Похоже на что-то из «Зелёных зондов»? | 498 |
| 17 | PixelFox | direct question / Loyal | Loyal | Question | Да! Вот уже смотрю второй стрим в этом формате  | accepted | LanguageModel: Да! Вот уже смотрю второй стрим в этом формате | 329 |
| 18 | PixelFox | direct question / Loyal | Loyal | Question | Какой у тебя сегодня настроение на стриме? 😊 | accepted | LanguageModel: Какой у тебя сегодня настроение на стриме? 😊 | 332 |
| 19 | PixelFox | direct question / Loyal | Loyal | Answer | даааа, как раз зашла! тут только начал? | accepted | LanguageModel: даааа, как раз зашла! тут только начал? | 296 |
| 20 | PixelFox | direct question / Loyal | Loyal | Question | Да, слышно как всегда! Мне тут *замечательно* | accepted | LanguageModel: Да, слышно как всегда! Мне тут *замечательно* | 334 |
| 21 | PixelFox | failure report / Wary | Wary | React | Случайно на аукционе или в лавке так? | accepted | LanguageModel: Случайно на аукционе или в лавке так? | 742 |
| 22 | PixelFox | failure report / Wary | Wary | React | ну что ж, хоть не от стыда замерз. | accepted | LanguageModel: ну что ж, хоть не от стыда замерз. | 317 |
| 23 | PixelFox | failure report / Wary | Wary | React | просто изумительно, что еще не умер от смеха | accepted | LanguageModel: просто изумительно, что еще не умер от смеха | 336 |
| 24 | PixelFox | failure report / Wary | Wary | React | Давайте попробуем еще раз, а то я уже нервничаю | accepted | LanguageModel: Давайте попробуем еще раз, а то я уже нервничаю | 381 |
| 25 | PixelFox | failure report / Wary | Wary | React | Возможно, стоит проверить продажи перед покупкой? 😂 | accepted | LanguageModel: Возможно, стоит проверить продажи перед покупкой? 😂 | 767 |
| 26 | PixelFox | failure report / Neutral | Neutral | Question | Это же обычное дело, да? К счастью, что на стэке еще было | accepted | LanguageModel: Это же обычное дело, да? К счастью, что на стэке еще было | 426 |
| 27 | PixelFox | failure report / Neutral | Neutral | React | Спасение утопленников это сложно? | accepted | LanguageModel: Спасение утопленников это сложно? | 304 |
| 28 | PixelFox | failure report / Neutral | Neutral | React | Опять по жизни, а? Ну что, перекуемся ещё раз? | accepted | LanguageModel: Опять по жизни, а? Ну что, перекуемся ещё раз? | 368 |
| 29 | PixelFox | failure report / Neutral | Neutral | Question | Тут же пошло что-то не так с продажей, а? | accepted | LanguageModel: Тут же пошло что-то не так с продажей, а? | 311 |
| 30 | PixelFox | failure report / Neutral | Neutral | React | ужас, да? вот почему я не играю в рпг! | accepted | LanguageModel: ужас, да? вот почему я не играю в рпг! | 344 |
| 31 | PixelFox | failure report / Friendly | Friendly | Question | Ааа как так вот? На что же этот предмет был нужен в игре-то? | accepted | LanguageModel: Ааа как так вот? На что же этот предмет был нужен в игре-то? | 404 |
| 32 | PixelFox | failure report / Friendly | Friendly | React | не знаю, как это твой второй день подряд с этим бонусом) | accepted | LanguageModel: не знаю, как это твой второй день подряд с этим бонусом) | 345 |
| 33 | PixelFox | failure report / Friendly | Friendly | React | Жуть как невезёт в этих играх... | accepted | LanguageModel: Жуть как невезёт в этих играх... | 291 |
| 34 | PixelFox | failure report / Friendly | Friendly | Question | Уже второй раз за день на это попадаешь? | accepted | LanguageModel: Уже второй раз за день на это попадаешь? | 283 |
| 35 | PixelFox | failure report / Friendly | Friendly | React | ну не везёт же совсем... но хотя бы интереснее получается от этого! | accepted | LanguageModel: ну не везёт же совсем... но хотя бы интереснее получается от этого! | 361 |
| 36 | PixelFox | failure report / Loyal | Loyal | Question | а что там вообще было за предмет-то, в смысле зачем он? | accepted | LanguageModel: а что там вообще было за предмет-то, в смысле зачем он? | 354 |
| 37 | PixelFox | failure report / Loyal | Loyal | React | не то что бы неожиданно... это же моя любимая сцена!) | accepted | LanguageModel: не то что бы неожиданно... это же моя любимая сцена!) | 361 |
| 38 | PixelFox | failure report / Loyal | Loyal | Question | Ах, этот продавец-неудачник! 😅 А что тогда можно взять вместо него? | accepted | LanguageModel: Ах, этот продавец-неудачник! 😅 А что тогда можно взять вместо него? | 427 |
| 39 | PixelFox | failure report / Loyal | Loyal | Question | ты что вообще делаешь в этой игре? | accepted | LanguageModel: ты что вообще делаешь в этой игре? | 259 |
| 40 | PixelFox | failure report / Loyal | Loyal | React | аааа что же я стыжусь за тебя((((( « | accepted | LanguageModel: аааа что же я стыжусь за тебя((((( « | 334 |
| 41 | NightOwl | direct question / Wary | Wary | Answer | даже не знаю с чего начинать | accepted | LanguageModel: даже не знаю с чего начинать | 262 |
| 42 | NightOwl | direct question / Wary | Wary | Answer | как и ждал | accepted | LanguageModel: как и ждал | 183 |
| 43 | NightOwl | direct question / Wary | Wary | Tease | сперва давай начнем с того | accepted | LanguageModel: сперва давай начнем с того | 254 |
| 44 | NightOwl | direct question / Wary | Wary | Tease | спросил бы раньше, когда был живой | unsupported historical claim | Fallback: я тут | 217 |
| 45 | NightOwl | direct question / Wary | Wary | Answer | еще бы нет | accepted | LanguageModel: еще бы нет | 188 |
| 46 | NightOwl | direct question / Neutral | Neutral | Answer | пока что так себе | accepted | LanguageModel: пока что так себе | 196 |
| 47 | NightOwl | direct question / Neutral | Neutral | Answer | да, но это не мой стиль | accepted | LanguageModel: да, но это не мой стиль | 261 |
| 48 | NightOwl | direct question / Neutral | Neutral | Answer | да я тут, ждал когда ты опять завалишься | accepted | LanguageModel: да я тут, ждал когда ты опять завалишься | 299 |
| 49 | NightOwl | direct question / Neutral | Neutral | Answer | да нет ты вроде круто начинаешь | accepted | LanguageModel: да нет ты вроде круто начинаешь | 327 |
| 50 | NightOwl | direct question / Neutral | Neutral | Answer | спокойный, как всегда | accepted | LanguageModel: спокойный, как всегда | 220 |
| 51 | NightOwl | direct question / Friendly | Friendly | Answer | тут как всегда какая-нибудь дрянь | accepted | LanguageModel: тут как всегда какая-нибудь дрянь | 296 |
| 52 | NightOwl | direct question / Friendly | Friendly | Answer | еще бы не угадала | accepted | LanguageModel: еще бы не угадала | 214 |
| 53 | NightOwl | direct question / Friendly | Friendly | Answer | да, здесь. так себе | accepted | LanguageModel: да, здесь. так себе | 282 |
| 54 | NightOwl | direct question / Friendly | Friendly | Answer | да не тормозит | accepted | LanguageModel: да не тормозит | 170 |
| 55 | NightOwl | direct question / Friendly | Friendly | Answer | не знаю еще что это за стрим | accepted | LanguageModel: не знаю еще что это за стрим | 228 |
| 56 | NightOwl | direct question / Loyal | Loyal | Answer | да ну, а ты думал? устал уже от меня? | accepted | LanguageModel: да ну, а ты думал? устал уже от меня? | 319 |
| 57 | NightOwl | direct question / Loyal | Loyal | Answer | все как всегда и не как всегда | accepted | LanguageModel: все как всегда и не как всегда | 252 |
| 58 | NightOwl | direct question / Loyal | Loyal | Answer | попал в лучшее время для просмотра | accepted | LanguageModel: попал в лучшее время для просмотра | 296 |
| 59 | NightOwl | direct question / Loyal | Loyal | Answer | да, не уйти же с патруля | accepted | LanguageModel: да, не уйти же с патруля | 197 |
| 60 | NightOwl | direct question / Loyal | Loyal | Answer | Давно уже не было | accepted | LanguageModel: Давно уже не было | 227 |
| 61 | NightOwl | failure report / Wary | Wary | Tease | мне бы такого опыта в реальной жизни | accepted | LanguageModel: мне бы такого опыта в реальной жизни | 299 |
| 62 | NightOwl | failure report / Wary | Wary | Tease | всё как всегда с твоими продажами | accepted | LanguageModel: всё как всегда с твоими продажами | 268 |
| 63 | NightOwl | failure report / Wary | Wary | Tease | снова на тупой кусок лопнул | accepted | LanguageModel: снова на тупой кусок лопнул | 279 |
| 64 | NightOwl | failure report / Wary | Wary | Tease | что за шутки? просто умер | accepted | LanguageModel: что за шутки? просто умер | 177 |
| 65 | NightOwl | failure report / Wary | Wary | Question | снова ли ты в этом дурацком геймплее | accepted | LanguageModel: снова ли ты в этом дурацком геймплее | 407 |
| 66 | NightOwl | failure report / Neutral | Neutral | Tease | тупой как и всегда | accepted | LanguageModel: тупой как и всегда | 265 |
| 67 | NightOwl | failure report / Neutral | Neutral | Tease | это же не опечатка в бд? | accepted | LanguageModel: это же не опечатка в бд? | 300 |
| 68 | NightOwl | failure report / Neutral | Neutral | Tease | хорошо хоть не в реальной жизни | accepted | LanguageModel: хорошо хоть не в реальной жизни | 303 |
| 69 | NightOwl | failure report / Neutral | Neutral | Tease | всего-то продал? | accepted | LanguageModel: всего-то продал? | 160 |
| 70 | NightOwl | failure report / Neutral | Neutral | Question | ты в какой серии сидишь | accepted | LanguageModel: ты в какой серии сидишь | 268 |
| 71 | NightOwl | failure report / Friendly | Friendly | Tease | ну да ну | accepted | LanguageModel: ну да ну | 332 |
| 72 | NightOwl | failure report / Friendly | Friendly | Tease | бля, я и сам бы так умер | profanity above this viewer | Fallback: ахах | 241 |
| 73 | NightOwl | failure report / Friendly | Friendly | Tease | ещё раз за игрой не прогуляться? | accepted | LanguageModel: ещё раз за игрой не прогуляться? | 355 |
| 74 | NightOwl | failure report / Friendly | Friendly | Tease | тупого еще нет | accepted | LanguageModel: тупого еще нет | 158 |
| 75 | NightOwl | failure report / Friendly | Friendly | Question | ты что, на урагане играешь или так | accepted | LanguageModel: ты что, на урагане играешь или так | 312 |
| 76 | NightOwl | failure report / Loyal | Loyal | Tease | спускаешься с облаков как каждый раз | accepted | LanguageModel: спускаешься с облаков как каждый раз | 325 |
| 77 | NightOwl | failure report / Loyal | Loyal | Tease | а это уже третий раз за сегодня | accepted | LanguageModel: а это уже третий раз за сегодня | 268 |
| 78 | NightOwl | failure report / Loyal | Loyal | Tease | ты ещё не научился зарабатывать на жизнь как нормальный?  | accepted | LanguageModel: ты ещё не научился зарабатывать на жизнь как нормальный? | 359 |
| 79 | NightOwl | failure report / Loyal | Loyal | Tease | тупой продавец теперь и в жизни | accepted | LanguageModel: тупой продавец теперь и в жизни | 206 |
| 80 | NightOwl | failure report / Loyal | Loyal | Question | а вот и награда за быструю продажу | accepted | LanguageModel: а вот и награда за быструю продажу | 287 |
