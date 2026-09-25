using System;
using System.Collections.Generic;
using GoLive.Voice;

namespace GoLive.Desktop
{
    // The player's recognized speech as a stream event: what future viewer reactions may respond to.
    public sealed class StreamSpeechEvent
    {
        public RecognizedSpeech Speech { get; }
        // Broadcast time (StreamSession.DurationSeconds) when the phrase entered the stream.
        public double StreamSeconds { get; }

        internal StreamSpeechEvent(RecognizedSpeech speech, double streamSeconds)
        {
            Speech = speech;
            StreamSeconds = streamSeconds;
        }
    }

    // Admits recognized phrases into the current broadcast only while it is Live. A phrase must end after the
    // broadcast went live and arrive before it stops; offline speech, speech during Starting and results that
    // finish after Stop never become stream events. Transient: the recent list only holds the current live
    // broadcast's phrases and is never saved. It depends on the stream, never on the fictional PC microphone.
    public sealed class StreamSpeechFeed : IDisposable
    {
        public const int MaximumRecentPhrases = 20;
        private readonly StreamSession _stream;
        private readonly Func<double> _clock;
        private readonly List<StreamSpeechEvent> _recent = new();
        private bool _live;
        private double _liveSince;
        private long _lastSequence = long.MinValue;

        public IReadOnlyList<StreamSpeechEvent> Recent { get; }
        public event Action<StreamSpeechEvent> SpeechAdded;

        public StreamSpeechFeed(StreamSession stream, Func<double> clock = null)
        {
            _stream = stream ?? throw new ArgumentNullException(nameof(stream));
            _clock = clock ?? (() => SpeechClock.Now);
            Recent = _recent.AsReadOnly();
            _stream.Changed += ObserveStream;
            ObserveStream();
        }

        // Returns true when the phrase became a stream event.
        public bool Offer(RecognizedSpeech speech)
        {
            if (speech == null || string.IsNullOrWhiteSpace(speech.Text)) return false;
            ObserveStream();
            if (!_live || speech.Timestamp < _liveSince || speech.Sequence <= _lastSequence) return false;
            _lastSequence = speech.Sequence;
            var entry = new StreamSpeechEvent(speech, _stream.DurationSeconds);
            if (_recent.Count == MaximumRecentPhrases) _recent.RemoveAt(0);
            _recent.Add(entry);
            SpeechAdded?.Invoke(entry);
            return true;
        }

        public void Dispose() => _stream.Changed -= ObserveStream;

        private void ObserveStream()
        {
            bool live = _stream.State == StreamState.Live;
            if (live == _live) return;
            _live = live;
            // Phrases belong to one live broadcast: cleared when it starts and when it ends (or is discarded by a load).
            _recent.Clear();
            if (live) _liveSince = _clock();
        }
    }
}
