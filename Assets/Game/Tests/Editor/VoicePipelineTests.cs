using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using GoLive.Voice;
using NUnit.Framework;

namespace GoLive.Tests
{
    // Real-voice pipeline logic with test adapters (the production backend is whisper.cpp; see the manual
    // real-microphone acceptance test). No Unity objects.
    public sealed class VoicePipelineTests
    {
        private const int Rate = 16000;

        [Test]
        public void SilenceNeverOpensAPhrase()
        {
            var vad = new VoiceActivityDetector(new VoiceActivitySettings(), Rate);
            var phrases = Feed(vad, Silence(3));
            Assert.That(phrases, Is.Empty);
            Assert.That(vad.InSpeech, Is.False);
        }

        [Test]
        public void NoiseShorterThanTheOnsetIsIgnored()
        {
            var vad = new VoiceActivityDetector(new VoiceActivitySettings(), Rate);
            var phrases = Feed(vad, Join(Silence(1), Tone(.1, .3), Silence(1), Tone(.06, .5), Silence(1)));
            Assert.That(phrases, Is.Empty, "clicks and short bursts are not speech");
        }

        [Test]
        public void SilenceTimeoutClosesThePhraseWithPreRollAndShortTail()
        {
            var settings = new VoiceActivitySettings();
            var vad = new VoiceActivityDetector(settings, Rate);
            var phrases = Feed(vad, Join(Silence(1), Tone(1.2, .2), Silence(1.5)));
            Assert.That(phrases.Count, Is.EqualTo(1));
            SpeechSegment phrase = phrases[0];
            Assert.That(phrase.VoicedSeconds, Is.EqualTo(1.2).Within(.03));
            double seconds = phrase.Samples.Length / (double)Rate;
            Assert.That(seconds, Is.InRange(1.2 + settings.PreRollMilliseconds / 1000.0, 1.2 + settings.PreRollMilliseconds / 1000.0 + .15),
                "pre-roll before the onset, only a short silent tail after it");
            Assert.That(phrase.Samples[0], Is.EqualTo(0).Within(1e-6), "the phrase starts in the pre-roll silence");
            Assert.That(phrase.Truncated, Is.False);
            Assert.That(phrase.EndSample / (double)Rate, Is.EqualTo(1 + 1.2 + .12).Within(.03));
        }

        [Test]
        public void SpeechBelowTheMinimumPhraseIsDiscarded()
        {
            var vad = new VoiceActivityDetector(new VoiceActivitySettings(), Rate);
            var phrases = Feed(vad, Join(Silence(1), Tone(.3, .2), Silence(1.5)));
            Assert.That(phrases, Is.Empty);
        }

        [Test]
        public void MaximumPhraseSplitsLongSpeechAndBuffersStayBounded()
        {
            var settings = new VoiceActivitySettings();
            var vad = new VoiceActivityDetector(settings, Rate);
            int capacity = vad.BufferCapacitySamples;
            var phrases = Feed(vad, Join(Silence(.5), Syllables(30, .2), Silence(1.5)));
            Assert.That(phrases.Count, Is.GreaterThanOrEqualTo(3), "30 s of speech is recognized in bounded parts");
            int maximum = Rate * (settings.MaximumPhraseMilliseconds + settings.PreRollMilliseconds) / 1000;
            foreach (SpeechSegment phrase in phrases) Assert.That(phrase.Samples.Length, Is.LessThanOrEqualTo(maximum));
            Assert.That(phrases[0].Truncated, Is.True);
            Assert.That(vad.BufferCapacitySamples, Is.EqualTo(capacity), "no unlimited recording");
            Assert.That(capacity, Is.LessThan(Rate * 13));
        }

        [Test]
        public void SustainedBackgroundSoundStopsOpeningPhrasesButSpeechStillDoes()
        {
            var vad = new VoiceActivityDetector(new VoiceActivitySettings(), Rate);
            var phrases = Feed(vad, Join(Tone(20, .03), Tone(1.2, .3), Tone(2, .03)));
            // The hum may open one phrase before the floor adapts; afterwards only the louder speech does.
            Assert.That(phrases.Count, Is.InRange(1, 2));
            Assert.That(phrases[phrases.Count - 1].VoicedSeconds, Is.EqualTo(1.2).Within(.1));
            Assert.That(vad.NoiseLevel, Is.GreaterThan(.02f));
        }

        [Test]
        public void ResetDiscardsAHalfSpokenPhrase()
        {
            var vad = new VoiceActivityDetector(new VoiceActivitySettings(), Rate);
            var phrases = Feed(vad, Join(Silence(.5), Tone(.8, .2)));
            vad.Reset();
            phrases.AddRange(Feed(vad, Silence(1.5)));
            Assert.That(phrases, Is.Empty);
        }

        [Test]
        public void InvalidDetectorSettingsAreRejected()
        {
            Assert.That(new VoiceActivitySettings().Validate(), Is.Null);
            Assert.Throws<ArgumentException>(() => new VoiceActivityDetector(new VoiceActivitySettings { MaximumPhraseMilliseconds = 500 }, Rate));
            Assert.Throws<ArgumentException>(() => new VoiceActivityDetector(new VoiceActivitySettings { SpeechToNoiseRatio = .5f }, Rate));
            Assert.Throws<ArgumentOutOfRangeException>(() => new VoiceActivityDetector(new VoiceActivitySettings(), 4000));
        }

        [Test]
        public void RecognizerInputIsResampledAndPaddedToTheBackendMinimum()
        {
            float[] resampled = AudioPreparation.ToRecognizerInput(new float[48000 * 2], 48000);
            Assert.That(resampled.Length, Is.EqualTo(32000));
            float[] padded = AudioPreparation.ToRecognizerInput(new float[8000], 16000);
            Assert.That(padded.Length, Is.EqualTo(SpeechRecognitionWorker.MinimumRecognitionSamples));
        }

        [Test]
        public void WorkerRecognizesOffTheCallingThreadAndDisposesTheBackendThere()
        {
            var fake = new FakeRecognizer();
            var worker = new SpeechRecognitionWorker(() => fake);
            worker.Start();
            WaitUntil(() => worker.State == RecognitionWorkerState.Ready);
            Assert.That(worker.Submit(Request(1, 1.5)), Is.True);
            SpeechResponse response = Take(worker);
            Assert.That(response.Sequence, Is.EqualTo(1));
            Assert.That(response.Error, Is.Null);
            Assert.That(response.Result.Text, Is.EqualTo("phrase 1"));
            Assert.That(fake.RecognizeThread, Is.Not.EqualTo(Thread.CurrentThread.ManagedThreadId), "inference never runs on the caller (main) thread");
            Assert.That(fake.LastSamples, Is.EqualTo(24000), "a 1.5 s phrase reaches the backend unpadded at 16 kHz");
            Assert.That(worker.Stop(2000), Is.True);
            Assert.That(worker.State, Is.EqualTo(RecognitionWorkerState.Stopped));
            Assert.That(fake.Disposals, Is.EqualTo(1));
            Assert.That(fake.DisposeThread, Is.EqualTo(fake.RecognizeThread), "the model is freed on the worker thread");
            Assert.That(worker.Submit(Request(2, 1)), Is.False, "a stopped worker accepts nothing");
        }

        [Test]
        public void ModelMissingFailsOnceAndStopsAccepting()
        {
            var fake = new FakeRecognizer { InitializeError = VoiceFailure.ModelMissing };
            var worker = new SpeechRecognitionWorker(() => fake);
            worker.Start();
            WaitUntil(() => worker.State == RecognitionWorkerState.Failed);
            Assert.That(worker.Error, Is.EqualTo(VoiceFailure.ModelMissing));
            Assert.That(worker.Submit(Request(1, 1)), Is.False);
            Assert.That(worker.Stop(2000), Is.True);
            Assert.That(fake.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void MissingNativeBackendIsReportedAsUnavailable()
        {
            var worker = new SpeechRecognitionWorker(() => throw new DllNotFoundException("libwhisper"));
            worker.Start();
            WaitUntil(() => worker.State == RecognitionWorkerState.Failed);
            Assert.That(worker.Error, Is.EqualTo(VoiceFailure.BackendUnavailable));
            worker.Dispose();
        }

        [Test]
        public void InferenceExceptionAffectsOnlyThatPhrase()
        {
            var fake = new FakeRecognizer { ThrowOnSequenceText = "phrase 1" };
            var worker = new SpeechRecognitionWorker(() => fake);
            worker.Start();
            WaitUntil(() => worker.State == RecognitionWorkerState.Ready);
            worker.Submit(Request(1, 1));
            worker.Submit(Request(2, 1));
            SpeechResponse failed = Take(worker);
            SpeechResponse next = Take(worker);
            Assert.That(failed.Error, Is.EqualTo(VoiceFailure.RecognitionFailed));
            Assert.That(next.Error, Is.Null);
            Assert.That(next.Result.Text, Is.EqualTo("phrase 2"));
            Assert.That(worker.State, Is.EqualTo(RecognitionWorkerState.Ready));
            worker.Dispose();
        }

        [Test]
        public void QueueIsBoundedAndDropsTheOldestWaitingPhrase()
        {
            var fake = new FakeRecognizer { Gate = new ManualResetEventSlim(false) };
            var worker = new SpeechRecognitionWorker(() => fake, 2);
            worker.Start();
            WaitUntil(() => worker.State == RecognitionWorkerState.Ready);
            worker.Submit(Request(1, 1));
            WaitUntil(() => fake.Started == 1);
            for (long sequence = 2; sequence <= 6; sequence++) worker.Submit(Request(sequence, 1));
            Assert.That(worker.DroppedRequests, Is.EqualTo(3));
            fake.Gate.Set();
            var sequences = new List<long> { Take(worker).Sequence, Take(worker).Sequence, Take(worker).Sequence };
            Assert.That(sequences, Is.EqualTo(new long[] { 1, 5, 6 }));
            worker.Dispose();
        }

        [Test]
        public void StopDuringInferenceNeverFreesTheModelUnderIt()
        {
            var fake = new FakeRecognizer { Gate = new ManualResetEventSlim(false) };
            var worker = new SpeechRecognitionWorker(() => fake);
            worker.Start();
            WaitUntil(() => worker.State == RecognitionWorkerState.Ready);
            worker.Submit(Request(1, 1));
            WaitUntil(() => fake.Started == 1);
            Assert.That(worker.Stop(50), Is.False, "the phrase is still being recognized");
            Assert.That(fake.Disposals, Is.Zero);
            fake.Gate.Set();
            WaitUntil(() => fake.Disposals == 1);
            Assert.That(fake.DisposeThread, Is.EqualTo(fake.RecognizeThread));
            Assert.That(worker.TryTake(out _), Is.False, "a result finished after Stop is discarded");
        }

        [Test]
        public void DisabledRecognitionIgnoresAudioAndStartsNothing()
        {
            int created = 0;
            var recognition = new VoiceRecognition(new VoiceActivitySettings(), () => { created++; return new FakeRecognizer(); });
            var heard = new List<RecognizedSpeech>();
            recognition.Recognized += heard.Add;
            recognition.SetEnabled(false);
            Assert.That(recognition.Status, Is.EqualTo(VoiceStatus.Disabled));
            recognition.BeginListening(Rate);
            SubmitAll(recognition, Join(Silence(.5), Tone(1.2, .2), Silence(1.5)));
            Pump(recognition, 300);
            Assert.That(heard, Is.Empty);
            Assert.That(created, Is.Zero, "the microphone path and the model stay closed");
            recognition.Dispose();
        }

        [Test]
        public void SpokenPhraseBecomesRecognizedSpeechOnceWithLanguageAndTimestamp()
        {
            double now = 100;
            var fake = new FakeRecognizer();
            var recognition = new VoiceRecognition(new VoiceActivitySettings(), () => fake, () => now);
            var heard = new List<RecognizedSpeech>();
            recognition.Recognized += heard.Add;
            recognition.Language = SpeechLanguage.English;
            recognition.FallbackLanguage = SpeechLanguage.Russian;
            recognition.BeginListening(Rate);
            WaitUntil(() => { recognition.Update(); return recognition.Status == VoiceStatus.Listening; });
            SubmitAll(recognition, Join(Silence(.5), Tone(1.2, .2), Silence(1.5)));
            WaitUntil(() => { recognition.Update(); return heard.Count == 1; });
            Pump(recognition, 100);
            Assert.That(heard.Count, Is.EqualTo(1));
            Assert.That(heard[0].Text, Is.EqualTo("phrase 1"));
            Assert.That(heard[0].Language, Is.EqualTo("en"));
            Assert.That(heard[0].Confidence, Is.EqualTo(.9f));
            Assert.That(heard[0].Timestamp, Is.EqualTo(100 + .5 + 1.2 + .12).Within(.03), "phrase end on the capture clock");
            Assert.That(fake.Languages, Is.EqualTo(new[] { "en|ru" }));
            recognition.Language = SpeechLanguage.Auto;
            SubmitAll(recognition, Join(Tone(1.2, .2), Silence(1.5)));
            WaitUntil(() => { recognition.Update(); return heard.Count == 2; });
            Assert.That(fake.Languages[1], Is.EqualTo("auto|ru"), "a language switch applies to the next phrase");
            recognition.Dispose();
            Assert.That(fake.Disposals, Is.EqualTo(1));
        }

        [Test]
        public void EmptyAndWhitespaceRecognitionIsIgnored()
        {
            var fake = new FakeRecognizer { Texts = new Queue<string>(new[] { "", "   \n ", "real words" }) };
            var recognition = new VoiceRecognition(new VoiceActivitySettings(), () => fake);
            var heard = new List<RecognizedSpeech>();
            recognition.Recognized += heard.Add;
            recognition.BeginListening(Rate);
            WaitUntil(() => { recognition.Update(); return recognition.Status == VoiceStatus.Listening; });
            for (int i = 0; i < 3; i++)
            {
                SubmitAll(recognition, Join(Tone(1.2, .2), Silence(1.5)));
                int expected = i + 1;
                WaitUntil(() => { recognition.Update(); return fake.Recognized == expected; });
            }
            Pump(recognition, 100);
            Assert.That(heard.Count, Is.EqualTo(1));
            Assert.That(heard[0].Text, Is.EqualTo("real words"));
            recognition.Dispose();
        }

        [Test]
        public void EndListeningDiscardsAPartialPhraseAndKeepsTheModel()
        {
            var fake = new FakeRecognizer();
            var recognition = new VoiceRecognition(new VoiceActivitySettings(), () => fake);
            var heard = new List<RecognizedSpeech>();
            recognition.Recognized += heard.Add;
            recognition.BeginListening(Rate);
            WaitUntil(() => { recognition.Update(); return recognition.Status == VoiceStatus.Listening; });
            SubmitAll(recognition, Join(Silence(.2), Tone(.9, .2)));
            recognition.EndListening();
            Assert.That(recognition.Status, Is.EqualTo(VoiceStatus.Idle));
            SubmitAll(recognition, Silence(1.5));
            Pump(recognition, 200);
            Assert.That(heard, Is.Empty);
            Assert.That(fake.Disposals, Is.Zero);
            recognition.Dispose();
        }

        [Test]
        public void StatusReportsMicrophoneModelAndBackendFailuresWithoutThrowing()
        {
            var missing = new VoiceRecognition(new VoiceActivitySettings(), () => new FakeRecognizer { InitializeError = VoiceFailure.ModelMissing });
            missing.BeginListening(Rate);
            WaitUntil(() => { missing.Update(); return missing.Status == VoiceStatus.ModelMissing; });
            SubmitAll(missing, Join(Tone(1.2, .2), Silence(1.5)));
            missing.Update();
            missing.Dispose();

            var broken = new VoiceRecognition(new VoiceActivitySettings(), () => throw new DllNotFoundException("libwhisper"));
            broken.BeginListening(Rate);
            WaitUntil(() => { broken.Update(); return broken.Status == VoiceStatus.RecognizerUnavailable; });
            broken.Dispose();

            var unplugged = new VoiceRecognition(new VoiceActivitySettings(), () => new FakeRecognizer());
            unplugged.BeginListening(Rate);
            unplugged.MicrophoneUnavailable();
            Assert.That(unplugged.Status, Is.EqualTo(VoiceStatus.MicrophoneUnavailable));
            unplugged.BeginListening(Rate);
            Assert.That(unplugged.Status, Is.Not.EqualTo(VoiceStatus.MicrophoneUnavailable), "a reopened microphone recovers");
            unplugged.Dispose();
        }

        [Test]
        public void DisablingReleasesTheModelWithoutBlocking()
        {
            var fake = new FakeRecognizer();
            var recognition = new VoiceRecognition(new VoiceActivitySettings(), () => fake);
            recognition.BeginListening(Rate);
            WaitUntil(() => { recognition.Update(); return recognition.Status == VoiceStatus.Listening; });
            var clock = Stopwatch.StartNew();
            recognition.SetEnabled(false);
            Assert.That(clock.ElapsedMilliseconds, Is.LessThan(500));
            WaitUntil(() => fake.Disposals == 1);
            Assert.That(recognition.Status, Is.EqualTo(VoiceStatus.Disabled));
            recognition.Dispose();
        }

        [TestCase("[Bell]", "")]
        [TestCase("(музыка)", "")]
        [TestCase("♪ ♪", "")]
        [TestCase(" Редактор субтитров А.Синецкая Корректор А.Сухиашвили", "")]
        [TestCase("Subtitles by the Amara.org community", "")]
        [TestCase(" Привет, чат! ", "Привет, чат!")]
        [TestCase("Hello [laughs] chat,  let's go", "Hello chat, let's go")]
        public void WhisperArtifactsAreNotSpeech(string raw, string expected)
        {
            Assert.That(WhisperArtifacts.Clean(raw), Is.EqualTo(expected));
        }

        [Test]
        public void RecognizedSpeechIsAPlainValueWithText()
        {
            Assert.Throws<ArgumentException>(() => new RecognizedSpeech(1, "  ", 0, null, "ru"));
            var speech = new RecognizedSpeech(1, "  Привет  ", 2.5, null, null);
            Assert.That(speech.Text, Is.EqualTo("Привет"));
            Assert.That(speech.Language, Is.Empty);
            Assert.That(speech.Confidence, Is.Null);
            foreach (var property in typeof(RecognizedSpeech).GetProperties())
                Assert.That(property.PropertyType.Namespace, Does.Not.StartWith("UnityEngine"), "no audio/device/Unity references in speech events");
        }

        internal sealed class FakeRecognizer : ISpeechRecognizer
        {
            public string InitializeError;
            public string ThrowOnSequenceText;
            public ManualResetEventSlim Gate;
            public Queue<string> Texts;
            public readonly List<string> Languages = new();
            public int Recognized;
            public int Started;
            public int Disposals;
            public int LastSamples;
            public int RecognizeThread;
            public int DisposeThread;

            public string Initialize() => InitializeError;

            public SpeechRecognitionResult Recognize(float[] samples16k, string language, string fallbackLanguage)
            {
                Interlocked.Increment(ref Started);
                RecognizeThread = Thread.CurrentThread.ManagedThreadId;
                LastSamples = samples16k.Length;
                lock (Languages) Languages.Add(language + "|" + fallbackLanguage);
                Gate?.Wait(5000);
                int count = Interlocked.Increment(ref Recognized);
                string text = Texts != null && Texts.Count > 0 ? Texts.Dequeue() : "phrase " + count;
                if (text == ThrowOnSequenceText) throw new InvalidOperationException("backend failure");
                return new SpeechRecognitionResult(text, .9f, language == "auto" ? fallbackLanguage : language);
            }

            public void Dispose()
            {
                DisposeThread = Thread.CurrentThread.ManagedThreadId;
                Interlocked.Increment(ref Disposals);
            }
        }

        internal static SpeechRequest Request(long sequence, double seconds) =>
            new(sequence, new float[(int)(seconds * Rate)], Rate, sequence, "auto", "ru");

        internal static float[] Silence(double seconds) => new float[(int)(seconds * Rate)];

        // Continuous talking: 250 ms voiced syllables separated by 60 ms gaps (as between words).
        internal static float[] Syllables(double seconds, double amplitude)
        {
            float[] samples = Tone(seconds, amplitude);
            int period = Rate * 310 / 1000, gap = Rate * 60 / 1000;
            for (int i = 0; i < samples.Length; i++) if (i % period >= period - gap) samples[i] = 0;
            return samples;
        }

        // A loud periodic signal: "speech" for an energy detector.
        internal static float[] Tone(double seconds, double amplitude)
        {
            var samples = new float[(int)(seconds * Rate)];
            for (int i = 0; i < samples.Length; i++) samples[i] = (float)(amplitude * 1.41 * Math.Sin(2 * Math.PI * 220 * i / Rate));
            return samples;
        }

        internal static float[] Join(params float[][] parts)
        {
            int length = 0;
            foreach (float[] part in parts) length += part.Length;
            var joined = new float[length];
            int offset = 0;
            foreach (float[] part in parts)
            {
                Array.Copy(part, 0, joined, offset, part.Length);
                offset += part.Length;
            }
            return joined;
        }

        internal static void SubmitAll(VoiceRecognition recognition, float[] samples)
        {
            var block = new float[320];
            for (int offset = 0; offset + block.Length <= samples.Length; offset += block.Length)
            {
                Array.Copy(samples, offset, block, 0, block.Length);
                recognition.Submit(block, block.Length);
            }
        }

        internal static void WaitUntil(Func<bool> condition)
        {
            var clock = Stopwatch.StartNew();
            while (!condition())
            {
                if (clock.ElapsedMilliseconds > 5000) Assert.Fail("condition not reached in 5 s");
                Thread.Sleep(2);
            }
        }

        private static List<SpeechSegment> Feed(VoiceActivityDetector vad, float[] samples)
        {
            var phrases = new List<SpeechSegment>();
            for (int offset = 0; offset < samples.Length; offset += 441)
                vad.Process(samples, offset, Math.Min(441, samples.Length - offset), phrases);
            return phrases;
        }

        private static SpeechResponse Take(SpeechRecognitionWorker worker)
        {
            SpeechResponse response = default;
            WaitUntil(() => worker.TryTake(out response));
            return response;
        }

        private static void Pump(VoiceRecognition recognition, int milliseconds)
        {
            var clock = Stopwatch.StartNew();
            while (clock.ElapsedMilliseconds < milliseconds)
            {
                recognition.Update();
                Thread.Sleep(5);
            }
        }
    }
}
