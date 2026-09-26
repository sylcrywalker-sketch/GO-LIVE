using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Desktop;
using GoLive.Viewers;
using GoLive.PcBuilding;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ViewerMemoryTests
    {
        private static (ViewerCommunity community, AudienceRoster roster, ViewerProfile first, ViewerProfile second) World()
        {
            var a = ViewerCommunityTests.Profile(); var b = ViewerCommunityTests.Profile(1);
            a.Interests |= StreamTopic.Hardware | StreamTopic.StreamSetup;
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(10);
            var community = new ViewerCommunity(new[] { a, b }, roster);
            community.BeginBroadcast("memory", 1, 100, StreamTopic.Games);
            roster.Join(a.Participant());
            return (community, roster, a, b);
        }

        private static StreamEvent Fact(AudienceRoster roster, long sequence, string text, double minute = 100)
            => StreamEvent.StreamerSpeech(sequence, "memory", sequence,
                SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(text, sequence), roster.NamesForMentions())).WithWitnesses(roster, minute);

        private static IReadOnlyList<ViewerMemory> Reserve(ViewerMemoryBank bank, StreamEvent current, double now, long id) =>
            (IReadOnlyList<ViewerMemory>)typeof(ViewerMemoryBank).GetMethod("Reserve", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bank, new object[] { current, now, id });
        private static void Finish(ViewerMemoryBank bank, long id, IReadOnlyList<ViewerMemory> memories, string text, double now) =>
            typeof(ViewerMemoryBank).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(bank, new object[] { id, memories, text, now });

        [Test]
        public void PermanentStateOwnsBoundedStructuredMemory()
        {
            Assert.That(typeof(PermanentViewerState).GetProperty("Memories"), Is.Not.Null,
                "Durable memory must be owned by permanent C# viewer state.");
        }

        [Test]
        public void SameTimestampLateJoinCannotReadEarlierSpeechOrChat()
        {
            using var live = new LiveDesktop(20260926);
            ViewerCore core = live.State.Viewers;
            core.Roster.SetAudienceSize(10);
            live.Say("я впервые проиграл финал турнира");
            core.Chat.Add(live.State.Stream.BroadcastId, "other", "Other", "секрет про старый турнир", 0, 0, ReactionSource.LanguageModel);
            var viewer = ChatDirectorTests.Viewer("viewer.late", "Late");
            core.Roster.Join(viewer, core.Events.Now);
            ChatSituation context = core.SituationFor(ChatDirectorTests.Intent(viewer, "Late привет"));
            Assert.That(context.RecentSpeech, Is.Null);
            Assert.That(context.RecentChat, Is.Empty);
        }

        [TestCase("помню вчера ты проиграл")]
        [TestCase("ты вчера не это говорил?")]
        public void UnsubstantiatedHistoricalClaimIsRejected(string text)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            var intent = ChatDirectorTests.Intent(viewer, "Alpha привет");
            Assert.That(ChatOutputValidator.Validate(text, intent, Array.Empty<StreamChatMessage>()).Accepted, Is.False);
        }

        [Test]
        public void PresentWitnessRemembersAfterLeavingButAbsentAndLateViewerDoNot()
        {
            var (community, roster, a, b) = World();
            var fact = Fact(roster, 1, "я впервые проиграл финал турнира");
            roster.Leave(a.Id); roster.Join(b.Participant(), 1);
            community.Process(fact);
            Assert.That(community.State(a.Id).Memories.Summary, Has.Count.EqualTo(1));
            Assert.That(community.State(b.Id).Memories.Summary, Is.Empty);
            var memory = community.State(a.Id).Memories.Summary.Single();
            Assert.That(memory.KnowledgeSource, Is.EqualTo(MemoryKnowledgeSource.HeardStreamer));
            Assert.That(memory.Kind, Is.EqualTo(ViewerMemoryKind.ReportedFailure));
            Assert.That(memory.CanonicalSubject, Is.EqualTo("final"));
            Assert.That(memory.CreatedGameMinutes, Is.EqualTo(100));
            community.Process(fact);
            Assert.That(community.State(a.Id).Memories.Summary, Has.Count.EqualTo(1));
        }

        [Test]
        public void ProductionSourceCapturesBeforeDrainAndCannotRestamp()
        {
            var profile = ViewerCommunityTests.Profile();
            using var live = new LiveDesktop(20260926, profiles: new[] { profile });
            var core = live.State.Viewers;
            core.Roster.Join(profile.Participant());
            Assert.That(live.Say("я впервые проиграл финал турнира"), Is.True);
            core.Roster.Leave(profile.Id);
            var events = new List<StreamEvent>(); core.Events.Drain(events);
            var fact = events.Single(e => e.Kind == StreamEventKind.StreamerSpeech);
            Assert.That(fact.HasWitnesses, Is.True);
            Assert.That(fact.WitnessedBy(profile.Id), Is.True);
            Assert.That(fact.WithWitnesses(core.Roster, 999), Is.SameAs(fact));
            core.Community.Process(fact);
            Assert.That(core.Community.State(profile.Id).Memories.Summary, Has.Count.EqualTo(1));
        }

        [Test]
        public void SelectionExcludesLateAndReturningNamedViewer()
        {
            var (_, roster, a, b) = World();
            var fact = Fact(roster, 1, "Tester0 Tester1 я впервые проиграл финал турнира");
            roster.Leave(a.Id); roster.Join(a.Participant(), 1); roster.Join(b.Participant(), 1);
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var intents = ReactionFoundationTests.Selector(roster, seed).Select(fact, 2, true, out _);
                Assert.That(intents.Any(i => i.Viewer.IsPermanent), Is.False);
            }
        }

        [Test]
        public void PersonalThanksOnlyBelongToIntendedWitness()
        {
            var (community, roster, a, b) = World(); roster.Join(b.Participant());
            community.Process(Fact(roster, 1, "Tester0 спасибо"));
            Assert.That(community.State(a.Id).Memories.Summary.Single().Kind, Is.EqualTo(ViewerMemoryKind.PersonalAcknowledgement));
            Assert.That(community.State(b.Id).Memories.Summary, Is.Empty);
        }

        [TestCase("ну так")]
        [TestCase("я убил противника")]
        [TestCase("я проиграл")]
        [TestCase("если я впервые выиграю финал")]
        [TestCase("завтра я выиграю финал турнира")]
        [TestCase("я впервые не проиграл финал турнира")]
        [TestCase("он сказал я впервые выиграл финал турнира")]
        [TestCase("he said i finally won the final")]
        [TestCase("i finally found a wonderful game")]
        public void OrdinaryOrHypotheticalSpeechCreatesNoMemory(string speech)
        {
            var (community, roster, a, _) = World(); community.Process(Fact(roster, 1, speech));
            Assert.That(community.State(a.Id).Memories.Summary, Is.Empty);
        }

        [Test]
        public void TechnicalFirstIncidentRequiresInterestAndLowImportanceDecays()
        {
            var (community, roster, a, b) = World(); roster.Join(b.Participant());
            for (int i = 1; i < 4; i++) community.Process(StreamEvent.PeripheralChanged(i, "tech" + i, i, PcPeripheralKind.Microphone, false).WithWitnesses(roster, 100));
            Assert.That(community.State(a.Id).Memories.Summary, Has.Count.EqualTo(1));
            Assert.That(community.State(a.Id).Memories.Summary[0].KnowledgeSource, Is.EqualTo(MemoryKnowledgeSource.Witnessed));
            Assert.That(community.State(b.Id).Memories.Summary, Is.Empty);
            community.Process(Fact(roster, 5, "я наконец выиграл финал турнира"));
            community.UpdateContext(100 + 7 * 1440, StreamTopic.Games);
            Assert.That(community.State(a.Id).Memories.Summary.Single().Kind, Is.EqualTo(ViewerMemoryKind.ReportedAchievement));
        }

        [Test]
        public void ReferencedLowImportanceMemoryOutlivesBaseExpiryButHasFiniteCeiling()
        {
            var (community, roster, a, _) = World();
            community.Process(StreamEvent.PeripheralChanged(1, "technical.first", 1, PcPeripheralKind.Microphone, false).WithWitnesses(roster, 100));
            var bank = community.State(a.Id).Memories;
            var current = Fact(roster, 2, "как микрофон");
            double day = 1440;
            var selected = Reserve(bank, current, 100 + 6 * day, 1);
            Finish(bank, 1, selected, "помню твой микрофон", 100 + 6 * day);
            bank.Decay(100 + 7 * day);
            Assert.That(bank.Summary, Has.Count.EqualTo(1), "A referenced fact survives its original seven-day expiry.");
            selected = Reserve(bank, current, 100 + 12 * day, 2);
            Finish(bank, 2, selected, "помню твой микрофон", 100 + 12 * day);
            bank.Decay(100 + 14 * day - 1);
            Assert.That(bank.Summary, Has.Count.EqualTo(1));
            bank.Decay(100 + 14 * day);
            Assert.That(bank.Summary, Is.Empty, "References cannot extend a fact beyond twice its base lifetime.");
        }

        [Test]
        public void EnglishFinallyDoesNotMisclassifyBossAsFinal()
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "i finally beat a boss"));
            Assert.That(community.State(a.Id).Memories.Summary.Single().CanonicalSubject, Is.EqualTo("boss"));
        }

        [Test]
        public void CapacityEvictsDeterministicallyAndRetrievalIsRelevantBoundedAndExcludesCurrentFact()
        {
            var (community, roster, a, _) = World();
            for (int i = 1; i <= 20; i++) community.Process(Fact(roster, i, "я впервые проиграл финал турнира", i));
            var bank = community.State(a.Id).Memories;
            Assert.That(bank.Summary, Has.Count.EqualTo(ViewerMemoryBank.Capacity));
            Assert.That(bank.Summary.Min(m => m.CreatedGameMinutes), Is.EqualTo(5));
            var current = Fact(roster, 20, "что думаете про финал турнира", 20);
            var memories = bank.Retrieve(current, 20);
            Assert.That(memories, Has.Count.EqualTo(2));
            Assert.That(memories.All(m => m.EventKey != current.Key), Is.True);
            Assert.That(memories[0].CreatedGameMinutes, Is.EqualTo(19));
            Assert.That(bank.Retrieve(Fact(roster, 21, "как микрофон", 21), 21), Is.Empty);
        }

        [Test]
        public void MemorySaveIsDetachedRepeatableAndLegacyOptional()
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "я впервые проиграл финал турнира"));
            var saved = community.Capture();
            community.Restore(saved); community.Restore(saved);
            Assert.That(community.State(a.Id).Memories.Summary, Has.Count.EqualTo(1));
            saved.Viewers[0].Memories[0].Importance = 1;
            Assert.That(community.State(a.Id).Memories.Summary[0].Importance, Is.EqualTo(3));
            saved.Viewers[0].Memories = null; community.Restore(saved);
            Assert.That(community.State(a.Id).Memories.Summary, Is.Empty);
        }

        [TestCase("null")]
        [TestCase("duplicate")]
        [TestCase("nan")]
        [TestCase("kind")]
        [TestCase("source")]
        [TestCase("subject")]
        [TestCase("count")]
        [TestCase("importance")]
        [TestCase("topics")]
        [TestCase("capacity")]
        public void InvalidMemoryGraphRejectsRestoreAtomically(string invalid)
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "я впервые проиграл финал турнира"));
            var saved = community.Capture(); var rows = saved.Viewers[0].Memories; var row = rows[0];
            switch (invalid)
            {
                case "null": rows.Add(null); break;
                case "duplicate": rows.Add(row); break;
                case "nan": row.CreatedGameMinutes = double.NaN; break;
                case "kind": row.Kind = (ViewerMemoryKind)123; break;
                case "source": row.KnowledgeSource = (MemoryKnowledgeSource)123; break;
                case "subject": row.CanonicalSubject = new string('x', 65); break;
                case "count": row.ReferenceCount = 4; break;
                case "importance": row.Importance = 0; break;
                case "topics": row.Topics = (StreamTopic)128; break;
                case "capacity": for (int i = 0; i < 16; i++) rows.Add(row); break;
            }
            Assert.That(community.Validate(saved), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => community.Restore(saved));
            Assert.That(community.State(a.Id).Memories.Summary, Has.Count.EqualTo(1));
            Assert.That(roster.IsWatching(a.Id), Is.True);
        }

        [Test]
        public void HeardFactPromptLabelsKnowledgeAndModelCannotCreateHistoricalTruth()
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "я впервые проиграл финал турнира"));
            var current = Fact(roster, 2, "Tester0 что думаешь про финал турнира");
            var intent = ChatDirectorTests.Intent(a.Participant(), "Tester0 что думаешь про финал турнира");
            var memories = community.State(a.Id).Memories.Retrieve(current, 100);
            var situation = new ChatSituation("channel", 10, 10, ViewerLanguage.Russian, null, null, memories: memories);
            Assert.That(ChatContextBuilder.Build(intent, situation, 100).User, Does.Contain("knowledge=HeardStreamer"));
            // Social quality pass: a heard fact may be recalled (it is what the streamer said), never claimed as seen.
            Assert.That(ChatOutputValidator.Validate("видел как ты вчера слил финал", intent, null, situation).Accepted, Is.False);
            Assert.That(ChatOutputValidator.Validate("помню твой финал", intent, null, situation).Accepted, Is.True);
            Assert.That(ChatOutputValidator.Validate("помню ты говорил про финал", intent, null, situation).Accepted, Is.True);
            var before = community.Capture().Viewers[0].Memories[0];
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "помню вчера сгорела видеокарта", .1));
            var chat = new StreamChat();
            using var director = new ChatDirector(model, new ChatModelSettings(), chat, new ReactionLog());
            director.Submit(intent); director.Update(0, roster, () => situation, "b");
            director.Update(intent.DueSeconds + .1, roster, () => situation, "b");
            Assert.That(director.Stats.Rejected, Is.GreaterThan(0));
            Assert.That(community.State(a.Id).Memories.Summary.Single().MemoryId, Is.EqualTo(before.MemoryId));
            Assert.That(community.State(a.Id).Memories.Summary.Single().ReferenceCount, Is.Zero);
        }

        [Test]
        public void ReservationsFailureUnrelatedPublicationCooldownAndLifetimeLimit()
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "я впервые проиграл финал турнира"));
            var bank = community.State(a.Id).Memories;
            var current = Fact(roster, 2, "что думаешь про финал турнира");
            var first = Reserve(bank, current, 100, 1);
            Assert.That(first, Has.Count.EqualTo(1));
            Assert.That(Reserve(bank, current, 100, 2), Is.Empty, "Concurrent work cannot reuse the same callback.");
            Finish(bank, 1, first, null, 100);
            Assert.That(bank.Summary[0].ReferenceCount, Is.Zero, "Failure releases without consuming.");
            first = Reserve(bank, current, 100, 3);
            Finish(bank, 3, first, "интересный финал", 100);
            Assert.That(bank.Summary[0].ReferenceCount, Is.Zero, "Related current-moment response is not a callback.");
            for (int i = 0; i < 3; i++)
            {
                double at = 100 + i * 120;
                first = Reserve(bank, current, at, 10 + i);
                Assert.That(first, Has.Count.EqualTo(1));
                Finish(bank, 10 + i, first, "помню ты говорил про финал", at);
                Assert.That(bank.Retrieve(current, at + 119), Is.Empty);
            }
            Assert.That(bank.Summary[0].ReferenceCount, Is.EqualTo(3));
            Assert.That(bank.Retrieve(current, 1000), Is.Empty);
        }

        [Test]
        public void FailureCallbackDoesNotConsumeUnrelatedAchievementOnSameSubject()
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "я впервые проиграл финал турнира", 100));
            community.Process(Fact(roster, 2, "я наконец выиграл финал турнира", 101));
            var bank = community.State(a.Id).Memories;
            var chosen = Reserve(bank, Fact(roster, 3, "что думаешь про финал турнира", 102), 102, 1);
            Assert.That(chosen, Has.Count.EqualTo(2));
            Assert.That(chosen[0].Kind, Is.EqualTo(ViewerMemoryKind.ReportedAchievement));
            Finish(bank, 1, chosen, "помню ты говорил что проиграл финал", 102);
            Assert.That(bank.Summary.Single(m => m.Kind == ViewerMemoryKind.ReportedFailure).ReferenceCount, Is.EqualTo(1));
            Assert.That(bank.Summary.Single(m => m.Kind == ViewerMemoryKind.ReportedAchievement).ReferenceCount, Is.Zero);
            Assert.That(bank.Summary.Single(m => m.Kind == ViewerMemoryKind.ReportedAchievement).LastReferencedGameMinutes, Is.Zero);
        }

        [Test]
        public void AmbiguousSubjectOnlyCallbackConsumesAtMostOneChosenFact()
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "я впервые проиграл финал турнира", 100));
            community.Process(Fact(roster, 2, "я наконец выиграл финал турнира", 101));
            var bank = community.State(a.Id).Memories;
            var chosen = Reserve(bank, Fact(roster, 3, "что думаешь про финал турнира", 102), 102, 1);
            Finish(bank, 1, chosen, "помню ты говорил про финал", 102);
            Assert.That(bank.Summary.Sum(m => m.ReferenceCount), Is.EqualTo(1));
        }

        [TestCase("я впервые проиграл финал турнира", "помню ты говорил что выиграл финал")]
        [TestCase("я наконец выиграл финал турнира", "помню ты говорил что проиграл финал")]
        public void HistoricalClaimCannotReverseTheKnownOutcome(string report, string callback)
        {
            var (community, roster, a, _) = World(); community.Process(Fact(roster, 1, report));
            var current = Fact(roster, 2, "Tester0 что думаешь про финал турнира");
            var intent = ChatDirectorTests.Intent(a.Participant(), "Tester0 что думаешь про финал турнира");
            var memories = community.State(a.Id).Memories.Retrieve(current, 100);
            var situation = new ChatSituation("channel", 1, 10, ViewerLanguage.Russian, null, null, memories: memories);
            Assert.That(ChatOutputValidator.Validate(callback, intent, null, situation).Accepted, Is.False);
        }

        [Test]
        public void DirectorFailureAndCancellationReleaseChosenMemoryWithoutConsuming()
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "я впервые проиграл финал турнира"));
            var bank = community.State(a.Id).Memories;
            var current = Fact(roster, 2, "Tester0 что думаешь про финал турнира");
            var intent = ChatDirectorTests.Intent(a.Participant(), "Tester0 что думаешь про финал турнира");
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Unavailable, null, .1));
            using var director = new ChatDirector(model, new ChatModelSettings(), new StreamChat(), new ReactionLog());
            director.Finished += (i, s, text) => Finish(bank, i.Id, s?.Memories, text, 100);
            ChatSituation Context() => new("channel", 1, 10, ViewerLanguage.Russian, null, null, memories: Reserve(bank, current, 100, intent.Id));
            director.Submit(intent); director.Update(0, roster, Context, "b");
            Assert.That(bank.Retrieve(current, 100), Is.Empty);
            director.Update(intent.DueSeconds + .1, roster, Context, "b");
            Assert.That(director.Stats.Unavailable, Is.EqualTo(1));
            Assert.That(bank.Summary[0].ReferenceCount, Is.Zero);
            Assert.That(bank.Retrieve(current, 100), Has.Count.EqualTo(1));
            director.Submit(intent); director.Update(0, roster, Context, "b"); director.CancelAll("test");
            Assert.That(bank.Retrieve(current, 100), Has.Count.EqualTo(1));
        }

        [Test]
        public void SuccessfulRelevantDirectorPublicationConsumesOneReference()
        {
            var (community, roster, a, _) = World();
            community.Process(Fact(roster, 1, "я впервые проиграл финал турнира"));
            var bank = community.State(a.Id).Memories;
            var current = Fact(roster, 2, "Tester0 что думаешь про финал турнира");
            var intent = ChatDirectorTests.Intent(a.Participant(), "Tester0 что думаешь про финал турнира");
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "помню ты говорил про финал", .1));
            var chat = new StreamChat();
            using var director = new ChatDirector(model, new ChatModelSettings(), chat, new ReactionLog());
            director.Finished += (i, s, text) => Finish(bank, i.Id, s?.Memories, text, 100);
            ChatSituation Context() => new("channel", 1, 10, ViewerLanguage.Russian, null, null, memories: Reserve(bank, current, 100, intent.Id));
            director.Submit(intent); director.Update(0, roster, Context, "b");
            Assert.That(bank.Summary[0].ReferenceCount, Is.Zero);
            director.Update(intent.DueSeconds + .1, roster, Context, "b");
            Assert.That(chat.Messages.Single().Source, Is.EqualTo(ReactionSource.LanguageModel));
            Assert.That(bank.Summary[0].ReferenceCount, Is.EqualTo(1));
            Assert.That(bank.Retrieve(current, 219), Is.Empty);
            Assert.That(bank.Retrieve(current, 220), Has.Count.EqualTo(1));
        }

        [Test]
        public void InvalidMemoryRejectsEntireDesktopGraphBeforeAnyMutation()
        {
            var profile = ViewerCommunityTests.Profile();
            using var state = new DesktopState(LiveDesktop.Apps(), viewerProfiles: new[] { profile });
            state.Outline.CreateAddress("original"); state.Windows.Open(DesktopAppId.Hub);
            var snapshot = state.Capture();
            snapshot.ViewerCommunity.Viewers[0].Memories.Add(null);
            Assert.Throws<ArgumentException>(() => state.Restore(snapshot, Array.Empty<DesktopDrive>()));
            Assert.That(state.Outline.Address, Is.EqualTo("original@outline.local"));
            Assert.That(state.Windows.Windows, Has.Count.EqualTo(1));
            Assert.That(state.RestoreGeneration, Is.Zero);
        }

        [Test]
        public void ExistingVisitCanReadObservedChatButReturnVisitCannot()
        {
            using var live = new LiveDesktop(20260926);
            var core = live.State.Viewers; var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            core.Roster.Join(viewer, core.Events.Now);
            core.Chat.Add(live.State.Stream.BroadcastId, "other", "Other", "услышанная реплика", core.Events.Now, 0, ReactionSource.LanguageModel);
            var intent = ChatDirectorTests.Intent(viewer, "Alpha привет");
            Assert.That(core.SituationFor(intent).RecentChat, Has.Count.EqualTo(1));
            core.Roster.Leave(viewer.ViewerId); core.Roster.Join(viewer, core.Events.Now);
            Assert.That(core.SituationFor(intent).RecentChat, Is.Empty);
        }
    }
}
