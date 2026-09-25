using System;
using System.Diagnostics;

namespace GoLive.Voice
{
    // Recognition language. Auto lets the backend detect Russian or English.
    public enum SpeechLanguage { Auto, Russian, English }

    // One recognized phrase of the player's real voice. A plain value: no audio, device or Unity references.
    public sealed class RecognizedSpeech
    {
        public long Sequence { get; }
        public string Text { get; }
        // SpeechClock seconds when the phrase ended.
        public double Timestamp { get; }
        // Mean token probability reported by the backend (uncalibrated); null when unavailable.
        public float? Confidence { get; }
        // ISO 639-1 code ("ru", "en"), or empty when unknown.
        public string Language { get; }

        public RecognizedSpeech(long sequence, string text, double timestamp, float? confidence, string language)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Recognized speech needs text.", nameof(text));
            if (double.IsNaN(timestamp) || double.IsInfinity(timestamp)) throw new ArgumentOutOfRangeException(nameof(timestamp));
            Sequence = sequence;
            Text = text.Trim();
            Timestamp = timestamp;
            Confidence = confidence;
            Language = language ?? "";
        }
    }

    // Monotonic real-time clock shared by voice capture and stream gating (independent of game time scale).
    public static class SpeechClock
    {
        public static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;
    }

    public static class SpeechLanguageCodes
    {
        public static string Code(SpeechLanguage language) => language switch
        {
            SpeechLanguage.Russian => "ru",
            SpeechLanguage.English => "en",
            _ => "auto"
        };
    }
}
