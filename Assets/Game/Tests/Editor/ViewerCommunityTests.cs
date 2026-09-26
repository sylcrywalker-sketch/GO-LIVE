using System;
using System.Linq;
using GoLive.Desktop;
using GoLive.Viewers;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ViewerCommunityTests
    {
        private static ViewerCommunity Community(AudienceRoster roster, params ViewerProfile[] profiles) => new(profiles, roster);
        internal static ViewerProfile Profile(int index = 0, int sentiment = 0) => new()
        {
            Id = "viewer.test" + index, DisplayName = "Tester" + index, Personality = "A gentle, reserved viewer.",
            InitialSentiment = sentiment, Interests = StreamTopic.Games,
            Schedule = new ScheduleTendency { StartHour = 18, EndHour = 23, Regularity = .8f },
            EventAffinity = Enumerable.Repeat(1f, ViewerProfile.AffinityKinds.Count).ToArray()
        };
        private static AudienceRoster Roster(int size = 100)
        {
            var roster = new AudienceRoster(EphemeralViewers.Create);
            roster.SetAudienceSize(size);
            return roster;
        }
        private static void Begin(ViewerCommunity community, ulong seed = 1, double minute = 1200, StreamTopic topic = StreamTopic.Games) =>
            community.BeginBroadcast("b" + seed, seed, minute, topic);
        private static StreamEvent Speech(AudienceRoster roster, string text, double at) => StreamEvent.StreamerSpeech(
            (long)at, "b", at, SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(text, (long)at), roster.NamesForMentions()));

        [Test]
        public void PresenceArrivesGraduallyLeavesAndCanReturnNextBroadcast()
        {
            var profiles = Enumerable.Range(0, 80).Select(i => Profile(i)).ToArray();
            var roster = Roster(); var community = Community(roster, profiles);
            Begin(community); community.Tick(0d);
            Assert.That(roster.Named.Count, Is.LessThan(profiles.Length));
            for (int t = 1; t <= 100; t++) community.Tick((double)t);
            var first = roster.Named.Select(p => p.ViewerId).ToArray();
            Assert.That(first.Length, Is.InRange(1, profiles.Length - 1));
            community.Tick(3600d);
            Assert.That(roster.Named, Is.Empty, "Every attendance has finite dwell.");
            community.EndBroadcast(); roster.SetAudienceSize(100);
            Begin(community, 2); community.Tick(100d);
            Assert.That(roster.Named.Any(p => first.Contains(p.ViewerId)), Is.True);
            Assert.That(roster.Named.Any(p => community.State(p.ViewerId).VisitCount == 2), Is.True);
        }

        [Test]
        public void ScheduleInterestsAndRelationshipInfluenceFixedSeedAttendance()
        {
            int Count(int sentiment, double minute, StreamTopic topic)
            {
                int count = 0;
                for (ulong seed = 1; seed <= 150; seed++)
                {
                    var roster = Roster(); var community = Community(roster, Profile(sentiment: sentiment));
                    Begin(community, seed, minute, topic); community.Tick(100d); count += roster.Named.Count;
                }
                return count;
            }
            int baseline = Count(0, 1200, StreamTopic.Games);
            Assert.That(baseline, Is.GreaterThan(Count(0, 600, StreamTopic.Games)));
            Assert.That(baseline, Is.GreaterThan(Count(0, 1200, StreamTopic.Money)));
            Assert.That(Count(100, 1200, StreamTopic.Games), Is.GreaterThan(Count(-100, 1200, StreamTopic.Games)));
            Assert.That(Count(0, 1200, StreamTopic.Games), Is.EqualTo(baseline));
        }

        [Test]
        public void ExtraFramesCannotRerollAttendance()
        {
            var profiles = Enumerable.Range(0, 40).Select(i => Profile(i)).ToArray();
            var fine = Roster(); var coarse = Roster();
            ViewerCommunity a = Community(fine, profiles), b = Community(coarse, profiles); Begin(a); Begin(b);
            for (int t = 0; t <= 1000; t++) a.Tick(t / 10d);
            b.Tick(100d);
            Assert.That(fine.Named.Select(v => v.ViewerId), Is.EquivalentTo(coarse.Named.Select(v => v.ViewerId)));
        }

        [Test]
        public void OnlyPresentIntendedViewerChangesWithCooldownAndConservativeHostility()
        {
            var roster = Roster(); var p = Profile(); var other = Profile(1);
            ViewerCommunity community = Community(roster, p, other); Begin(community);
            roster.Join(p.Participant()); roster.Join(other.Participant());
            community.Process(Speech(roster, "Tester0 спасибо", 10));
            Assert.That(community.State(p.Id).Sentiment, Is.EqualTo(3));
            Assert.That(community.State(other.Id).Sentiment, Is.Zero);
            community.Process(Speech(roster, "Tester0 спасибо", 11));
            Assert.That(community.State(p.Id).Sentiment, Is.EqualTo(3));
            community.Process(Speech(roster, "Tester0 игра ужасная", 100));
            Assert.That(community.State(p.Id).Sentiment, Is.EqualTo(4), "Frustration at a game is only an acknowledgement.");
            community.Process(Speech(roster, "Tester0 ты идиот", 200));
            Assert.That(community.State(p.Id).Sentiment, Is.Zero);
            roster.Leave(p.Id);
            community.Process(StreamEvent.StreamerSpeech(300, "b", 300,
                SpeechRelevance.Analyze(ReactionFoundationTests.Recognized("Tester0 спасибо", 300), new[] { new ViewerNameForms(p.Id, p.Participant().NameForms) })));
            Assert.That(community.State(p.Id).Sentiment, Is.Zero);
        }

        [Test]
        public void CaptureIsDetachedInvalidRestoreIsAtomicAndLegacyResetsDefaults()
        {
            var roster = Roster(); var profile = Profile(sentiment: 7); ViewerCommunity community = Community(roster, profile);
            Begin(community); roster.Join(profile.Participant());
            community.Process(Speech(roster, "Tester0 спасибо", 10));
            ViewerCommunitySnapshot snapshot = community.Capture();
            var rows = snapshot.Viewers;
            var row = rows[0]; row.Sentiment = 99;
            Assert.That(community.State(profile.Id).Sentiment, Is.EqualTo(10));
            rows.Add(row);
            Assert.That(community.Validate(snapshot), Is.Not.Null);
            Assert.Throws<ArgumentException>(() => community.Restore(snapshot));
            Assert.That(community.State(profile.Id).Sentiment, Is.EqualTo(10));
            community.Restore(null);
            Assert.That(community.State(profile.Id).Sentiment, Is.EqualTo(7));
            Assert.That(roster.Named, Is.Empty);
        }

        [Test]
        public void RelationshipContextPreservesPersonalityAndContainsNoNumericMeter()
        {
            var p = Profile(sentiment: -80); ViewerCommunity community = Community(Roster(), p);
            string context = community.RelationshipContext(p.Id);
            Assert.That(context, Does.Not.Contain("-80"));
            Assert.That(context, Does.Contain("personality"));
            Assert.That(p.Personality, Is.EqualTo("A gentle, reserved viewer."));
        }

        [Test]
        public void CurrentScheduleAndContentCanAdmitAnEarlierAbsentViewerWithoutReroll()
        {
            var profiles = Enumerable.Range(0, 80).Select(i => Profile(i)).ToArray();
            var roster = Roster(); var community = Community(roster, profiles);
            Begin(community, minute: 600, topic: StreamTopic.Money); community.Tick(100);
            int outside = roster.Named.Count;
            community.UpdateContext(1200, StreamTopic.Games); community.Tick(101);
            Assert.That(roster.Named.Count, Is.GreaterThan(outside));
            int inside = roster.Named.Count;
            for (int i = 0; i < 100; i++) community.Tick(101);
            Assert.That(roster.Named.Count, Is.EqualTo(inside));
        }

        [TestCase(99, "Tester0 спасибо", 100)]
        [TestCase(-99, "Tester0 ты идиот", -100)]
        public void SentimentClampsAtBothBounds(int initial, string speech, int expected)
        {
            var roster = Roster(); var profile = Profile(sentiment: initial); var community = Community(roster, profile);
            Begin(community); roster.Join(profile.Participant());
            community.Process(Speech(roster, speech, 10));
            Assert.That(community.State(profile.Id).Sentiment, Is.EqualTo(expected));
        }

        [TestCase(-1, 23, .5f)]
        [TestCase(18, 24, .5f)]
        [TestCase(18, 23, -1f)]
        [TestCase(18, 23, 1.1f)]
        [TestCase(18, 23, float.NaN)]
        public void InvalidScheduleConfigurationIsRejected(int start, int end, float regularity)
        {
            var profile = Profile(); profile.Schedule.StartHour = start; profile.Schedule.EndHour = end; profile.Schedule.Regularity = regularity;
            Assert.That(profile.Validate(), Is.Not.Null);
        }

        [Test]
        public void NamedEpochSurvivesClearAndTrimmedAnonymousIdentityIsNeverReused()
        {
            var roster = Roster(2); var profile = Profile();
            roster.Join(profile.Participant(), 15); long first = roster.Epoch(profile.Id);
            Assert.That(roster.JoinedAt(profile.Id), Is.EqualTo(15));
            var old = roster.AnonymousChatter(new AudienceRandom(1), _ => true, 20);
            roster.Clear(); roster.SetAudienceSize(2); roster.Join(profile.Participant(), 30);
            Assert.That(roster.Epoch(profile.Id), Is.GreaterThan(first));
            Assert.That(roster.JoinedAt(profile.Id), Is.EqualTo(30));
            var next = roster.AnonymousChatter(new AudienceRandom(1), _ => true);
            Assert.That(next.ViewerId, Is.Not.EqualTo(old.ViewerId));
            Assert.That(roster.IsWatching(old.ViewerId), Is.False);
        }

        [Test]
        public void AudienceShrinkImmediatelyRemovesExcessNamedAndAnonymousWatchers()
        {
            var roster = Roster(1000); var random = new AudienceRandom(1);
            roster.Join(Profile().Participant()); roster.Join(Profile(1).Participant());
            for (int i = 0; i < 400; i++) roster.AnonymousChatter(random, _ => false);
            Assert.That(roster.Ephemeral.Count, Is.LessThanOrEqualTo(128));
            roster.SetAudienceSize(1);
            Assert.That(roster.Named.Count, Is.EqualTo(1)); Assert.That(roster.Ephemeral, Is.Empty);
            roster.SetAudienceSize(0); Assert.That(roster.Named, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IntentCannotSurviveLeaveAndRejoinOrRequestModelWhileAbsent(bool rejoin)
        {
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            ReactionIntent intent = ChatDirectorTests.Intent(viewer, "Alpha ты тут?");
            var roster = Roster(); roster.Join(viewer); roster.Leave(viewer.ViewerId);
            if (rejoin) roster.Join(viewer);
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "ну привет", .1));
            var chat = new StreamChat();
            using var director = new ChatDirector(model, new ChatModelSettings(), chat, new ReactionLog());
            director.Submit(intent);
            director.Update(intent.DueSeconds, roster, () => new ChatSituation("channel", 10, 10, ViewerLanguage.Russian, chat.Messages, null), "b");
            director.Update(intent.DueSeconds + 1, roster, () => new ChatSituation("channel", 10, 10, ViewerLanguage.Russian, chat.Messages, null), "b");
            Assert.That(model.Requests, Is.Empty); Assert.That(chat.Messages, Is.Empty);
        }
    }
}
