using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using GoLive.Editor.Voice;
using GoLive.Voice;
using NUnit.Framework;
using static GoLive.Tests.VoicePipelineTests;

namespace GoLive.Tests
{
    public sealed class VoiceEvidenceRegressionTests
    {
        private string _directory;

        [SetUp] public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "GoLiveVoiceTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
        }

        [TearDown] public void TearDown() => Directory.Delete(_directory, true);

        [Test]
        public void WavRoundTripPreservesRateCountAndClampedSamples()
        {
            string path = Path.Combine(_directory, "round-trip.wav");
            float[] input = { -2, -1, -.5f, 0, .5f, 1, 2, .123f };
            WavFile.Write(path, input, input.Length - 1, 48000);
            float[] read = WavFile.Read(path, out int rate);
            Assert.That(rate, Is.EqualTo(48000));
            Assert.That(read.Length, Is.EqualTo(input.Length - 1));
            for (int i = 0; i < read.Length; i++)
                Assert.That(read[i], Is.EqualTo(Math.Clamp(input[i], -1, 1)).Within(2.0 / 32768));
        }

        [TestCase(-1, 16000)]
        [TestCase(2, 16000)]
        [TestCase(1, 0)]
        [TestCase(1, -1)]
        [TestCase(1, int.MaxValue)]
        public void WavInvalidWriteArgumentsDoNotOverwriteAnExistingRecording(int count, int rate)
        {
            string path = Path.Combine(_directory, "preserved.wav");
            byte[] original = { 9, 8, 7 };
            File.WriteAllBytes(path, original);
            Assert.Throws<ArgumentOutOfRangeException>(() => WavFile.Write(path, new float[1], count, rate));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

        [TestCase("non-pcm")]
        [TestCase("zero-channels")]
        [TestCase("negative-channels")]
        [TestCase("zero-rate")]
        [TestCase("wrong-bits")]
        [TestCase("short-fmt")]
        [TestCase("oversized-chunk")]
        [TestCase("negative-chunk")]
        [TestCase("truncated-data")]
        [TestCase("partial-frame")]
        public void WavMalformedInputHasABoundedInvalidDataFailure(string kind)
        {
            byte[] bytes = PcmWav();
            switch (kind)
            {
                case "non-pcm": Put(bytes, 20, (short)3); break;
                case "zero-channels": Put(bytes, 22, (short)0); break;
                case "negative-channels": Put(bytes, 22, (short)-1); break;
                case "zero-rate": Put(bytes, 24, 0); break;
                case "wrong-bits": Put(bytes, 34, (short)8); break;
                case "short-fmt": Put(bytes, 16, 2); break;
                case "oversized-chunk": Put(bytes, 16, int.MaxValue); break;
                case "negative-chunk": Put(bytes, 16, -1); break;
                case "truncated-data": Array.Resize(ref bytes, bytes.Length - 2); break;
                case "partial-frame": Put(bytes, 40, 3); break;
            }
            string path = Path.Combine(_directory, kind + ".wav");
            File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => WavFile.Read(path, out _));
        }

        [Test]
        public void WavSkipsOddPaddedChunksAndReadsFirstStereoChannel()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(0); writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("JUNK")); writer.Write(1); writer.Write((byte)42); writer.Write((byte)0);
            writer.Write(Encoding.ASCII.GetBytes("fmt ")); writer.Write(16); writer.Write((short)1); writer.Write((short)2);
            writer.Write(16000); writer.Write(64000); writer.Write((short)4); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(8);
            writer.Write((short)16384); writer.Write((short)-123); writer.Write((short)-16384); writer.Write((short)123);
            byte[] bytes = stream.ToArray(); Put(bytes, 4, bytes.Length - 8);
            string path = Path.Combine(_directory, "stereo.wav"); File.WriteAllBytes(path, bytes);
            Assert.That(WavFile.Read(path, out int rate), Is.EqualTo(new[] { .5f, -.5f }));
            Assert.That(rate, Is.EqualTo(16000));
        }

        [Test]
        public void DiagnosticOutcomesIncludeEmptyFailedAndSuccessfulPhrasesExactlyOnce()
        {
            var fake = new FakeRecognizer { Texts = new Queue<string>(new[] { "", "broken", "Привет, парни!" }), ThrowOnSequenceText = "broken" };
            using var voice = new VoiceRecognition(new VoiceActivitySettings(), () => fake, () => 100);
            var finished = new List<PhraseDiagnostics>();
            var heard = new List<RecognizedSpeech>();
            var queued = new List<long>();
            voice.PhraseFinished += finished.Add; voice.Recognized += heard.Add;
            voice.PhraseQueued += (sequence, segment) => queued.Add(sequence);
            voice.BeginListening(16000);
            WaitUntil(() => { voice.Update(); return voice.IsListening; });
            for (int i = 0; i < 3; i++)
            {
                SubmitAll(voice, Join(Tone(1.2, .2), Silence(1.5)));
                int expected = i + 1;
                WaitUntil(() => { voice.Update(); return finished.Count == expected; });
            }
            voice.Update();
            Assert.That(finished.Count, Is.EqualTo(3));
            Assert.That(queued, Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(finished[0].Text, Is.Empty);
            Assert.That(finished[0].Error, Is.Null);
            Assert.That(finished[1].Error, Is.EqualTo(VoiceFailure.RecognitionFailed));
            Assert.That(finished[2].Text, Is.EqualTo("Привет, парни!"));
            Assert.That(finished[2].AudioSeconds, Is.GreaterThan(1));
            Assert.That(finished[2].VoicedSeconds, Is.GreaterThan(0));
            Assert.That(heard.Count, Is.EqualTo(1));
            Assert.That(voice.FailedPhrases, Is.EqualTo(1));
        }

        [TestCase("captured")]
        [TestCase("queued")]
        [TestCase("finished")]
        public void DiagnosticListenerMayDisposeThePipelineDuringItsCallback(string callback)
        {
            var fake = new FakeRecognizer { Texts = new Queue<string>(new[] { "" }) };
            using var voice = new VoiceRecognition(new VoiceActivitySettings(), () => fake);
            bool disposed = false;
            Action dispose = () => { disposed = true; voice.Dispose(); };
            if (callback == "captured") voice.Captured += (_, _, _) => dispose();
            if (callback == "queued") voice.PhraseQueued += (_, _) => dispose();
            if (callback == "finished") voice.PhraseFinished += _ => dispose();
            voice.BeginListening(16000);
            WaitUntil(() => { voice.Update(); return voice.IsListening; });
            float[] audio = Join(Tone(1.2, .2), Silence(1.5), Tone(1.2, .2), Silence(1.5));
            Assert.DoesNotThrow(() => voice.Submit(audio, audio.Length));
            Assert.DoesNotThrow(() => WaitUntil(() => { voice.Update(); return disposed; }));
            Assert.That(voice.IsListening, Is.False);
        }

        [Test]
        public void DisablingRecognitionClearsAbandonedDiagnosticAudio()
        {
            using var gate = new ManualResetEventSlim(false);
            var fake = new FakeRecognizer { Gate = gate };
            using var voice = new VoiceRecognition(new VoiceActivitySettings(), () => fake);
            voice.PhraseFinished += _ => { };
            voice.BeginListening(16000);
            WaitUntil(() => { voice.Update(); return voice.IsListening; });
            try
            {
                SubmitAll(voice, Join(Tone(1.2, .2), Silence(1.5)));
                WaitUntil(() => fake.Started == 1);
                Dictionary<long, SpeechSegment> cache = DiagnosticCache(voice);
                Assert.That(cache.Count, Is.EqualTo(1));
                Assert.That(cache[1].Samples.Length, Is.GreaterThan(0), "the diagnostics retain captured audio before cancellation");

                voice.SetEnabled(false);

                Assert.That(cache, Is.Empty, "canceled responses cannot release diagnostics through Update");
                Assert.That(voice.Status, Is.EqualTo(VoiceStatus.Disabled));
            }
            finally
            {
                gate.Set();
                voice.SetEnabled(false);
                WaitUntil(() => fake.Disposals == 1);
            }
        }

        [Test]
        public void DiagnosticCacheEvictsOldestRetainedPhraseAcrossListenerGaps()
        {
            using var gate = new ManualResetEventSlim(false);
            var fake = new FakeRecognizer { Gate = gate };
            using var voice = new VoiceRecognition(new VoiceActivitySettings(), () => fake);
            Action<PhraseDiagnostics> listener = _ => { };
            voice.PhraseFinished += listener;
            voice.BeginListening(16000);
            WaitUntil(() => { voice.Update(); return voice.IsListening; });
            float[] phrase = Join(Tone(1.2, .2), Silence(1.5));
            try
            {
                SubmitAll(voice, phrase);
                WaitUntil(() => fake.Started == 1);
                voice.PhraseFinished -= listener;
                for (int i = 0; i < 33; i++) SubmitAll(voice, phrase);
                voice.PhraseFinished += listener;
                for (int i = 0; i < 40; i++) SubmitAll(voice, phrase);

                Dictionary<long, SpeechSegment> cache = DiagnosticCache(voice);
                Assert.That(cache.Count, Is.EqualTo(32), "the bound counts retained entries, not contiguous phrase sequences");
                for (long sequence = 43; sequence <= 74; sequence++)
                    Assert.That(cache.ContainsKey(sequence), Is.True, "the newest diagnostic audio remains available");
            }
            finally
            {
                gate.Set();
                voice.SetEnabled(false);
                WaitUntil(() => fake.Disposals == 1);
            }
        }

        [Test]
        public void RecognizedListenerMayDisableWithoutThrowingOrDeliveringPendingResults()
        {
            using var gate = new ManualResetEventSlim(false);
            var fake = new FakeRecognizer { Gate = gate };
            using var voice = new VoiceRecognition(new VoiceActivitySettings(), () => fake);
            var heard = new List<RecognizedSpeech>();
            voice.Recognized += speech => { heard.Add(speech); voice.SetEnabled(false); };
            voice.BeginListening(16000);
            WaitUntil(() => { voice.Update(); return voice.IsListening; });
            try
            {
                float[] phrase = Join(Tone(1.2, .2), Silence(1.5));
                SubmitAll(voice, phrase);
                WaitUntil(() => fake.Started == 1);
                SubmitAll(voice, phrase);
                gate.Set();
                WaitUntil(() => PendingResponseCount(voice) == 2);

                Assert.DoesNotThrow(voice.Update);
                Assert.DoesNotThrow(voice.Update);
                Assert.That(heard.Count, Is.EqualTo(1), "shutdown abandons the second already finished response");
                Assert.That(heard[0].Sequence, Is.EqualTo(1));
                Assert.That(voice.LastRecognized.Sequence, Is.EqualTo(1));
                Assert.That(voice.Status, Is.EqualTo(VoiceStatus.Disabled));
            }
            finally
            {
                gate.Set();
                voice.SetEnabled(false);
                WaitUntil(() => fake.Disposals == 1);
            }
        }

        private static Dictionary<long, SpeechSegment> DiagnosticCache(VoiceRecognition voice) =>
            (Dictionary<long, SpeechSegment>)typeof(VoiceRecognition)
                .GetField("_queued", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(voice);

        private static int PendingResponseCount(VoiceRecognition voice)
        {
            var worker = (SpeechRecognitionWorker)typeof(VoiceRecognition)
                .GetField("_worker", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(voice);
            object gate = typeof(SpeechRecognitionWorker).GetField("_gate", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(worker);
            var responses = (Queue<SpeechResponse>)typeof(SpeechRecognitionWorker)
                .GetField("_responses", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(worker);
            lock (gate) return responses.Count;
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("  \t ")]
        [TestCase(" Привет, парни! 🎮 ")]
        public void WhisperPromptUsesNullTerminatedUtf8WithoutAnsiLoss(string prompt)
        {
            // Test the exact allocator used by Initialize without loading a model or native DLL.
            MethodInfo allocate = typeof(WhisperSpeechRecognizer).GetMethod("Utf8", BindingFlags.NonPublic | BindingFlags.Static);
            IntPtr pointer = (IntPtr)allocate.Invoke(null, new object[] { prompt });
            try
            {
                if (string.IsNullOrWhiteSpace(prompt)) { Assert.That(pointer, Is.EqualTo(IntPtr.Zero)); return; }
                byte[] expected = Encoding.UTF8.GetBytes(prompt.Trim());
                var actual = new byte[expected.Length]; Marshal.Copy(pointer, actual, 0, actual.Length);
                Assert.That(actual, Is.EqualTo(expected));
                Assert.That(Marshal.ReadByte(pointer, actual.Length), Is.Zero);
                // Ownership is transferred to the recognizer; its repeated Dispose must release and zero the handle.
                using var recognizer = new WhisperSpeechRecognizer(Array.Empty<string>(), 1, false);
                FieldInfo field = typeof(WhisperSpeechRecognizer).GetField("_russianPrompt", BindingFlags.NonPublic | BindingFlags.Instance);
                field.SetValue(recognizer, pointer); pointer = IntPtr.Zero;
                recognizer.Dispose(); recognizer.Dispose();
                Assert.That(field.GetValue(recognizer), Is.EqualTo(IntPtr.Zero));
            }
            finally { if (pointer != IntPtr.Zero) Marshal.FreeHGlobal(pointer); }
        }

        private static byte[] PcmWav()
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(40); writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1); writer.Write(16000); writer.Write(32000);
            writer.Write((short)2); writer.Write((short)16); writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(4);
            writer.Write((short)16384); writer.Write((short)-16384);
            return stream.ToArray();
        }

        private static void Put(byte[] bytes, int offset, short value) => BitConverter.GetBytes(value).CopyTo(bytes, offset);
        private static void Put(byte[] bytes, int offset, int value) => BitConverter.GetBytes(value).CopyTo(bytes, offset);
    }
}
