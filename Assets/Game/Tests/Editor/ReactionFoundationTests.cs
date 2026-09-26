using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;
using GoLive.PcBuilding;
using GoLive.Viewers;
using GoLive.Voice;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Stage A: deterministic stream events, speech relevance, silence ranges and reaction selection. No language
    // model takes part in any of these decisions.
    public sealed class ReactionFoundationTests
    {
        private static readonly ReactionTuning Tuning = new();

        [TestCase("так секунду")]
        [TestCase("сейчас воды попью")]
        [TestCase("ну короче")]
        [TestCase("hold on one sec")]
        public void TrivialSpeechIsIgnored(string text)
        {
            SpeechAnalysis speech = Analyze(text);
            Assert.That(speech.Relevance, Is.LessThan(Tuning.SpeechThreshold), text);
            var selector = Selector(Roster(5), 1);
            Assert.That(selector.Select(Speech(speech), 10, true, out string reason), Is.Empty);
            Assert.That(reason, Is.EqualTo("speech below threshold"));
        }

        [TestCase("если сейчас опять проиграю — удаляю игру", .55f)]
        [TestCase("чат что думаете?", .75f)]
        [TestCase("чат если я сейчас опять умру всё", .75f)]
        [TestCase("ok chat what do you think, mic or webcam first?", .75f)]
        [TestCase("спасибо за донат", .35f)]
        public void InterestingSpeechBecomesEligible(string text, float atLeast)
        {
            Assert.That(Analyze(text).Relevance, Is.GreaterThanOrEqualTo(atLeast), text);
        }

        [Test]
        public void SpeechCuesAndTopicsAreDetected()
        {
            SpeechAnalysis question = Analyze("чат что думаете про новую видеокарту?");
            Assert.That(question.Has(SpeechCue.AddressesChat) && question.Has(SpeechCue.Question), Is.True);
            Assert.That(question.Topics.HasFlag(StreamTopic.Hardware), Is.True);
            SpeechAnalysis promise = Analyze("завтра куплю новую видеокарту");
            Assert.That(promise.Has(SpeechCue.FutureCommitment), Is.True);
            Assert.That(Analyze("так секунду").Has(SpeechCue.Filler), Is.True);
            Assert.That(Analyze("сейчас воды попью").Has(SpeechCue.SelfTalk), Is.True);
        }

        [TestCase("NightOwl ты вообще ещё тут?")]
        [TestCase("найт оул ты вообще еще тут")]
        [TestCase("найтоул, ты тут?")]
        public void NamingAWatchingViewerAddressesThem(string text)
        {
            AudienceRoster roster = Roster(4, NightOwl());
            SpeechAnalysis speech = SpeechRelevance.Analyze(Recognized(text), roster.NamesForMentions());
            Assert.That(speech.MentionedViewerIds, Is.EqualTo(new[] { "viewer.nightowl" }));
            Assert.That(speech.Relevance, Is.GreaterThanOrEqualTo(.6f));
            int direct = 0;
            for (ulong seed = 1; seed <= 40; seed++)
                if (Selector(Roster(4, NightOwl()), seed).Select(Speech(speech), 10, true, out _)
                    .Any(intent => intent.Direct && intent.Viewer.ViewerId == "viewer.nightowl")) direct++;
            Assert.That(direct, Is.GreaterThanOrEqualTo(32), "the addressed viewer answers almost always");
        }

        [Test]
        public void ZeroReactionsIsAValidResult()
        {
            int silent = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                List<ReactionIntent> intents = Selector(Roster(2), seed).Select(Speech(Analyze("если сейчас проиграю будет обидно")), 10, true, out string reason);
                if (intents.Count == 0)
                {
                    silent++;
                    Assert.That(reason, Is.EqualTo("silence"));
                }
            }
            Assert.That(silent, Is.GreaterThan(10), "a tiny audience often lets a moment pass");
        }

        [Test]
        public void AbsentOrDepartedViewerIsNeverSelected()
        {
            for (ulong seed = 1; seed <= 40; seed++)
            {
                AudienceRoster roster = Roster(3, PixelFox(), NightOwl());
                Assert.That(roster.Leave("viewer.nightowl"), Is.True);
                SpeechAnalysis speech = SpeechRelevance.Analyze(Recognized("NightOwl ты тут?"), roster.NamesForMentions());
                Assert.That(speech.MentionedViewerIds, Is.Empty, "only watching viewers can be named");
                foreach (ReactionIntent intent in Selector(roster, seed).Select(Speech(speech), 10, true, out _))
                    Assert.That(intent.Viewer.ViewerId, Is.Not.EqualTo("viewer.nightowl"));
                Assert.That(roster.IsWatching("viewer.nightowl"), Is.False);
            }
        }

        [Test]
        public void NobodyReactsWithoutAnAudience()
        {
            AudienceRoster roster = Roster(0);
            Assert.That(roster.Join(NightOwl()), Is.False, "a named viewer is part of the audience count and cannot exceed it");
            Assert.That(Selector(roster, 1).Select(Speech(Analyze("чат что думаете?")), 10, true, out string reason), Is.Empty);
            Assert.That(reason, Is.EqualTo("no audience"));
        }

        [Test]
        public void WatchingViewerCanReact()
        {
            int chosen = 0;
            for (ulong seed = 1; seed <= 40; seed++)
                if (Selector(Roster(1, PixelFox()), seed).Select(Speech(Analyze("чат что думаете?")), 10, true, out _)
                    .Any(intent => intent.Viewer.ViewerId == "viewer.pixelfox")) chosen++;
            Assert.That(chosen, Is.GreaterThan(10));
        }

        [Test]
        public void SameViewerIsRateLimited()
        {
            AudienceRoster roster = Roster(1, PixelFox());
            ReactionSelector selector = Selector(roster, 7);
            ReactionIntent first = null;
            for (int i = 0; first == null && i < 30; i++)
                first = selector.Select(Speech(Analyze("чат что думаете про стрим?", 100 + i)), i * 2, true, out _).FirstOrDefault();
            Assert.That(first, Is.Not.Null);
            double now = first.DueSeconds + 1;
            for (int i = 0; i < 20; i++)
                Assert.That(selector.Select(Speech(Analyze("чат а вы что скажете?", 200 + i)), now + i * .5, true, out _), Is.Empty,
                    "the only viewer just wrote");
            // Even a direct address waits a few seconds after the viewer's last message.
            SpeechAnalysis named = SpeechRelevance.Analyze(Recognized("PixelFox а ты что думаешь?", 300), roster.NamesForMentions());
            Assert.That(named.MentionedViewerIds, Is.EqualTo(new[] { "viewer.pixelfox" }));
            Assert.That(selector.Select(Speech(named), first.DueSeconds + 1, true, out _), Is.Empty);
        }

        [Test]
        public void RepeatedEventKeyNeverReactsTwice()
        {
            ReactionSelector selector = Selector(Roster(20, PixelFox(), NightOwl()), 3);
            StreamEvent donation = StreamEvent.Donation(1, "stream.donation.1", 5, null, "someone", 500);
            selector.Select(donation, 5, true, out _);
            Assert.That(selector.Select(donation, 6, true, out string reason), Is.Empty);
            Assert.That(reason, Is.EqualTo("duplicate"));
        }

        [Test]
        public void RepeatedDonationReceiptIsOneEventAndOneCredit()
        {
            using var live = new LiveDesktop(20260926);
            DonationAccount account = live.State.Donation;
            var wallet = new GoLive.Economy.Wallet(0);
            using var payout = new DonationPayout(account, wallet);
            string id = live.State.Stream.BroadcastId + ".donation.test";
            Assert.That(account.Receive(id, "someone", 300), Is.Null);
            Assert.That(account.Receive(id, "someone", 300), Is.Null, "the same receipt id is accepted once");
            var events = new List<StreamEvent>();
            live.State.Viewers.Events.Drain(events);
            Assert.That(events.Where(e => e.Kind == StreamEventKind.Donation).Select(e => e.Key), Is.EqualTo(new[] { id }));
            Assert.That(wallet.BalanceCents, Is.EqualTo(300), "exactly one credit; reactions never touch money");
        }

        [Test]
        public void TinyAudienceLeavesMeaningfulSilenceAndBigAudienceTalksMore()
        {
            int small = ScheduledMessages(3, out double longestGap);
            int big = ScheduledMessages(50, out _);
            TestContext.WriteLine($"10 minutes: 3 viewers -> {small} messages (longest gap {longestGap:0}s), 50 viewers -> {big}");
            Assert.That(small, Is.LessThanOrEqualTo(18), "a 3-viewer chat is never a busy chat");
            Assert.That(longestGap, Is.GreaterThanOrEqualTo(45), "with 3 viewers the chat can be quiet for a while");
            Assert.That(big, Is.GreaterThan(small * 2), "a bigger audience writes more");
        }

        [Test]
        public void ShortSilenceIsIgnoredLongSilenceIsAMoment()
        {
            using var live = new LiveDesktop(11);
            var events = new List<StreamEvent>();
            live.Advance(30, listening: true, into: events);
            Assert.That(events.Where(e => e.Kind == StreamEventKind.StreamerSilence), Is.Empty, "a 30 s pause is normal");
            live.Advance(50, listening: true, into: events);
            Assert.That(events.Where(e => e.Kind == StreamEventKind.StreamerSilence).Select(e => e.Silence), Is.EqualTo(new[] { SilenceLevel.Long }));
            Assert.That(live.State.Stream.StreamerActivity, Is.EqualTo(StreamerActivity.Quiet));
            live.Advance(110, listening: true, into: events);
            Assert.That(events.Count(e => e.Silence == SilenceLevel.VeryLong), Is.EqualTo(1));
            Assert.That(live.State.Stream.StreamerActivity, Is.EqualTo(StreamerActivity.VeryQuiet));
            live.Say("я тут");
            live.Advance(1, listening: true, into: events);
            Assert.That(live.State.Stream.StreamerActivity, Is.EqualTo(StreamerActivity.Active), "speaking ends the silence");
        }

        [Test]
        public void SilenceIsNotMeasuredWithoutAListeningMicrophone()
        {
            using var live = new LiveDesktop(12);
            var events = new List<StreamEvent>();
            live.Advance(400, listening: false, into: events);
            Assert.That(events.Where(e => e.Kind == StreamEventKind.StreamerSilence), Is.Empty);
            Assert.That(live.State.Stream.StreamerActivity, Is.EqualTo(StreamerActivity.Active));
        }

        [Test]
        public void LongSilenceCostsASlightAmountOfRetention()
        {
            var tuning = new AudienceTuning();
            AudienceFactors active = AudienceFactors.Evaluate(new AudienceConditions(1080, StreamQuality.Low, 5, false, true, false), tuning);
            AudienceFactors veryQuiet = AudienceFactors.Evaluate(new AudienceConditions(1080, StreamQuality.Low, 5, false, true, false,
                StreamerActivity.VeryQuiet), tuning);
            AudienceFactors away = AudienceFactors.Evaluate(new AudienceConditions(1080, StreamQuality.Low, 5, false, true, false,
                StreamerActivity.Away), tuning);
            Assert.That(veryQuiet.Retention, Is.EqualTo(active.Retention * tuning.VeryQuietRetention).Within(1e-9));
            Assert.That(away.Retention, Is.LessThan(veryQuiet.Retention));
        }

        [Test]
        public void OfflineBlocksStreamReactions()
        {
            Assert.That(Selector(Roster(10, PixelFox()), 1).Select(Speech(Analyze("чат что думаете?")), 10, false, out string reason), Is.Empty);
            Assert.That(reason, Is.EqualTo("offline"));
            using var live = new LiveDesktop(13);
            live.State.Stream.Stop();
            live.Tick(1);
            live.Say("чат что думаете?");
            live.Tick(1);
            Assert.That(live.State.Viewers.Pending, Is.Empty);
            Assert.That(live.State.Viewers.Events.IsLive, Is.False);
        }

        [Test]
        public void DecisionsAreDeterministicUnderASeed()
        {
            string Run(ulong seed)
            {
                ReactionSelector selector = Selector(Roster(12, PixelFox(), NightOwl()), seed);
                var trace = new List<string>();
                for (int i = 0; i < 30; i++)
                {
                    StreamEvent streamEvent = i % 3 == 0
                        ? StreamEvent.AudienceChatter(i, "chatter." + i, i * 7, 12)
                        : Speech(Analyze(i % 2 == 0 ? "чат что думаете?" : "опять проиграл блин", i));
                    foreach (ReactionIntent intent in selector.Select(streamEvent, i * 7, true, out _))
                        trace.Add($"{intent.Event.Key}:{intent.Viewer.DisplayName}:{intent.DueSeconds:0.000}:{intent.Direct}");
                }
                return string.Join("|", trace);
            }
            Assert.That(Run(42), Is.EqualTo(Run(42)));
            Assert.That(Run(42), Is.Not.EqualTo(Run(43)));
        }

        [Test]
        public void LowValueSpeechIsTracedAsRejectedAndInterestingSpeechAsAnEvent()
        {
            using var live = new LiveDesktop(14);
            live.Say("так секунду");
            live.Tick(.1);
            ReactionLogEntry rejected = live.State.Viewers.Log.Entries.Last(e => e.EventKind == StreamEventKind.StreamerSpeech);
            Assert.That(rejected.Outcome, Is.EqualTo(ReactionOutcome.Rejected));
            Assert.That(rejected.Reason, Is.EqualTo("speech below threshold"));
            Assert.That(rejected.Speech, Is.EqualTo("так секунду"));
            live.Say("чат если я сейчас опять умру всё");
            live.Tick(.1);
            ReactionLogEntry interesting = live.State.Viewers.Log.Entries.Last(e => e.EventKind == StreamEventKind.StreamerSpeech);
            Assert.That(interesting.Relevance, Is.GreaterThanOrEqualTo(.75f));
            Assert.That(interesting.Outcome, Is.Not.EqualTo(ReactionOutcome.Rejected).Or.Property("Reason").EqualTo("silence"));
        }

        // Ten minutes of the same moments for a given audience size: scheduled messages and the longest quiet gap.
        private static int ScheduledMessages(int viewers, out double longestGap)
        {
            var roster = Roster(viewers, PixelFox(), NightOwl());
            ReactionSelector selector = Selector(roster, 99);
            var times = new List<double>();
            for (int second = 0; second < 600; second += 5)
            {
                StreamEvent streamEvent = second % 30 == 0
                    ? Speech(Analyze(second % 60 == 0 ? "чат что думаете?" : "опять лагает комп", second))
                    : StreamEvent.AudienceChatter(second, "chatter." + second, second, viewers);
                foreach (ReactionIntent intent in selector.Select(streamEvent, second, true, out _)) times.Add(intent.DueSeconds);
            }
            times.Sort();
            longestGap = 0;
            for (int i = 1; i < times.Count; i++) longestGap = Math.Max(longestGap, times[i] - times[i - 1]);
            return times.Count;
        }

        internal static SpeechAnalysis Analyze(string text, long sequence = 1) =>
            SpeechRelevance.Analyze(Recognized(text, sequence), Array.Empty<ViewerNameForms>());

        internal static RecognizedSpeech Recognized(string text, long sequence = 1) => new(sequence, text, 0, .8f, "ru");

        // The recognized phrase's sequence is its idempotency key, so every phrase in a test has its own.
        internal static StreamEvent Speech(SpeechAnalysis speech) => StreamEvent.StreamerSpeech(speech.Sequence, "test", speech.Sequence, speech);

        internal static ReactionSelector Selector(AudienceRoster roster, ulong seed) => new(Tuning, roster, new AudienceRandom(seed));

        internal static AudienceRoster Roster(int audience, params ChatParticipant[] named)
        {
            var roster = new AudienceRoster(EphemeralViewers.Create);
            roster.SetAudienceSize(audience);
            foreach (ChatParticipant participant in named) Assert.That(roster.Join(participant), Is.True);
            return roster;
        }

        internal static ChatParticipant NightOwl() => new("viewer.nightowl", "NightOwl", true,
            new ReactionTraits(.7f, 1f, .9f, StreamTopic.Games), new[] { "night owl", "найт оул", "найтоул", "найт овл" });

        internal static ChatParticipant PixelFox() => new("viewer.pixelfox", "PixelFox", true,
            new ReactionTraits(.8f, 1f, 1f, StreamTopic.Community), new[] { "пиксель фокс", "пиксельфокс" });
    }

    // A registered channel broadcasting live from a plain DesktopState, ticked like the runtime bridge ticks it.
    internal sealed class LiveDesktop : IDisposable
    {
        private readonly List<Object> _created = new();
        private long _speechSequence;

        public DesktopState State { get; }

        public LiveDesktop(ulong seed, PcPeripherals peripherals = null, ReactionTuning reactions = null)
        {
            State = new DesktopState(Apps(), peripherals, new AudienceTuning(), new AudienceRandom(seed), reactions);
            Assert.That(State.Outline.CreateAddress("viewer.core"), Is.Null);
            Assert.That(State.Trich.Register(State.Outline, State.Outline.Address), Is.Null);
            Assert.That(State.Stream.Connect(State.Trich.ChannelCode), Is.Null);
            Assert.That(State.Stream.Start(StreamSessionTests.CreateCapabilities(_created, false), true, 5), Is.Null);
            Tick(.75);
            Assert.That(State.Stream.State, Is.EqualTo(StreamState.Live));
        }

        public void Tick(double seconds, bool atDesk = true, bool listening = true)
        {
            State.Stream.Tick((float)seconds, StreamSessionTests.PrimeTime);
            State.Viewers.Tick(new StreamerContext(atDesk, listening));
        }

        // Advances in one-second frames, collecting normalized events (the viewer core's own queue is bypassed).
        public void Advance(double seconds, bool listening, List<StreamEvent> into, bool atDesk = true)
        {
            for (double elapsed = 0; elapsed < seconds; elapsed += 1)
            {
                State.Stream.Tick(1, StreamSessionTests.PrimeTime);
                State.Viewers.Events.Tick(new StreamerContext(atDesk, listening));
                State.Viewers.Events.Drain(into);
                State.Stream.SetStreamerActivity(State.Viewers.Events.Activity);
            }
        }

        public bool Say(string text) => State.SpeechFeed.Offer(new RecognizedSpeech(++_speechSequence, text, SpeechClock.Now, .8f, "ru"));

        public void Dispose()
        {
            State.Dispose();
            foreach (Object item in _created) Object.DestroyImmediate(item);
        }

        internal static DesktopAppDefinition[] Apps() => new[]
        {
            new DesktopAppDefinition(DesktopAppId.MyComputer, "computer", "computer.desc", null, 20, true),
            new DesktopAppDefinition(DesktopAppId.Hub, "hub", "hub.desc", null, 40, true),
            new DesktopAppDefinition(DesktopAppId.Web, "web", "web.desc", null, 40, true),
            new DesktopAppDefinition(DesktopAppId.Outline, "outline", "outline.desc", null, 50, false),
            new DesktopAppDefinition(DesktopAppId.Trich, "trich", "trich.desc", null, 50, false),
            new DesktopAppDefinition(DesktopAppId.Streamly, "streamly", "streamly.desc", null, 50, false),
            new DesktopAppDefinition(DesktopAppId.Donation, "donation", "donation.desc", null, 50, false)
        };
    }
}
