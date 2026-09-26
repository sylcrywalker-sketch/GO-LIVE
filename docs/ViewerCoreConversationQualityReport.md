# Viewer Core — conversation quality pass

Continues `aedd3fc` on `claude/sharp-wozniak-iakuny`. Not merged to main. Routing, group/thread ownership, Whisper, `ViewerDailyState` ownership, memory/promise architecture and audience simulation were not changed.

**Result in one paragraph.** The old answer prompt buried the question under metadata and prohibitions, gave the model English facts to calque, and fell back to stock lines. After rewriting the answer contract and making the fallback purpose-aware, Ministral's direct-question PASS rate rose from 45% to 59% on the development corpus, and from 54% to 72% on a holdout set that was never used for tuning. Non-answers to direct questions fell from 24% to 2%. Unrelated fallbacks fell from 11 to 1, and that one is fixed after the benchmark. What remains is mostly the model's Russian: broken words, odd collocations and incoherent late chain turns. Better prompting did not reduce these. With the same prompt, `google/gemma-3-4b` answered direct questions better (82% dev, 87% holdout) with less VRAM, but it is weaker on memory/promise callbacks. **Recommendation: CHANGE MODEL** (to Gemma-3-4B, gated; see the last section). The production config is deliberately still Ministral. This is not a 9.5/10 result: multi-turn chains are still weak for every model tested.

All evidence is in [`docs/evidence/viewer-conversation-quality`](evidence/viewer-conversation-quality): every raw generation ([raw/](evidence/viewer-conversation-quality/raw)), every annotated line ([annotated/](evidence/viewer-conversation-quality/annotated)), the exact prompts ([prompt-audit.md](evidence/viewer-conversation-quality/prompt-audit.md)) and the scripts that computed the tables ([tooling/](evidence/viewer-conversation-quality/tooling)).

## 1. Method

**Corpus.** [`ViewerConversationQualityBenchmark`](../Assets/Game/Tests/Editor/ViewerConversationQualityBenchmark.cs) is an Explicit Unity test. It drives the production planner, `ChatContextBuilder`, adapter, `ChatOutputValidator`, `FallbackChat` and `ChatDirector` through 55 fixed contexts:

- 45 development cases: ReasonForMood ×6 (including the real failed #7 and its named form), TodayActivity ×5, Mood ×4, CurrentActivity ×2, GamePreference ×3, Opinion ×4, Explanation ×4 (including real #11/#12), direct named remarks ×3, a relationship pair, 2 memory and 2 promise callbacks, 4 group questions with 2–3 viewers (real #6/#10/#35), and 5 four-turn chains, one of them the chain from the spec;
- 10 holdout cases, written before the first post-change run and never used while tuning.

It uses authored profiles, recorded day entries and real session phrases. Targets are fixed per case, so routing cannot vary. Each case ran 3 times at the production settings (T 0.85, top-p 0.95, 6 s timeout, same token budget). There were no retries and nothing was selected afterwards. Later chain turns use the model's own earlier answer.

**Annotation.** Every published line was labelled with the spec rubric: PASS or one or more of NON_ANSWER, INCOHERENT, UNSUPPORTED_FACT, WRONG_ROLE, WRONG_TARGET, ASSISTANT_TONE, PERSONALITY_MISS, UNNATURAL_RUSSIAN, REPETITIVE, OVERLONG. That is 1,207 lines across the reported runs; "nothing published" is counted separately. The criteria:

- a stock fallback that does not answer is NON_ANSWER;
- a contradiction of the supplied day or invented history is UNSUPPORTED_FACT; a small elaboration when asked (the soup's kind) is tolerated;
- grammar, non-words, calques, quote leftovers and wrong gender are UNNATURAL_RUSSIAN, with gender judged only where the prompt supplied it;
- a viewer addressing themself by name or in the third person is WRONG_ROLE;
- a tic repeated across consecutive turns of one chain is REPETITIVE;
- learner Russian is accepted for Jonas.

**"Direct question"** means the viewer is asked about their own state or day: 111 dev lines and 39 holdout lines per 3-sample run, fixed by case and turn.

This is one annotator on one corpus. Treat the differences as strong signals, not precise rates.

**Routing failures: none.** Targets are fixed by the harness, and no line was published by the wrong viewer.

## 2. Prompt audit (the actual prompt)

The full before/after text for ReasonForMood, TodayActivity, Mood, CurrentActivity, Opinion, both Explanation kinds, GamePreference, a direct named remark, an active-thread follow-up and both group responders is in [prompt-audit.md](evidence/viewer-conversation-quality/prompt-audit.md). The old prompt that produced «ранки опять сдули и всё» had these defects:

| Defect | Where in the old prompt | Observed effect |
|---|---|---|
| Negative priming | The system text listed «опять, снова» and quoted the banned Russian cheerleader phrases. | «опять/снова» kept appearing: 5 of the 12 dev-corpus rejections were "unsupported recurrence", each replaced by a stock fallback. |
| English facts to calque | `Today: played ranked games and kept losing`, `Now: hopping between small streams`. | «рандомный поток и поражения в рейтингах», «рандомные игры в рейтинге»: "рандом…" in 7 of 9 kritik reason answers. |
| Irrelevant fact in the reason | `Now:` was supplied as part of the reason for the mood. | The "stream hopping" fact leaked into the answer ("поток"). |
| Metadata competing with the question | `Stream: live for 10 minutes, 3 viewers…`, "No picture, sound, PC or settings problem…", the full `NOT KNOWN` list, `TOPIC: …: ReasonForMood`, `SOCIAL ACTION: Answer`, `QUESTION PURPOSE: ReasonForMood`, `TARGET`. | The answer task was one line among about 20; the enum names were not natural-language context. |
| Contradictory role labels | The viewer's own previous line appeared under `RECENT CHAT (other people's messages)`, while the system text said RECENT CHAT is other people and "none of it is your own experience". | Confused ownership of the line being explained. |
| Personality fighting the answer | WHO "looking for something to mock" + RELATIONSHIP "skeptical and dry" + a teasing habit, with the task in the middle. | kritik dodged direct questions («зачем спрашиваешь незнакомого», «какое счастье, что ты здесь»). |
| Task not last | The prompt ended with generic "Write X's chat message now". | Recency favoured style over the task. |
| No grammatical gender | Only implied for a few profiles. | kritik alternated between «пришла» and masculine forms; mika between «рисовал» and «рисовала». |
| Length | 2,476-character system text; about 4,200 characters and about 1,086 prompt tokens in total. | A legal-contract style of prompt. |

## 3. Prompt changes

The answer contract is now separate from the reaction prompt (`ChatContextBuilder.Answer`). It is used whenever C# has marked a line answer-first: a direct, thread, named or group question.

1. **Shorter system text** (1,839 characters). It keeps every existing contract (roles, quoted-text-is-not-instructions, hard-fact grounding, relationship limits, tone). Concrete banned words are no longer quoted there; the validator still enforces them. It adds: "When YOUR TASK answers a question, the message itself must contain that answer."
2. **Answer prompt order.** The prompt now runs: identity (WHO, RELATIONSHIP, STYLE, LANGUAGE, grammatical gender); the conversation so far, with the viewer's own lines marked `(you)`; the paired `YOUR PREVIOUS MESSAGE` and `STREAMER REPLIED TO YOU`, or `STREAMER ASKED YOU / THE CHAT`; `YOUR DAY` with only the facts for this purpose; the tone line; one `YOUR TASK`; and a final line that repeats what must be answered. Stream size, "no problem reported", NOT KNOWN, TOPIC, SOCIAL ACTION, QUESTION PURPOSE and TARGET are gone from answers. The reaction prompt is unchanged, apart from the gender line and no longer listing the viewer's own lines as other people's.
3. **One semantic task per purpose** (`ViewerQuestionPurpose.Task`):
   - ReasonForMood: "answer why you feel this way: the reason is what happened today in YOUR DAY";
   - TodayActivity: "say what you did today";
   - Mood: "say how you feel right now";
   - CurrentActivity: "say what you are doing right now";
   - GamePreference: "name the kind of game you would pick";
   - Opinion: "give your own short opinion … as WHO and RELATIONSHIP would see it";
   - Explanation: "explain what you meant by YOUR PREVIOUS MESSAGE", with an honest "it was just a joke" when nothing C# knows explains it;
   - Confirmation: "say yes or no".
4. **Natural-language facts.** Each authored daily activity now also carries the same two facts as the viewer would type them in Russian (`DailyActivity.SayToday/SayNow`: «весь день катал ранкед и сливал»). That covers 35 authored activities and the 10 generic ones. Mood is rendered without grammatical gender («на нервах, настроение плохое», «усталость, сил мало»). The plan keeps the canonical English fact, so the validator, planner and tests are unaffected; English speakers get the English text.
5. **Facts per purpose.** A reason gets mood + today, not "now". A question about one of the viewer's own doings in a personal exchange («часто играешь в ранкед?», «а ты сегодня на смене?») gets today + now, but only if the day covers that doing. Word-order variants are recognized («чем сейчас занята?», «чем занималась сегодня?»). A presence check («ты тут?») is a Confirmation.
6. **Grammatical gender** is a new `ViewerProfile.Gender`. Please review these authoring choices:
   - male: NightOwl ("his"), kritik228 (your example «сливал»), Jonas, doshirak_king, Sovetnik_Pro;
   - female: ZinaIvanovna, PixelFox ("her"), mika_draws (your example «рисовала»);
   - unspecified: ByteCat (its Russian day phrasing is gender-neutral).
7. **Relationship** changes delivery only. A Wary viewer answers "dry and brief, but you still really answer"; a Loyal viewer is "glad they asked".

The prompt was developed on a 9-case smoke run plus the first full run. The development corpus is therefore not blind; the holdout is.

## 4. Purpose-aware fallback

`FallbackChat.Pick` now receives the generation situation from `ChatDirector`. For a question C# has classified, it answers from the same facts the prompt supplied, or returns **silence**:

| Purpose | Fallback built from | Example |
|---|---|---|
| ReasonForMood / TodayActivity / Explanation of a daily line | `SayToday` | «весь день катал ранкед и сливал» |
| CurrentActivity | `SayNow` | «по мелким стримам прыгаю» |
| Mood | a small gender-free family per `ViewerMood`, optionally plus `SayNow` | «на нервах сегодня, по мелким стримам прыгаю»; Zina: «Устало немного, сил мало» |
| Confirmation | yes/no from mood and energy; a presence check gets «я тут» | «да, есть немного» |
| GamePreference / Opinion | a neutral short opinion (Wary: «ну такое») | «что-нибудь попроще» |
| Explanation without facts, origin, any other direct question | — | silence |

A thread reply that asks nothing gets a listening sign («ну да») instead of the old «норм, а ты как?». A named greeting now gets a greeting, and a named thanks gets «да не за что» (the thanks fix came after the benchmark; see §10). Letter case and slang register follow the profile.

Effect on the dev corpus: 12 fallbacks (11 unrelated) → 11 fallbacks (1 unrelated).

The new test `EveryAuthoredRussianPhrasePublishesAsAnAnswerForItsOwnDay` proves that every authored phrase passes the validator for its own day, so a fallback is never discarded at publication.

## 5. Before / after (Ministral, 3 samples per case)

| Run | Lines | PASS | Direct-Q PASS | Direct-Q NON_ANSWER | Fallback (unrelated) | NON_ANSWER | INCOHERENT | UNSUPPORTED | WRONG_ROLE | UNNATURAL_RU | Chains all-PASS | Latency mean / median / p90 s |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| before, dev | 204 | 88 (43%) | 50/111 (45%) | 27 (24%) | 12 (11) | 41 | 42 | 24 | 3 | 35 | 0/15 | 0.33 / 0.32 / 0.47 |
| **after, dev** | 204 | **120 (59%)** | **65/111 (59%)** | **2 (2%)** | 11 (1) | 12 | 25 | 12 | 2 | **43** | 1/15 | 0.35 / 0.30 / 0.57 |
| before, holdout | 45 | 24 (53%) | 21/39 (54%) | 5 (13%) | 5 (4) | 5 | 10 | 5 | 0 | 3 | 0/3 | 0.35 / 0.35 / 0.48 |
| **after-1, holdout (clean)** | 45 | **32 (71%)** | **28/39 (72%)** | 1 (3%) | 1 (0) | 2 | 5 | 2 | 1 | 5 | 0/3 | 0.34 / 0.33 / 0.44 |
| after, holdout | 45 | 32 (71%) | 30/39 (77%) | 0 | 1 (0) | 0 | 6 | 1 | 0 | 8 | 1/3 | 0.36 / 0.32 / 0.59 |

"after-1" is the first full post-change run. It is the clean holdout measurement: it exposed one classifier gap (H05 «а ты сегодня на смене?» got no day facts), which I then fixed. "after" is the final code. The after-1 development-corpus lines are kept in raw/ but were not annotated, because the final run supersedes them. ASSISTANT_TONE was 2 → 1, PERSONALITY_MISS 0 → 1, and REPETITIVE and OVERLONG were 0 in both. Silent lines went 0 → 2; both were valid model texts rejected as too long or multi-line.

The real failed case, R01 «Почему без настроения?» after «без настроения но здесь сижу»:

| | Sample 1 | Sample 2 | Sample 3 |
|---|---|---|---|
| before | «рандомные игры в рейтинге меня убивают» | «ранкирушки опять сожрали нервы» → rejected → **«живой, а у тебя как?»** | «рандомный боец снова меня убил в ранке» → rejected → **«норм, а ты как?»** |
| after, Ministral | «какойто ренкед был» | «ранкед меня раздражает» | «ранкед не проходит» |
| after, Qwen3-8B | «ранкед весь день сливал» | «ранкед катал и сливал» | «ранкед весь день сливал» |
| after, Gemma-3-4B | «катал ранкед и сливал, ну и блин» | «все день катал ранкед и сливал хах» | «катал ранкед и сливал весь день хах» |
| fallback if rejected now | «весь день катал ранкед и сливал» | | |

Other lines that changed class, before → after:

- ReasonForMood fallback: «живой, а у тебя как?» → «пытался готовиться к экзамену, в основном прокрастинировал»;
- TodayActivity fallback: «норм, а ты как?» → «весь день на скучных уроках сидел»;
- group mood, kritik: «всё как обычно тут» → «на стрессе, прыгаю по мелкому»;
- Zina's reason: «Сидела я долго в очереди у врача.»;
- mika's reason: «только рисовать не получается».

Every line of every run, with its facts, raw model text, validator verdict, published source and labels, is in the [annotated CSVs](evidence/viewer-conversation-quality/annotated).

## 6. Multi-turn conversations

The spec chain CH1, kritik (Wary, ranked day): «без настроения но здесь сижу» → «почему без настроения?» → «понятно. часто играешь в ранкед?» → «ну ты хоть иногда выигрываешь?» → «ладно, не расстраивайся».

| Sample | Before | After (Ministral) |
|---|---|---|
| 1 | «рандомный пул не друг» / «не знаю, чем занят» / «зачем вообще побеждать тут» / «ты просто так и сидишь» | «ранкед понадеялся на рандом» / «да и всё время в сливе» / «ничего не выиграл» / «ты что бабушке сказки рассказываешь» |
| 2 | fallback «да потихоньку, отдыхаю» / «рандомно что-то да. тут же не важно» / «ну я и так угадал» / «ладно, буду здесь еще минутку» | «всех в рангед слил и сожрал» / «да не часто» / «ты что ли мне сдался?» / fallback «ну да» |
| 3 | «рандомный пушистик не спас даже» / «только от безысходности пришла» / «не моя вина что ты всегда проигрываешь» / «ты же ещё не пытался» | «ранкед меня сломал» / «не всегда но и не редко» / «не очень» / «думал уже вылетел» |

The first two or three turns usually hold together now (sample 3 passes turns 1–3); the closing turn still often misfires.

Holdout chain H09, Zina at the clinic, final code: «Устала сегодня немного, но чай с вязанием — это же чудо!» / «Ох, давно не было такого — часы просидела в очереди в поликлинике.» / «Конечно же устала, чаю бы согреться.» / «Но вы всё-таки не ложитесь слишком поздно перед этим? Бывает так...». This one is coherent. Sample 1 of the same chain starts with the non-word «Спитсонку».

Chain results, counting a chain as all-PASS only when all 4 turns pass:

- Ministral: 1/15 dev and 1/3 holdout after, from 0/15 and 0/3 before.
- Qwen3-8B: 1/15 and 0/3.
- Gemma-3-4B: 0/15 and 0/3.

Turns 1–2 are usually right now. Turns 3–4 («ну ты хоть иногда выигрываешь?», «ладно, держись там», «ну отдыхайте тогда») are where every model loses the thread. **Short conversations do not yet meet the bar "readable as one coherent exchange".**

Group questions: no semantic clones in the Ministral waves (for example «на нервах сегодня, по мелким стримам прыгаю» / «Настроение отличное — …» / «все окей»). Each viewer used their own day.

Relationship: the Wary/Loyal NightOwl pair (RD1/RD2) sounded almost the same for all models, which is weak but not wrong. The Wary/Friendly kritik pair (O02/O04) differed in the intended direction («ну такое» versus «был бы скучнее, если б не ты»).

Callbacks, Ministral after: memory MC1 2/3 recognizable; MC2 2/3, one misdated as «второй раз за день»; promise PC1 3/3. On PC2, the model's «уже пять минут как обещали» and «а вот и обещанный старт» were rejected as promise claims, and the greeting fallback was published.

## 7. Model benchmark (same corpus, final prompt)

Local inventory (LM Studio, RTX 5070 12 GB, 31 GB RAM). Everything below is Q4_K_M unless noted. Nothing was downloaded. VRAM is the increase measured by `nvidia-smi` when only that model was loaded (4096 context, no Unity).

| Model | Result | VRAM | Median / p90 latency |
|---|---|---|---|
| **mistralai/ministral-3-8b-instruct-2512 (production)** | 3-sample run (§5) | 5,668 MiB | 0.30 / 0.57 s |
| qwen/qwen3-8b | 3 samples, with `/no_think` appended to the system text; without it, thinking used the whole token budget | 5,293–5,325 MiB | 0.27 / 0.40 s |
| **google/gemma-3-4b** | 1-sample screen, then 3 samples | 4,093–4,095 MiB | 0.28 / 0.35 s |
| qwen/qwen3-4b-2507 | 1-sample screen | 3,195–3,323 MiB | 0.21 / 0.28 s |
| qwen3.5-4b | excluded: thinking cannot be turned off through the production adapter (`/no_think` ignored; 44/44 tokens were reasoning, empty content) | 4,009 MiB | — |
| phi-4-mini-instruct | excluded after probe: 3/3 R01 probes garbled («долбанулся», «собираю пена») | 3,137 MiB | — |
| openai/gpt-oss-20b (MXFP4) | excluded after probe: does not fit next to the game (9,573 MiB), and its Russian was odd («тут весь день catalog ранкед и слил», «каталогировал»); a reasoning model at 0.4–1.0 s | 9,573 MiB | — |

qwen3-vl-8b (a vision variant of Qwen3-8B) and a 27B distillation (16 GB) were not run.

| Final prompt, 3 samples | Lines | PASS | Direct-Q PASS | NON_ANSWER | INCOHERENT | UNSUPPORTED | WRONG_ROLE | UNNATURAL_RU | REPETITIVE | Fallbacks | Silent |
|---|---|---|---|---|---|---|---|---|---|---|---|
| Ministral, dev | 204 | 120 (59%) | 65/111 (59%) | 12 | 25 | 12 | 2 | 43 | 0 | 11 | 2 |
| Qwen3-8B, dev | 204 | 123 (60%) | 76/111 (68%) | 7 | 12 | 4 | 19 | 33 | 1 | 3 | 9 |
| **Gemma-3-4B, dev** | 204 | **134 (66%)** | **91/111 (82%)** | 19 | 9 | 6 | 6 | 27 | 7 | 12 | 0 |
| Ministral, holdout | 45 | 32 (71%) | 30/39 (77%) | 0 | 6 | 1 | 0 | 8 | 0 | 1 | 0 |
| Qwen3-8B, holdout | 45 | 31 (69%) | 30/39 (77%) | 0 | 1 | 1 | 0 | 5 | 8 | 2 | 2 |
| **Gemma-3-4B, holdout** | 45 | **37 (82%)** | **34/39 (87%)** | 0 | 0 | 1 | 1 | 1 | 5 | 4 | 0 |
| Qwen3-4B-2507, 1 sample | 83 | 48 (58%) | 38/50 (76%) | 2 | 6 | 3 | 2 | 20 | 3 | 5 | 1 |

What each model does wrong:

**Ministral.** Garbled words and collocations: «Спитсонку», «споконвеку», «рантарил кран», «серверы в аду тошнит», «клубничек навязывается». It adds unsupported details («сидела час с лишним на сквозняке», «а у меня была такая тетя», «лаг тут на каждом шаге») and its late chain turns lose the thread. It has the widest variety and follows callbacks best.

**Qwen3-8B.** Very grounded and clean, but near-identical across samples. ZinaIvanovna addresses herself by name or in the third person («Зина Ивановна сегодня сходила на рынок…»), which accounts for 19 WRONG_ROLE lines. The smiley instruction leaves a literal «) on PixelFox lines, and «сынок» opens every Zina line.

**Gemma-3-4B.** The best direct answers and the fewest incoherent lines, but:

- it ignores the promise callback (PC1 0/3: «сынок, вы хоть поели сегодня?») and a memory callback (MC2 1/3);
- it dodges opinion questions («ну и что ты хотел спросить?»);
- doshirak and mika address themselves by name;
- it has «хах»/«сынок» tics;
- it leaves `»}` quote remnants, which were rejected 9 times. The purpose fallbacks hide this: 12 Gemma lines were fallbacks, none unrelated. Without the fallbacks its dev direct-question PASS would be about 73%.

Its answers are also low-variety: the same question gets the same line.

**Is the 8B model the quality ceiling?** Partly yes. The prompt contract removed dodged answers and unrelated fallbacks, and made the reason-for-mood answer reliable. What Ministral still gets wrong is language-level, and the prompt did not reduce it (UNNATURAL_RUSSIAN 35 → 43 lines on the dev corpus). With the identical prompt, a 4B model produced fewer broken Russian lines (27) and fewer incoherent lines (9 vs 25). Some cross-model artifacts come from this prompt itself: the «» quoting of style tokens such as `End with «)»` causes the Qwen/Gemma quote leftovers.

## 8. Current production model and resources

Production is still `mistralai/ministral-3-8b-instruct-2512` Q4_K_M, with T 0.85, top-p 0.95, 64 max tokens and a 6 s timeout. `ViewerCore.asset` is unchanged, and LM Studio was left with Ministral loaded as before (4096 context, parallel 4).

The new answer prompt is 21% shorter: about 3,330 versus 4,200 characters on average, and about 823 versus 1,086 prompt tokens. Ministral latency is essentially unchanged (median 0.30 versus 0.32 s; p90 0.57 versus 0.47 s; single maximum 1.13 s). No extra requests, retries or verifier calls were added.

## 9. Remaining bad outputs (final code, Ministral)

- **Broken or unnatural Russian (43 dev lines):** non-words, calques, wrong prepositions, odd similes. Examples: «на ножках ходит уже втрое сутки», «как слон на блинчиках».
- **Incoherent late chain turns and explanations (25):** «ты что бабушке сказки рассказываешь» to "don't be upset"; «было видно что он не понял».
- **Unsupported details (12):** durations, relatives, lag and misdated callbacks («уже второй раз за день» for yesterday's mic cut). The validator does not catch «час с лишним» (no count word). It accepts «уже три часа» because the stream fact "3 viewers" makes "3" grounded. I left both gaps as they are and report them here.
- **Gender slips despite the new line:** «скинула» from kritik, «Устал немного» from PixelFox, «использовала» addressed to the male streamer.
- **Persona:** a Wary kritik praising («потрясный стрим»); ByteCat lecturing about microphones it cannot hear.
- **Classifier limits:** «а чего так устала?» is not a why-question (only почему/отчего are); «и долго ещё?» gets no day facts.

## 10. Tests

**Full ordinary Unity suite: 1,321 test cases — 1,308 passed, 0 failed, 13 skipped** (the 12 existing Explicit cases plus the new Explicit benchmark), in 140 s ([XML](evidence/viewer-conversation-quality/full-suite-final.xml)). That is the previous 1,276 plus 32 new deterministic cases.

New, [`ViewerConversationQualityTests`](../Assets/Game/Tests/Editor/ViewerConversationQualityTests.cs), 32 cases. No model wording is asserted.

- **Prompt contract:**
  - a ReasonForMood prompt asks why, supplies the Russian reason, excludes "now" and the metadata, and ends with the task;
  - the previous line is paired before the question and marked as the viewer's own;
  - each purpose gets only its own facts;
  - grammatical gender reaches Russian speakers only.
- **Fallback:**
  - the reason fallback equals the supplied reason;
  - no direct question gets a stock line;
  - the mood fallback follows the mood;
  - with no truthful answer (no day, a nonsense explanation, a generic question, origin) the result is silence;
  - a Russian viewer without Russian phrasing stays silent;
  - a presence check is confirmed;
  - a named thanks is thanked;
  - `ChatDirector` publishes the purpose answer when model output is rejected.
- **Data and classification:**
  - every authored Russian phrase passes the validator for its own day;
  - free word order is recognized;
  - a doing question gets its day only when the day covers it.
- **Hard facts:** hardware, money, stream-duration, own-donation and recurrence claims are still rejected inside an answered reason.

Three existing assertions pinned the old prompt text and were updated to the new contract:

- `FollowUpPromptPairs…` no longer expects the `QUESTION PURPOSE` enum line;
- `RealMoodFollowUp…` now requires that "now" is **not** part of a reason;
- `TheViewersOwnRecentLines…` checks the `(you)`-marked lines in the answer prompt.

The `ChatDirectorTests` that expect a fallback for «ты тут?» pass unchanged, because presence is now a truthful Confirmation.

Two validator false positives found by the benchmark were narrowed (not widened):

- «скучал» ("was bored") is no longer a devotion claim; only «скучал по…» is;
- a day that says "fixed a leaking tap" now permits «чинил кран».

`*emphasis*` markup around invented titles is now rejected.

After the model runs, the named-thanks fallback («а?» → «да не за что») was fixed. It does not change any prompt. The benchmark tables count that line as the old «а?» NON_ANSWER.

The first "before" attempt aborted on a harness bug (anonymous seats cannot join by name) after 111 lines. It is kept unannotated in raw/ as `before-ministral-20260926-200244.jsonl`; the annotated before run is the complete rerun.

## 11. Recommendation

**CHANGE MODEL — to `google/gemma-3-4b` (Q4_K_M), as a gated switch.** On the declared blocker, direct personal and factual questions, it answered 82% of dev lines and 87% of holdout lines correctly, against Ministral's 59% and 77%. It produced fewer incoherent and broken-Russian lines, and needs about 1,570 MiB less VRAM with a lower p90 latency, which matters with the game on the same GPU.

It is not a free win. It regresses explicit promise/memory callbacks and opinion answers, has strong verbal tics and low variety, and part of its score comes from the new fallbacks catching its markup leftovers. The prompt was also developed against Ministral.

I did **not** flip `ViewerCore.asset`. Before switching, I suggest:

1. remove the «» quoting of style tokens in `Habits`;
2. re-run this benchmark on Gemma and Ministral and check the callbacks;
3. run the one [60–90 s microphone acceptance](ViewerCoreConversationQualityMicrophoneAcceptance.md) with the chosen model while the game is running.

If you prefer to stay on Ministral, the new prompt and fallback still stand on their own: they are model-agnostic, and they removed the non-answers.

The microphone session has not been performed. Viewer Core is still not accepted. Nothing is merged to main.
