using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;
using GoLive.Viewers;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class ViewerPresenceIntegrationTests
    {
        [Test]
        public void RuntimeCommunityJoinsWithinSimulationSeatsAndNormalizesOneJoinEach()
        {
            var profiles = Enumerable.Range(0, 40).Select(i => ViewerCommunityTests.Profile(i)).ToArray();
            using var live = new LiveDesktop(20260926, profiles: profiles);
            var joined = new HashSet<string>();
            live.State.Viewers.Community.Joined += participant => Assert.That(joined.Add(participant.ViewerId), Is.True, "Only one visit per viewer in a broadcast.");
            for (int i = 0; i < 150; i++)
            {
                live.State.Stream.Tick(1, 1200);
                live.State.Viewers.Tick(new StreamerContext(true, true, 1200, StreamTopic.Games));
                Assert.That(live.State.Viewers.Roster.Named.Count, Is.LessThanOrEqualTo(live.State.Stream.Audience.CurrentViewers));
            }
            ViewerCore core = live.State.Viewers;
            Assert.That(core.Roster.Named, Is.Not.Empty);
            Assert.That(profiles.Sum(p => core.Community.State(p.Id).VisitCount), Is.EqualTo(joined.Count));
            Assert.That(core.Roster.Named.All(p => joined.Contains(p.ViewerId)), Is.True);
            Assert.That(core.Log.Entries.Any(e => e.EventKind == StreamEventKind.ViewerJoined), Is.True);
        }

        [Test]
        public void OrdinarySpeechUpdatesOnlyItsPresentNamedTargetBeforePromptConstruction()
        {
            var profiles = new[] { ViewerCommunityTests.Profile(), ViewerCommunityTests.Profile(1) };
            using var live = new LiveDesktop(20260926, profiles: profiles);
            ViewerCore core = live.State.Viewers;
            core.Roster.SetAudienceSize(10);
            foreach (var profile in profiles) core.Roster.Join(profile.Participant());
            Assert.That(live.Say("Tester0 спасибо"), Is.True);
            live.Tick(.1);
            Assert.That(core.Community.State(profiles[0].Id).Sentiment, Is.EqualTo(3));
            Assert.That(core.Community.State(profiles[1].Id).Sentiment, Is.Zero);
            var intent = ChatDirectorTests.Intent(profiles[0].Participant(), "Tester0 привет");
            var request = ChatContextBuilder.Build(intent, core.SituationFor(intent), 100);
            Assert.That(request.User, Does.Contain("RELATIONSHIP:"));
            Assert.That(request.User, Does.Contain(profiles[0].Personality));
        }

        [Test]
        public void ExplicitThanksToSomeoneElseDoesNotTargetRecentDonor()
        {
            using var live = new LiveDesktop(20260926);
            var donor = ChatDirectorTests.Viewer("viewer.donor", "Donor");
            var target = ChatDirectorTests.Viewer("viewer.target", "Target");
            live.State.Viewers.Roster.SetAudienceSize(10);
            live.State.Viewers.Roster.Join(donor); live.State.Viewers.Roster.Join(target);
            Assert.That(live.State.Donation.Receive(live.State.Stream.BroadcastId + ".donation.test", "Donor", 500), Is.Null);
            Assert.That(live.Say("Target спасибо"), Is.True);
            var events = new List<StreamEvent>(); live.State.Viewers.Events.Drain(events);
            StreamEvent speech = events.Single(e => e.Kind == StreamEventKind.StreamerSpeech);
            Assert.That(speech.SubjectViewerId, Is.Null);
            Assert.That(speech.Speech.MentionedViewerIds, Is.EqualTo(new[] { target.ViewerId }));
        }

        [Test]
        public void ExplicitThanksToAbsentKnownViewerDoesNotChangeRecentPresentDonor()
        {
            var donor = ViewerCommunityTests.Profile(); donor.DisplayName = "Donor";
            var absent = ViewerCommunityTests.Profile(1); absent.DisplayName = "Target";
            using var live = new LiveDesktop(20260926, profiles: new[] { donor, absent });
            ViewerCore core = live.State.Viewers;
            core.Roster.SetAudienceSize(10); core.Roster.Join(donor.Participant());
            Assert.That(core.Roster.IsWatching(absent.Id), Is.False);
            Assert.That(live.State.Donation.Receive(live.State.Stream.BroadcastId + ".donation.test", "Donor", 500), Is.Null);
            Assert.That(live.Say("Target спасибо"), Is.True);
            live.Tick(.1);
            Assert.That(core.Community.State(donor.Id).Sentiment, Is.Zero, "An explicitly named absent viewer must suppress donor inference.");
            Assert.That(core.Community.State(absent.Id).Sentiment, Is.Zero, "Absent viewers do not receive social changes.");
            Assert.That(core.Roster.IsWatching(absent.Id), Is.False, "Recognizing a known name must not create presence.");
        }

        [Test]
        public void DesktopRestoreIsDetachedIdempotentAndResetsTransientPresence()
        {
            var profile = ViewerCommunityTests.Profile(sentiment: 12);
            using var state = new DesktopState(LiveDesktop.Apps(), viewerProfiles: new[] { profile });
            state.Outline.CreateAddress("before");
            DesktopSnapshot snapshot = state.Capture();
            snapshot.ViewerCommunity.Viewers[0].Sentiment = 40;
            snapshot.ViewerCommunity.Viewers[0].VisitCount = 6;
            Assert.That(state.Viewers.Community.State(profile.Id).Sentiment, Is.EqualTo(12));
            state.Viewers.Roster.SetAudienceSize(10); state.Viewers.Roster.Join(profile.Participant());
            state.Restore(snapshot, Array.Empty<DesktopDrive>());
            state.Restore(snapshot, Array.Empty<DesktopDrive>());
            Assert.That(state.Viewers.Community.State(profile.Id).Sentiment, Is.EqualTo(40));
            Assert.That(state.Viewers.Community.State(profile.Id).VisitCount, Is.EqualTo(6));
            Assert.That(state.Viewers.Community.Capture().Viewers, Has.Count.EqualTo(1));
            Assert.That(state.Viewers.Roster.Named, Is.Empty);
            Assert.That(state.Viewers.Chat.Messages, Is.Empty);
            snapshot.ViewerCommunity.Viewers[0].Sentiment = -99;
            Assert.That(state.Viewers.Community.State(profile.Id).Sentiment, Is.EqualTo(40));
            snapshot.ViewerCommunity = null;
            state.Restore(snapshot, Array.Empty<DesktopDrive>());
            Assert.That(state.Viewers.Community.State(profile.Id).Sentiment, Is.EqualTo(12));
            Assert.That(state.Viewers.Community.State(profile.Id).VisitCount, Is.Zero);
        }

        [TestCase("unknown")]
        [TestCase("duplicate")]
        [TestCase("null-row")]
        [TestCase("null-list")]
        [TestCase("range")]
        [TestCase("negative-count")]
        [TestCase("version")]
        public void MalformedCommunityRejectsEntireDesktopGraphBeforeAnyMutation(string invalid)
        {
            var profile = ViewerCommunityTests.Profile();
            using var state = new DesktopState(LiveDesktop.Apps(), viewerProfiles: new[] { profile });
            state.Outline.CreateAddress("original"); state.Windows.Open(DesktopAppId.Hub);
            DesktopSnapshot snapshot = state.Capture();
            var rows = snapshot.ViewerCommunity.Viewers;
            switch (invalid)
            {
                case "unknown": rows[0].ViewerId = "viewer.unknown"; break;
                case "duplicate": rows.Add(rows[0]); break;
                case "null-row": rows.Add(null); break;
                case "null-list": snapshot.ViewerCommunity.Viewers = null; break;
                case "range": rows[0].Sentiment = 101; break;
                case "negative-count": rows[0].Acknowledgements = -1; break;
                case "version": snapshot.ViewerCommunity.Version = 999; break;
            }
            Assert.Throws<ArgumentException>(() => state.Restore(snapshot, Array.Empty<DesktopDrive>()));
            Assert.That(state.Outline.Address, Is.EqualTo("original@outline.local"));
            Assert.That(state.Windows.Windows, Has.Count.EqualTo(1));
            Assert.That(state.RestoreGeneration, Is.Zero);
        }

        [Test]
        public void EmptyLegacyCommunitySeedsNewAuthoredDefaults()
        {
            using var legacy = new DesktopState(LiveDesktop.Apps());
            using var current = new DesktopState(LiveDesktop.Apps(), viewerProfiles: new[] { ViewerCommunityTests.Profile(sentiment: 17) });
            current.Restore(legacy.Capture(), Array.Empty<DesktopDrive>());
            Assert.That(current.Viewers.Community.State("viewer.test0").Sentiment, Is.EqualTo(17));
        }

        [Test]
        public void LoadCancelsInflightModelAndLateResultCannotRecreatePresence()
        {
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "ну привет", .1)) { Manual = true };
            using var live = new LiveDesktop(20260926, languageModel: model);
            ViewerCore core = live.State.Viewers;
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            core.Roster.SetAudienceSize(10); core.Roster.Join(viewer);
            ReactionIntent intent = ChatDirectorTests.Intent(viewer, "Alpha привет");
            core.Director.Submit(intent);
            core.Director.Update(0, core.Roster, core.SituationFor, live.State.Stream.BroadcastId);
            Assert.That(model.Requests, Has.Count.EqualTo(1));
            live.State.Restore(live.State.Capture(), Array.Empty<DesktopDrive>());
            Assert.That(model.Cancelled(0), Is.True);
            model.Complete(0, new LanguageModelResult(LanguageModelStatus.Ok, "ну привет", .1));
            core.Director.Update(100, core.Roster, core.SituationFor, "replaced");
            Assert.That(core.Roster.Named, Is.Empty); Assert.That(core.Chat.Messages, Is.Empty);
            Assert.That(core.Director.QueueDepth, Is.Zero);
        }

        [Test]
        public void InflightReactionIsCancelledOnLeaveRejoinBeforePublication()
        {
            var model = new ChatDirectorTests.FakeModel(_ => new LanguageModelResult(LanguageModelStatus.Ok, "ну привет", .1)) { Manual = true };
            var chat = new StreamChat();
            using var director = new ChatDirector(model, new ChatModelSettings(), chat, new ReactionLog());
            var viewer = ChatDirectorTests.Viewer("viewer.a", "Alpha");
            var roster = new AudienceRoster(EphemeralViewers.Create); roster.SetAudienceSize(5); roster.Join(viewer);
            ReactionIntent intent = ChatDirectorTests.Intent(viewer, "Alpha привет"); director.Submit(intent);
            ChatSituation Situation() => new("channel", 0, 5, ViewerLanguage.Russian, chat.Messages, null);
            director.Update(0, roster, Situation, "b");
            Assert.That(model.Requests, Has.Count.EqualTo(1));
            roster.Leave(viewer.ViewerId); roster.Join(viewer);
            model.Complete(0, new LanguageModelResult(LanguageModelStatus.Ok, "ну привет", .1));
            director.Update(intent.DueSeconds + 1, roster, Situation, "b");
            Assert.That(model.Cancelled(0), Is.True); Assert.That(chat.Messages, Is.Empty);
        }
    }
}
