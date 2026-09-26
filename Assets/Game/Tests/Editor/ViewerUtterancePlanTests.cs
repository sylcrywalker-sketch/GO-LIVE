using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Desktop;
using GoLive.PcBuilding;
using GoLive.Viewers;
using GoLive.Voice;
using NUnit.Framework;

namespace GoLive.Tests
{
    // Social quality pass: C# decides what a viewer is socially doing and which facts exist before any generation.
    // These guard the deterministic parts (plan, factual envelope, relationship behavior, callback bounds and
    // grounding rejections of the audited defects). They never assert generated wording.
    public sealed class ViewerUtterancePlanTests
    {
        private const string FailurePhrase = "Блин, я продал не тот предмет и опять умер в игре самым тупым способом!";

        internal static ReactionIntent Make(long id, StreamEvent e, ChatParticipant viewer, bool direct = false, long epoch = 0) =>
            (ReactionIntent)typeof(ReactionIntent).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 8).Invoke(new object[] { id, e, viewer, direct, 0, 10d, 30d, epoch });

        internal static StreamEvent Said(string text, ChatParticipant named = null, long sequence = 1, string language = "ru")
        {
            var names = named == null ? Array.Empty<ViewerNameForms>() : new[] { new ViewerNameForms(named.ViewerId, named.NameForms) };
            return StreamEvent.StreamerSpeech(sequence, "plan", 600, SpeechRelevance.Analyze(new RecognizedSpeech(sequence, text, 0, .9f, language), names));
        }

        private static ChatSituation Situation(RelationshipTier tier = RelationshipTier.Neutral, IReadOnlyList<StreamChatMessage> chat = null,
            IReadOnlyList<ViewerMemory> memories = null, ViewerPromiseContext promise = null, double streamSeconds = 600) =>
            new("secret_channel_42", streamSeconds, 10, ViewerLanguage.Russian, chat, null, tier + ".", memories, promise, tier, StreamTopic.Games, 4000);

        private static ChatParticipant Authored(string id) => ViewerProfileTests.Profile(id).Participant();

        private static Dictionary<UtteranceIntent, int> Intents(ChatParticipant viewer, Func<long, StreamEvent> moment, RelationshipTier tier,
            bool direct = false, int count = 400)
        {
            var counts = new Dictionary<UtteranceIntent, int>();
            for (long id = 1; id <= count; id++)
            {
                UtteranceIntent intent = ViewerUtterancePlanner.For(Make(id, moment(id), viewer, direct), Situation(tier)).Intent;
                counts[intent] = counts.TryGetValue(intent, out int n) ? n + 1 : 1;
            }
            return counts;
        }

        private static double Share(Dictionary<UtteranceIntent, int> counts, UtteranceIntent intent) =>
            counts.TryGetValue(intent, out int n) ? n / (double)counts.Values.Sum() : 0;

        [Test]
        public void PlanIsBuiltFromGameStateAndBoundsThePrompt()
        {
            ChatParticipant viewer = Authored("viewer.nightowl");
            StreamEvent moment = Said("Чат, как думаете, какую игру лучше запустить дальше?");
            ReactionIntent intent = Make(7, moment, viewer);
            ChatSituation situation = Situation();
            ViewerUtterancePlan plan = ViewerUtterancePlanner.For(intent, situation);
            Assert.That(ViewerUtterancePlanner.For(intent, situation), Is.SameAs(plan), "prompt and validation share one plan");
            Assert.That(plan.ViewerId, Is.EqualTo(viewer.ViewerId));
            Assert.That(plan.CurrentEventId, Is.EqualTo(moment.Key));
            Assert.That(plan.Language, Is.EqualTo(ViewerLanguage.Russian));
            Assert.That(plan.RelevantMemoryId, Is.Null);
            Assert.That(plan.RelevantPromiseId, Is.Null);
            Assert.That(plan.RelationshipTone, Is.EqualTo(RelationshipTier.Neutral));
            Assert.That(plan.AllowedFacts.Any(f => f.Source == FactSource.StreamerSaid && f.Text == moment.Speech.Text), Is.True);
            Assert.That(plan.AllowedFacts.Any(f => f.Source == FactSource.Stream && f.Text.Contains("Nobody knows when it will end")), Is.True);

            ViewerChatRequest request = ChatContextBuilder.Build(intent, situation, 64);
            Assert.That(request.System, Does.Contain("DO NOT INVENT SPECIFIC GAME FACTS"));
            Assert.That(request.User, Does.Contain("SOCIAL ACTION: " + plan.Intent));
            Assert.That(request.User, Does.Contain("TOPIC: " + plan.Topic));
            Assert.That(request.User, Does.Contain("NOT KNOWN"));
            Assert.That(request.User, Does.Contain("STREAMER SAID: «" + moment.Speech.Text + "»"));
            Assert.That(request.User, Does.Not.Contain("secret_channel"), "the channel name is not a topic the model may borrow");
            Assert.That(request.User, Does.Not.Contain("MEMORY:"));
        }

        [Test]
        public void SocialActionFollowsTheMomentAndAuthoredHabits()
        {
            StreamEvent Failure(long id) => Said(FailurePhrase, sequence: id);
            StreamEvent MicrophoneLost(long id) => StreamEvent.PeripheralChanged(id, "mic." + id, 600, PcPeripheralKind.Microphone, false);
            StreamEvent Question(long id) => Said("Чат, как думаете, какую игру лучше запустить дальше?", sequence: id);

            var mika = Intents(Authored("viewer.mika"), Failure, RelationshipTier.Friendly);
            Assert.That(mika.Keys, Is.SubsetOf(new[] { UtteranceIntent.React, UtteranceIntent.Concern }), "gentle Mika never teases or pushes back");
            var owl = Intents(Authored("viewer.nightowl"), Failure, RelationshipTier.Neutral);
            Assert.That(owl.ContainsKey(UtteranceIntent.Concern), Is.False);
            Assert.That(Share(owl, UtteranceIntent.Tease), Is.GreaterThan(.4), "NightOwl keeps score by teasing");
            var kritik = Intents(Authored("viewer.kritik228"), Failure, RelationshipTier.Wary);
            Assert.That(kritik.Keys, Has.No.Member(UtteranceIntent.Tease).And.No.Member(UtteranceIntent.Concern).And.No.Member(UtteranceIntent.Question));
            Assert.That(Share(kritik, UtteranceIntent.Disagree), Is.GreaterThan(.2), "kritik is unimpressed rather than teasing");

            Assert.That(Intents(Authored("viewer.pixelfox"), MicrophoneLost, RelationshipTier.Neutral).Keys, Has.No.Member(UtteranceIntent.TechnicalComment),
                "a non-technical viewer is not pushed into a hardware theme");
            Assert.That(Share(Intents(Authored("viewer.bytecat"), MicrophoneLost, RelationshipTier.Neutral), UtteranceIntent.TechnicalComment), Is.GreaterThan(.45));
            Assert.That(Share(Intents(Authored("viewer.sovetnik"), Question, RelationshipTier.Neutral), UtteranceIntent.Answer), Is.GreaterThan(.6));
            Assert.That(Share(Intents(Authored("viewer.zinaivanovna"), Failure, RelationshipTier.Friendly), UtteranceIntent.Concern), Is.GreaterThan(.3));

            ChatParticipant fox = Authored("viewer.pixelfox");
            Assert.That(Intents(fox, id => Said("спасибо PixelFox", fox, id), RelationshipTier.Neutral, true).Keys, Is.EqualTo(new[] { UtteranceIntent.ThankResponse }));
            Assert.That(Share(Intents(Authored("viewer.nightowl"), id => StreamEvent.StreamerSilence(id, "s." + id, 600, 200, SilenceLevel.VeryLong),
                RelationshipTier.Neutral), UtteranceIntent.SilenceCheck), Is.GreaterThan(.6));
            Assert.That(Share(Intents(Authored("viewer.mika"), id => Said("Так, ну, секунду, ага", sequence: id), RelationshipTier.Friendly), UtteranceIntent.React),
                Is.EqualTo(1), "filler is never a question or a new topic");
        }

        [Test]
        public void RelationshipChangesTheSocialActionNotOnlyAnAdjective()
        {
            ChatParticipant fox = Authored("viewer.pixelfox");
            StreamEvent Direct(long id) => Said("PixelFox, ты тут? Как тебе сегодняшний стрим?", fox, id);
            StreamEvent Failure(long id) => Said(FailurePhrase, sequence: id);
            StreamEvent Question(long id) => Said("Чат, как думаете, какую игру лучше запустить дальше?", sequence: id);
            ChatParticipant owl = Authored("viewer.nightowl");

            Assert.That(Intents(fox, Failure, RelationshipTier.Wary).ContainsKey(UtteranceIntent.Concern), Is.False, "wary viewers do not reassure");
            Assert.That(Share(Intents(fox, Failure, RelationshipTier.Loyal), UtteranceIntent.Concern),
                Is.GreaterThan(Share(Intents(fox, Failure, RelationshipTier.Neutral), UtteranceIntent.Concern)), "loyal viewers back the streamer");
            Assert.That(Share(Intents(fox, Direct, RelationshipTier.Friendly, true), UtteranceIntent.Question),
                Is.GreaterThan(Share(Intents(fox, Direct, RelationshipTier.Wary, true), UtteranceIntent.Question)), "warmth keeps the conversation going");
            Assert.That(Share(Intents(owl, Question, RelationshipTier.Wary), UtteranceIntent.Disagree),
                Is.GreaterThan(Share(Intents(owl, Question, RelationshipTier.Neutral), UtteranceIntent.Disagree)), "wariness is skepticism, not toxicity");

            string wary = ChatContextBuilder.Build(Make(3, Said("спасибо PixelFox", fox, 3), fox, true), Situation(RelationshipTier.Wary), 64).User;
            string loyal = ChatContextBuilder.Build(Make(3, Said("спасибо PixelFox", fox, 3), fox, true), Situation(RelationshipTier.Loyal), 64).User;
            Assert.That(wary, Does.Contain("curtly, without warmth"));
            Assert.That(loyal, Does.Contain("happy to support them"));
        }

        [Test]
        public void RelationshipChangesEligibilityAndDelayBeforeThePrompt()
        {
            ChatParticipant fox = Authored("viewer.pixelfox");
            (double rate, double delay) Measure(RelationshipTier tier, string phrase)
            {
                int reactions = 0; double delays = 0;
                for (ulong seed = 1; seed <= 400; seed++)
                {
                    AudienceRoster roster = ReactionFoundationTests.Roster(5, fox);
                    var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed), id => id == fox.ViewerId ? tier : RelationshipTier.Neutral);
                    StreamEvent e = StreamEvent.StreamerSpeech((long)seed, "tier", 10,
                        SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(phrase, (long)seed), roster.NamesForMentions()));
                    ReactionIntent intent = selector.Select(e, 10, true, out _).FirstOrDefault(i => i.Viewer == fox && i.Direct);
                    if (intent == null) continue;
                    Assert.That(intent.Tier, Is.EqualTo(tier));
                    reactions++; delays += intent.DueSeconds - 10;
                }
                return (reactions / 400.0, delays / Math.Max(1, reactions));
            }
            var waryFluff = Measure(RelationshipTier.Wary, "PixelFox привет");
            var neutralFluff = Measure(RelationshipTier.Neutral, "PixelFox привет");
            var waryQuestion = Measure(RelationshipTier.Wary, "PixelFox как тебе стрим?");
            var loyalQuestion = Measure(RelationshipTier.Loyal, "PixelFox как тебе стрим?");
            Assert.That(waryFluff.rate, Is.LessThan(.65), "a wary viewer may let a pleasantry pass");
            Assert.That(neutralFluff.rate, Is.GreaterThan(.85));
            Assert.That(waryQuestion.rate, Is.GreaterThan(waryFluff.rate), "a real question still gets a wary answer");
            Assert.That(loyalQuestion.rate, Is.GreaterThan(.93));
            Assert.That(loyalQuestion.delay, Is.LessThan(waryQuestion.delay * .8), "close viewers answer sooner");
        }

        [Test]
        public void SkepticsDigDrawsOutFriendsWhoPushBack()
        {
            ChatParticipant friend = Authored("viewer.doshirak"); // ordinary habits: may push back
            ChatParticipant kritik = Authored("viewer.kritik228");
            double Disagree(RelationshipTier origin)
            {
                int disagree = 0;
                for (long id = 1; id <= 400; id++)
                {
                    var roster = ReactionFoundationTests.Roster(5, friend, kritik);
                    var line = new StreamChat().Add("b", kritik.ViewerId, kritik.DisplayName, "скука как всегда", 5, 1, ReactionSource.LanguageModel);
                    var reply = (StreamEvent)typeof(StreamEvent).GetMethod("Reply", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { line, 6d, roster, 100d });
                    var situation = new ChatSituation("c", 60, 5, ViewerLanguage.Russian, null, null, tier: RelationshipTier.Friendly, replyTargetTier: origin);
                    ViewerUtterancePlan plan = ViewerUtterancePlanner.For(Make(id, reply, friend), situation);
                    Assert.That(plan.Target, Is.EqualTo(UtteranceTarget.OtherViewer));
                    Assert.That(plan.ReplyTarget, Is.EqualTo(kritik.DisplayName));
                    if (id == 1)
                        Assert.That(ChatContextBuilder.Build(Make(id, reply, friend), situation, 64).User,
                            Does.Contain("OTHER VIEWER SAID (kritik228, not you, not the streamer): «скука как всегда»"));
                    if (plan.Intent == UtteranceIntent.Disagree) disagree++;
                }
                return disagree / 400.0;
            }
            Assert.That(Disagree(RelationshipTier.Wary), Is.GreaterThan(Disagree(RelationshipTier.Neutral) + .1));
        }

        [TestCase(RelationshipTier.Wary, "Ох, а я тут же! Смотрю уже с начала и влюбилась 😍", false)]
        [TestCase(RelationshipTier.Loyal, "я в тебя влюбилась", false)]
        [TestCase(RelationshipTier.Neutral, "мой любимый стример тут", false)]
        [TestCase(RelationshipTier.Loyal, "мой любимый стример тут", true)]
        [TestCase(RelationshipTier.Neutral, "я тут, смотрю ❤", false)]
        [TestCase(RelationshipTier.Friendly, "я тут, смотрю ❤", true)]
        [TestCase(RelationshipTier.Wary, "ну тут я, смотрю", true)]
        public void RelationshipClaimsCannotExceedGameState(RelationshipTier tier, string text, bool accepted)
        {
            ChatParticipant fox = Authored("viewer.pixelfox");
            ReactionIntent intent = Make(1, Said("PixelFox, ты тут? Как тебе сегодняшний стрим?", fox), fox, true);
            ChatValidation result = ChatOutputValidator.Validate(text, intent, null, Situation(tier));
            Assert.That(result.Accepted, Is.EqualTo(accepted), text + " -> " + result.Reason);
            if (!accepted) Assert.That(result.Reason, Is.EqualTo("relationship claim above game state"));
        }

        [TestCase("viewer.sovetnik", "Да я здесь, только ждать осталось всего пять минут до конца.", false)]
        [TestCase("viewer.sovetnik", "Что, RTX 4090 уже устарела? И зачем такой мощности на стриме?", false)]
        [TestCase("viewer.kritik228", "тут же р5 1060 с года", false)]
        [TestCase("viewer.bytecat", "на интеле такое бывает", true)]
        [TestCase("viewer.bytecat", "амд или нвидиа вот в чём вопрос", true)]
        [TestCase("viewer.bytecat", "видюха это всегда больная тема", true)]
        [TestCase("viewer.bytecat", "уже 20 минут стрима и ничего", false)]
        [TestCase("viewer.bytecat", "10 минут стрима а уже весело", true)]
        [TestCase("viewer.nightowl", "1v5 это сильно", true)]
        [TestCase("viewer.nightowl", "ну 10/10 конечно", true)]
        public void SpecificNumbersAndHardwareNeedFactualSupport(string id, string text, bool accepted)
        {
            ChatParticipant viewer = Authored(id);
            ReactionIntent intent = Make(1, Said("Чат, что думаете о видеокарте?"), viewer);
            ChatValidation result = ChatOutputValidator.Validate(text, intent, null, Situation());
            Assert.That(result.Accepted, Is.EqualTo(accepted), text + " -> " + result.Reason);
            if (!accepted) Assert.That(result.Reason, Does.StartWith("ungrounded specific claim"));
        }

        [Test]
        public void SupportedSpecificsPassFromSpeechChatAndNames()
        {
            ChatParticipant byteCat = Authored("viewer.bytecat");
            Assert.That(ChatOutputValidator.Validate("на интеле такое бывает", Make(9, Said(FailurePhrase), byteCat), null, Situation()).Reason,
                Does.StartWith("ungrounded specific claim"), "a brand dragged into a non-hardware moment is invented");
            ChatParticipant owl = Authored("viewer.nightowl");
            ReactionIntent intent = Make(1, Said("я стримлю уже 2 часа, и у меня RTX 3060"), owl);
            var chat = new StreamChat();
            chat.Add("b", "viewer.kritik228", "kritik228", "опять кринж", 1, 0, ReactionSource.LanguageModel);
            Assert.That(ChatOutputValidator.Validate("2 часа это сила", intent, chat.Messages, Situation(chat: chat.Messages)).Accepted, Is.True);
            Assert.That(ChatOutputValidator.Validate("3060 норм карта", intent, chat.Messages, Situation(chat: chat.Messages)).Accepted, Is.True);
            Assert.That(ChatOutputValidator.Validate("228 опять ноет", intent, chat.Messages, Situation(chat: chat.Messages)).Accepted, Is.True);
            Assert.That(ChatOutputValidator.Validate("rtx 4090 бы сюда", intent, chat.Messages, Situation(chat: chat.Messages)).Accepted, Is.False);
        }

        [Test]
        public void FactsKeepSpeakerRolesAndCurrentState()
        {
            ChatParticipant owl = Authored("viewer.nightowl");
            string silence = ChatContextBuilder.Build(Make(1, StreamEvent.StreamerSilence(1, "s", 600, 200, SilenceLevel.VeryLong), owl), Situation(), 64).User;
            Assert.That(silence, Does.Contain("still silent right now"), "silence continues; nobody 'finally spoke'");
            ChatParticipant kid = Authored("viewer.arcadekid");
            string microphone = ChatContextBuilder.Build(Make(1, StreamEvent.PeripheralChanged(1, "m", 600, PcPeripheralKind.Microphone, false), kid), Situation(), 64).User;
            Assert.That(microphone, Does.Contain("The streamer's microphone (theirs, not yours)"));
            string filler = ChatContextBuilder.Build(Make(1, Said("Так, ну, секунду, ага"), owl), Situation(), 64).User;
            Assert.That(filler, Does.Contain("Those were filler words; nothing notable happened."));
            var chat = new StreamChat();
            chat.Add("b", "viewer.x", "someone", "я вчера проиграл финал", 1, 0, ReactionSource.LanguageModel);
            string withChat = ChatContextBuilder.Build(Make(1, Said(FailurePhrase), owl), Situation(chat: chat.Messages), 64).User;
            Assert.That(withChat, Does.Contain("RECENT CHAT (other people's messages, oldest first)"));
            string gpu = ChatContextBuilder.Build(Make(1, Said("Чат, что думаете о видеокарте?"), owl), Situation(), 64).User;
            Assert.That(gpu, Does.Contain("You know of no purchase, upgrade or hardware change."));
        }

        [Test]
        public void CallbacksAreSparseRelevantAndRelationshipScaled()
        {
            ViewerProfile owlProfile = ViewerProfileTests.Profile("viewer.nightowl");
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(10);
            var community = new ViewerCommunity(new[] { owlProfile }, roster);
            community.BeginBroadcast("old", 1, 100, StreamTopic.Games);
            roster.Join(owlProfile.Participant());
            community.Process(StreamEvent.StreamerSpeech(1, "old", 30, SpeechRelevance.Analyze(
                ReactionFoundationTests.Recognized("Я впервые проиграл финал турнира", 1), community.KnownNames)).WithWitnesses(roster, 100));
            ViewerMemory memory = community.State(owlProfile.Id).Memories.Summary.Single();
            ChatParticipant owl = owlProfile.Participant();
            IReadOnlyList<ViewerMemory> Relevant(StreamEvent e) => community.State(owlProfile.Id).Memories.Retrieve(e, 1600);

            int chosen = 0;
            for (long id = 1; id <= 600; id++)
            {
                ReactionIntent intent = Make(id, Said("Чат, как лучше подготовиться к финалу турнира?", sequence: id), owl);
                IReadOnlyList<ViewerMemory> relevant = Relevant(intent.Event);
                Assert.That(relevant, Has.Count.EqualTo(1));
                bool callback = ViewerUtterancePlanner.ChooseCallback(intent, RelationshipTier.Neutral, relevant, null, out ViewerMemory picked, out _);
                var situation = new ChatSituation("c", 60, 10, ViewerLanguage.Russian, null, null, "Neutral.", callback ? new[] { picked } : null,
                    tier: RelationshipTier.Neutral, gameMinutes: 1600);
                ViewerUtterancePlan plan = ViewerUtterancePlanner.For(intent, situation);
                string prompt = ChatContextBuilder.Build(intent, situation, 64).User;
                if (callback)
                {
                    chosen++;
                    Assert.That(plan.Intent, Is.EqualTo(UtteranceIntent.Callback));
                    Assert.That(plan.RelevantMemoryId, Is.EqualTo(memory.MemoryId));
                    Assert.That(prompt, Does.Contain("MEMORY: ReportedFailure; subject=final; knowledge=HeardStreamer. Meaning: yesterday"));
                }
                else
                {
                    Assert.That(plan.Intent, Is.Not.EqualTo(UtteranceIntent.Callback));
                    Assert.That(prompt, Does.Not.Contain("MEMORY:"));
                    Assert.That(ChatOutputValidator.Validate("помню ты говорил что финал слил", intent, null, situation).Reason,
                        Is.EqualTo("unsupported historical claim"), "an unplanned callback cannot slip through");
                }
            }
            double chance = ViewerUtterancePlanner.CallbackChance(owl, RelationshipTier.Neutral);
            Assert.That(chance, Is.EqualTo(owlProfile.Habits.CallbackInterest).Within(1e-6));
            Assert.That(chosen / 600.0, Is.EqualTo(chance).Within(.07), "bounded, not every relevant moment");

            ReactionIntent unrelated = Make(1, Said("Чат, какую игру лучше запустить дальше?"), owl);
            Assert.That(Relevant(unrelated.Event), Is.Empty);
            Assert.That(ViewerUtterancePlanner.ChooseCallback(unrelated, RelationshipTier.Loyal, Relevant(unrelated.Event), null, out _, out _), Is.False);
            Assert.That(ViewerUtterancePlanner.CallbackChance(owl, RelationshipTier.Loyal), Is.GreaterThan(chance));
            Assert.That(ViewerUtterancePlanner.CallbackChance(owl, RelationshipTier.Wary), Is.LessThan(chance));
            var eager = new ViewerProfile { Id = "viewer.eager", DisplayName = "Eager", Personality = "x", Habits = new SocialHabits { CallbackInterest = 1 } };
            var eagerViewer = new ChatParticipant(eager.Id, eager.DisplayName, true, new ReactionTraits(.5f, 1, 1, StreamTopic.Games), null,
                new ViewerPersona(ViewerLanguage.Russian, "x", "y", 1, 5, profile: eager));
            Assert.That(ViewerUtterancePlanner.CallbackChance(eagerViewer, RelationshipTier.Loyal), Is.EqualTo(ViewerUtterancePlanner.CallbackCap));
        }

        [Test]
        public void RuntimeTracesCallbackCandidatesOnlyForTheWitness()
        {
            var profiles = new[] { ViewerCommunityTests.Profile(), ViewerCommunityTests.Profile(1) };
            foreach (var profile in profiles) profile.Schedule.Regularity = 0; // only the explicit joins below attend
            using var live = new LiveDesktop(20260926, profiles: profiles);
            ViewerCore core = live.State.Viewers;
            for (int seconds = 0; seconds < 300 && live.State.Stream.Audience.CurrentViewers < 2; seconds++) live.Tick(1);
            Assert.That(live.State.Stream.Audience.CurrentViewers, Is.GreaterThanOrEqualTo(2));
            core.Roster.SetAudienceSize(live.State.Stream.Audience.CurrentViewers);
            core.Roster.Join(profiles[0].Participant());
            Assert.That(live.Say("я впервые проиграл финал турнира"), Is.True);
            live.Tick(.1);
            core.Roster.Join(profiles[1].Participant());
            Assert.That(core.Community.State(profiles[0].Id).Memories.Summary, Has.Count.EqualTo(1));
            ChatSituation witness = core.SituationFor(ChatDirectorTests.Intent(profiles[0].Participant(), "Tester0 как подготовиться к финалу турнира?"));
            ChatSituation absent = core.SituationFor(ChatDirectorTests.Intent(profiles[1].Participant(), "Tester1 как подготовиться к финалу турнира?"));
            Assert.That(witness.CallbackCandidates, Does.Contain(core.Community.State(profiles[0].Id).Memories.Summary[0].MemoryId));
            Assert.That(witness.Memories.Count, Is.LessThanOrEqualTo(1), "one callback fact at most");
            Assert.That(absent.CallbackCandidates, Is.Null);
            Assert.That(absent.Memories, Is.Empty);
        }

        [Test]
        public void AuthoredSilenceKeepsViewersOutOfTopicsTheyIgnore()
        {
            ChatParticipant byteCat = Authored("viewer.bytecat");
            ChatParticipant mika = Authored("viewer.mika");
            int Picks(ChatParticipant viewer, string phrase)
            {
                int picks = 0;
                for (ulong seed = 1; seed <= 200; seed++)
                {
                    AudienceRoster roster = ReactionFoundationTests.Roster(3, viewer);
                    foreach (ReactionIntent intent in ReactionFoundationTests.Selector(roster, seed).Select(Said(phrase, sequence: (long)seed), 10, true, out _))
                        if (intent.Viewer == viewer) picks++;
                }
                return picks;
            }
            Assert.That(Picks(byteCat, "Чат, я сегодня так устал и не ел, соседи опять шумят!"), Is.Zero, "ByteCat is bored by apartment drama");
            Assert.That(Picks(byteCat, "Чат, у меня опять лагает комп!"), Is.GreaterThan(0));
            Assert.That(Picks(mika, "Чат, какую видеокарту купить, денег мало!"), Is.Zero);
            Assert.That(Picks(mika, "Чат, я сегодня так устал!"), Is.GreaterThan(0));
        }

        [Test]
        public void CollidingVoicesDifferInWhatTheyDoNotOnlyInWords()
        {
            ViewerCommunityCatalog catalog = ViewerProfileTests.Catalog();
            Assert.That(catalog.ValidationError, Is.Null);
            SocialHabits Of(string id) => catalog.Find(id).Habits;
            foreach (var (a, b) in new[] { ("viewer.nightowl", "viewer.kritik228"), ("viewer.mika", "viewer.zinaivanovna"),
                         ("viewer.mika", "viewer.pixelfox"), ("viewer.zinaivanovna", "viewer.pixelfox"), ("viewer.bytecat", "viewer.sovetnik") })
                Assert.That(Of(a).Prefers, Is.Not.EquivalentTo(Of(b).Prefers), a + " and " + b + " prefer different social actions");
            Assert.That(Of("viewer.nightowl").CallbackInterest, Is.GreaterThan(Of("viewer.kritik228").CallbackInterest), "NightOwl keeps score, kritik does not care");
            var invalid = ViewerProfileTests.Profile("viewer.mika");
            var copy = UnityEngine.JsonUtility.FromJson<ViewerProfile>(UnityEngine.JsonUtility.ToJson(invalid));
            copy.Habits.Prefers = new[] { UtteranceIntent.Callback };
            Assert.That(copy.Validate(), Is.Not.Null, "callbacks come from memory, never from a habit");
            copy.Habits.Prefers = new[] { UtteranceIntent.Question };
            Assert.That(copy.Validate(), Is.Not.Null, "a habit cannot both prefer and avoid an action");
        }

        [Test]
        public void PlannedCallbacksMayRecallHeardFactsNaturallyButNeverAsSeen()
        {
            ViewerProfile owlProfile = ViewerProfileTests.Profile("viewer.nightowl");
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(10);
            var community = new ViewerCommunity(new[] { owlProfile }, roster);
            community.BeginBroadcast("old", 1, 100, StreamTopic.Games);
            roster.Join(owlProfile.Participant());
            community.Process(StreamEvent.StreamerSpeech(1, "old", 30, SpeechRelevance.Analyze(
                ReactionFoundationTests.Recognized("Я впервые проиграл финал турнира", 1), community.KnownNames)).WithWitnesses(roster, 100));
            ViewerMemory memory = community.State(owlProfile.Id).Memories.Summary.Single();
            ChatParticipant owl = owlProfile.Participant();
            var planned = new ChatSituation("c", 60, 10, ViewerLanguage.Russian, null, null, "Neutral.", new[] { memory }, gameMinutes: 1600);
            ReactionIntent final = Make(1, Said("Чат, я снова в финале турнира. Как думаете, в этот раз получится?"), owl);
            foreach (string text in new[] { "а ты вчера как проиграл-то", "а вот и снова финал, как вчера", "в прошлый раз не повезло" })
                Assert.That(ChatOutputValidator.Validate(text, final, null, planned).Accepted, Is.True, text);
            Assert.That(ChatOutputValidator.Validate("видел как ты вчера проиграл", final, null, planned).Reason, Is.EqualTo("unsupported historical claim"),
                "heard, not seen");
            Assert.That(ChatOutputValidator.Validate("помню вчера ты выиграл финал", final, null, planned).Accepted, Is.False, "the known outcome cannot flip");
            Assert.That(ChatOutputValidator.Validate("в прошлый раз видюха сгорела", final, null, planned).Accepted, Is.False, "another subject is not this memory");
            ReactionIntent unrelated = Make(2, Said("Чат, какую игру лучше запустить дальше?"), owl);
            Assert.That(ChatOutputValidator.Validate("в прошлый раз не повезло", unrelated, null, planned).Accepted, Is.False,
                "without the subject in the moment or the line, a vague past claim is unsupported");

            var ledgerRoster = new AudienceRoster(EphemeralViewers.Create); ledgerRoster.SetAudienceSize(5);
            ledgerRoster.Join(owl);
            var ledger = new ViewerPromiseLedger();
            ledger.Add("p", ViewerPromiseVocabulary.Parse("я завтра куплю видеокарту", 100, 0), EventWitnesses.Capture(ledgerRoster));
            ViewerPromiseContext promise = ledger.Retrieve(owl.ViewerId, "gpu", 200);
            Assert.That(ViewerPromiseVocabulary.References("уже давно обещал карту?", promise, "gpu"), Is.True, "the moment names the GPU");
            Assert.That(ViewerPromiseVocabulary.References("уже давно обещал карту?", promise), Is.False, "nothing names the GPU");
        }

        [Test]
        public void PlanFactsAnswerTheAuditedGaps()
        {
            ChatParticipant sovetnik = Authored("viewer.sovetnik");
            string chatQuestion = ChatContextBuilder.Build(Make(1, Said("Чат, как думаете, какую игру лучше запустить дальше?"), sovetnik), Situation(), 64).User;
            Assert.That(chatQuestion, Does.Contain("No picture, sound, PC or settings problem has been reported"), "no problem to diagnose");
            string setup = ChatContextBuilder.Build(Make(2, Said("Чат, у меня опять лагает комп"), sovetnik), Situation(), 64).User;
            Assert.That(setup, Does.Not.Contain("No picture, sound, PC or settings problem"), "the streamer reported one");
            Assert.That(setup, Does.Contain("The streamer is talking about themselves: it happened to them, not to you."));
            var answer = ViewerUtterancePlanner.For(Make(1, Said("Чат, как думаете, какую игру лучше запустить дальше?"), sovetnik), Situation());
            Assert.That(answer.GameChoice, Is.True);
            if (answer.Intent == UtteranceIntent.Answer)
                Assert.That(chatQuestion, Does.Contain("a kind of game, never a specific title"));
            ChatParticipant fox = Authored("viewer.pixelfox");
            Assert.That(ChatContextBuilder.Build(Make(3, Said("спасибо PixelFox", fox), fox, true), Situation(), 64).User,
                Does.Contain("They are thanking you by name; what for is not said."));
            ReactionIntent gpu = Make(4, Said("Чат, что думаете о видеокарте?"), fox);
            Assert.That(ChatContextBuilder.Build(gpu, Situation(), 64).User, Does.Contain("You know of no purchase, upgrade or hardware change."));
            var knowsPromise = new ChatSituation("c", 60, 10, ViewerLanguage.Russian, null, null, "Neutral.", callbackCandidates: "promise.0000000000000001");
            Assert.That(ChatContextBuilder.Build(gpu, knowsPromise, 64).User, Does.Not.Contain("You know of no purchase"),
                "a promise witness is never told they know of no purchase");
        }

        [Test]
        public void ElapsedTimeFromTheFactsIsNotAHistoryClaim()
        {
            ChatParticipant jonas = Authored("viewer.jonas");
            ReactionIntent silence = Make(1, StreamEvent.StreamerSilence(1, "s", 600, 200, SilenceLevel.VeryLong), jonas);
            Assert.That(ChatOutputValidator.Validate("Прошло уже три минуты без слов... всё в порядке?", silence, null, Situation()).Accepted, Is.True,
                "grounded: silent for about 3 minutes");
            Assert.That(ChatOutputValidator.Validate("Прошло уже двадцать минут без слов", silence, null, Situation()).Reason, Does.StartWith("ungrounded specific claim"));
            Assert.That(ChatOutputValidator.Validate("в прошлый раз было так же", silence, null, Situation()).Reason, Is.EqualTo("unsupported historical claim"));
        }

        [Test]
        public void AgainNeedsTheStreamersWordsChatOrAPlannedCallback()
        {
            ChatParticipant mika = Authored("viewer.mika");
            ReactionIntent microphone = Make(1, StreamEvent.PeripheralChanged(1, "m", 600, PcPeripheralKind.Microphone, false), mika);
            Assert.That(ChatOutputValidator.Validate("опять с микрофоном", microphone, null, Situation()).Reason, Is.EqualTo("unsupported recurrence"));
            Assert.That(ChatOutputValidator.Validate("опять с микрофоном", microphone, null).Accepted, Is.True, "legacy validation without a plan is unchanged");
            var chat = new StreamChat();
            chat.Add("b", "viewer.x", "someone", "микрофон снова отвалился", 1, 0, ReactionSource.LanguageModel);
            Assert.That(ChatOutputValidator.Validate("опять с микрофоном", microphone, chat.Messages, Situation(chat: chat.Messages)).Accepted, Is.True, "visible chat said it");
            ChatParticipant owl = Authored("viewer.nightowl");
            Assert.That(ChatOutputValidator.Validate("ну ты опять", Make(2, Said(FailurePhrase), owl), null, Situation()).Accepted, Is.True, "the streamer said опять");
            Assert.That(ChatOutputValidator.Validate("попробуй снова", Make(3, Said("Блин, я проиграл"), owl), null, Situation()).Accepted, Is.True, "a suggestion, not a claim");
            Assert.That(ChatOutputValidator.Validate("снова проиграл", Make(4, Said("Блин, я проиграл"), owl), null, Situation()).Reason, Is.EqualTo("unsupported recurrence"));
        }

        [TestCase("viewer.arcadekid", "last time you lost the final lol")]
        [TestCase("viewer.nightowl", "на днях ты тоже слил")]
        public void HistoricalClaimsWithoutAPlannedCallbackAreRejected(string id, string text)
        {
            ChatParticipant viewer = Authored(id);
            ReactionIntent intent = Make(1, Said(FailurePhrase), viewer);
            Assert.That(ChatOutputValidator.Validate(text, intent, null, Situation()).Reason, Is.EqualTo("unsupported historical claim"));
        }
    }
}
