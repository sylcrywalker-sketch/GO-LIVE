using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GoLive.Desktop;
using GoLive.PcBuilding;
using GoLive.Voice;
using NUnit.Framework;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // Recognized real speech enters the stream only while Live; the fictional PC microphone plays no part.
    public sealed class StreamSpeechFeedTests
    {
        private readonly List<Object> _created = new();
        private PcCapabilities _desktop;
        private double _now = 1000;

        [SetUp]
        public void SetUp() => _desktop = StreamSessionTests.CreateCapabilities(_created, false);

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _created) Object.DestroyImmediate(item);
            _created.Clear();
        }

        [Test]
        public void OnlyPhrasesSpokenWhileLiveBecomeStreamEvents()
        {
            var stream = Stream(new PcPeripherals());
            using var feed = new StreamSpeechFeed(stream, () => _now);
            var events = new List<StreamSpeechEvent>();
            feed.SpeechAdded += events.Add;

            Assert.That(feed.Offer(Speech(1, "офлайн фраза")), Is.False, "Offline");
            Assert.That(stream.Start(_desktop, true, 5), Is.Null);
            _now += .5;
            Assert.That(feed.Offer(Speech(2, "во время запуска")), Is.False, "Starting is not Live");
            stream.Tick(.75f, StreamSessionTests.PrimeTime);
            Assert.That(stream.State, Is.EqualTo(StreamState.Live));
            _now += 3;
            Assert.That(feed.Offer(Speech(3, "Привет чат, сегодня попробуем новый стрим.")), Is.True, "Live");
            stream.Tick(10, StreamSessionTests.PrimeTime);
            _now += 2;
            Assert.That(feed.Offer(Speech(4, "Hello chat, let's try this again.")), Is.True);
            Assert.That(stream.Stop(), Is.Null);
            _now += .1;
            Assert.That(feed.Offer(Speech(5, "after stop")), Is.False, "Stopping immediately stops new phrases");
            stream.Tick(1, StreamSessionTests.PrimeTime);
            Assert.That(feed.Offer(Speech(6, "offline again")), Is.False);

            Assert.That(events.Select(e => e.Speech.Sequence), Is.EqualTo(new long[] { 3, 4 }));
            Assert.That(events[1].StreamSeconds, Is.EqualTo(10).Within(1e-6));
            Assert.That(feed.Recent, Is.Empty, "the transient phrase list ends with the broadcast");
        }

        [Test]
        public void PhraseThatEndedBeforeGoingLiveIsRejectedEvenIfRecognizedLater()
        {
            var stream = Stream(new PcPeripherals());
            using var feed = new StreamSpeechFeed(stream, () => _now);
            Assert.That(stream.Start(_desktop, true, 5), Is.Null);
            RecognizedSpeech spokenDuringStartup = Speech(1, "сказано до эфира");
            _now += 1;
            stream.Tick(.75f, StreamSessionTests.PrimeTime);
            Assert.That(feed.Offer(spokenDuringStartup), Is.False, "recognition latency cannot smuggle offline speech into the stream");
            _now += 2;
            Assert.That(feed.Offer(Speech(2, "уже в эфире")), Is.True);
        }

        [Test]
        public void DuplicateDeliveryAndBlankTextAreIgnored()
        {
            var stream = Live(new PcPeripherals(), out StreamSpeechFeed feed);
            using (feed)
            {
                var speech = Speech(7, "один раз");
                Assert.That(feed.Offer(speech), Is.True);
                Assert.That(feed.Offer(speech), Is.False, "a repeated backend callback cannot duplicate a stream event");
                Assert.That(feed.Offer(null), Is.False);
                Assert.That(feed.Recent.Count, Is.EqualTo(1));
                Assert.That(stream.State, Is.EqualTo(StreamState.Live));
            }
        }

        [Test]
        public void EachBroadcastStartsWithAnEmptyTransientFeed()
        {
            var stream = Live(new PcPeripherals(), out StreamSpeechFeed feed);
            using (feed)
            {
                Assert.That(feed.Offer(Speech(1, "первый эфир")), Is.True);
                Assert.That(feed.Recent.Count, Is.EqualTo(1));
                stream.Abort();
                Assert.That(feed.Recent, Is.Empty);
                Assert.That(stream.Start(_desktop, true, 5), Is.Null);
                stream.Tick(.75f, StreamSessionTests.PrimeTime);
                _now += 1;
                Assert.That(feed.Recent, Is.Empty);
                Assert.That(feed.Offer(Speech(2, "второй эфир")), Is.True);
                Assert.That(feed.Recent.Single().Speech.Text, Is.EqualTo("второй эфир"));
            }
        }

        [Test]
        public void InGameMicrophoneAbsenceDoesNotBlockRealSpeech()
        {
            var peripherals = new PcPeripherals();
            Assert.That(peripherals.HasMicrophone, Is.False);
            Live(peripherals, out StreamSpeechFeed feed);
            using (feed) Assert.That(feed.Offer(Speech(1, "микрофона в игре нет")), Is.True);
        }

        [Test]
        public void BackendFailureLeavesTheBroadcastAndAudienceRunning()
        {
            var stream = Live(new PcPeripherals(), out StreamSpeechFeed feed);
            var recognition = new VoiceRecognition(new VoiceActivitySettings(), () => throw new DllNotFoundException("libwhisper"));
            recognition.Recognized += speech => feed.Offer(speech);
            recognition.BeginListening(16000);
            VoicePipelineTests.WaitUntil(() => { recognition.Update(); return recognition.Status == VoiceStatus.RecognizerUnavailable; });
            VoicePipelineTests.SubmitAll(recognition, VoicePipelineTests.Join(VoicePipelineTests.Tone(1.2, .2), VoicePipelineTests.Silence(1.5)));
            recognition.Update();
            stream.Tick(60, StreamSessionTests.PrimeTime);
            Assert.That(stream.State, Is.EqualTo(StreamState.Live));
            Assert.That(stream.Audience.SimulatedSeconds, Is.EqualTo(60));
            Assert.That(feed.Recent, Is.Empty, "voice reactions are simply unavailable");
            recognition.Dispose();
            feed.Dispose();
        }

        [Test]
        public void DesktopStateOwnsOneFeedForItsStream()
        {
            using var state = new DesktopState(new[]
            {
                new DesktopAppDefinition(DesktopAppId.MyComputer, "computer", "computer.desc", null, 20, true),
                new DesktopAppDefinition(DesktopAppId.Hub, "hub", "hub.desc", null, 40, true),
                new DesktopAppDefinition(DesktopAppId.Web, "web", "web.desc", null, 40, true),
                new DesktopAppDefinition(DesktopAppId.Outline, "outline", "outline.desc", null, 50, false),
                new DesktopAppDefinition(DesktopAppId.Trich, "trich", "trich.desc", null, 50, false),
                new DesktopAppDefinition(DesktopAppId.Streamly, "streamly", "streamly.desc", null, 50, false),
                new DesktopAppDefinition(DesktopAppId.Donation, "donation", "donation.desc", null, 50, false)
            });
            Assert.That(state.SpeechFeed, Is.Not.Null);
            Assert.That(state.SpeechFeed.Offer(new RecognizedSpeech(1, "нет эфира", SpeechClock.Now, null, "ru")), Is.False);
            Assert.That(typeof(DesktopSnapshot).GetFields().Select(field => field.FieldType), Has.None.EqualTo(typeof(StreamSpeechFeed)),
                "transcripts are never part of save data");
        }

        [Test]
        public void FictionalPeripheralsHaveNoDependencyOnRealVoice()
        {
            var equipment = new[] { typeof(PcPeripherals), typeof(PcPeripheralsSnapshot), typeof(PcPeripheralKind) };
            foreach (Type type in equipment)
                foreach (Type used in UsedTypes(type))
                    Assert.That(used.Namespace ?? "", Does.Not.StartWith("GoLive.Voice").And.Not.StartWith("Whisper"),
                        type.Name + " must not know about " + used.FullName);
            foreach (Type type in typeof(VoiceRecognition).Assembly.GetTypes().Where(type => type.Namespace == "GoLive.Voice"))
                foreach (Type used in UsedTypes(type))
                    Assert.That(used.Namespace ?? "", Does.Not.StartWith("GoLive.PcBuilding").And.Not.StartWith("GoLive.Desktop"),
                        "real voice " + type.Name + " must not depend on " + used.FullName);
        }

        private static IEnumerable<Type> UsedTypes(Type type)
        {
            const BindingFlags all = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            foreach (FieldInfo field in type.GetFields(all)) yield return field.FieldType;
            foreach (PropertyInfo property in type.GetProperties(all)) yield return property.PropertyType;
            foreach (MethodInfo method in type.GetMethods(all))
            {
                yield return method.ReturnType;
                foreach (ParameterInfo parameter in method.GetParameters()) yield return parameter.ParameterType;
            }
            foreach (EventInfo item in type.GetEvents(all)) yield return item.EventHandlerType;
            foreach (ConstructorInfo constructor in type.GetConstructors(all))
                foreach (ParameterInfo parameter in constructor.GetParameters()) yield return parameter.ParameterType;
        }

        private RecognizedSpeech Speech(long sequence, string text) => new(sequence, text, _now, .8f, "ru");

        private static StreamSession Stream(PcPeripherals peripherals)
        {
            var channel = DesktopAccountTests.RegisteredChannel();
            var stream = new StreamSession(channel, new DonationAccount(), peripherals, new AudienceTuning(), new AudienceRandom(3));
            Assert.That(stream.Connect(channel.ChannelCode), Is.Null);
            return stream;
        }

        private StreamSession Live(PcPeripherals peripherals, out StreamSpeechFeed feed)
        {
            var stream = Stream(peripherals);
            feed = new StreamSpeechFeed(stream, () => _now);
            Assert.That(stream.Start(_desktop, true, 5), Is.Null);
            stream.Tick(.75f, StreamSessionTests.PrimeTime);
            _now += 1;
            return stream;
        }
    }
}
