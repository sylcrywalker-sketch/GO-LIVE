using System;
using System.Linq;
using GoLive.Voice;
using NUnit.Framework;

namespace GoLive.Tests
{
    public sealed class AudioPreparationContextTests
    {
        private const int ContextSamples = 8000; // 500 ms at the recognizer's 16 kHz rate.

        [TestCase(8000)]
        [TestCase(32000)]
        public void DecoderContextSurroundsTheWholePhraseWithoutChangingItsSamples(int length)
        {
            float[] input = Enumerable.Range(0, length).Select(i => i % 2 == 0 ? .25f : -.5f).ToArray();
            float[] unchanged = (float[])input.Clone();
            float[] prepared = AudioPreparation.ToRecognizerInput(input, 16000);
            Assert.That(prepared.Length, Is.EqualTo(Math.Max(length, SpeechRecognitionWorker.MinimumRecognitionSamples) + 2 * ContextSamples));
            Assert.That(prepared.Take(ContextSamples), Is.All.Zero, "leading context is zeros, never another captured phrase");
            Assert.That(prepared.Skip(ContextSamples).Take(length), Is.EqualTo(input), "the original phrase stays intact at a known offset");
            Assert.That(prepared.Skip(ContextSamples + length), Is.All.Zero, "minimum-length padding and trailing context contain no invented samples");
            Assert.That(input, Is.EqualTo(unchanged), "preparation does not mutate capture-owned memory");
        }

        [Test]
        public void DecoderContextIsAddedAfterResamplingToSixteenKilohertz()
        {
            float[] input = { .1f, .2f, .3f, .4f, .5f, .6f, .7f, .8f };
            float[] prepared = AudioPreparation.ToRecognizerInput(input, 32000);
            Assert.That(prepared.Length, Is.EqualTo(SpeechRecognitionWorker.MinimumRecognitionSamples + 2 * ContextSamples));
            Assert.That(prepared.Take(ContextSamples), Is.All.Zero);
            Assert.That(prepared.Skip(ContextSamples).Take(4), Is.EqualTo(new[] { .1f, .3f, .5f, .7f }));
            Assert.That(prepared.Skip(ContextSamples + 4), Is.All.Zero);
        }
    }
}
