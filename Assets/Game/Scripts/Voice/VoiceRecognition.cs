using System;
using System.Collections.Generic;

namespace GoLive.Voice
{
    public enum VoiceStatus
    {
        // Player turned recognition off: the microphone is never opened.
        Disabled,
        // Enabled and ready; nothing currently asks to listen.
        Idle,
        // Listening requested; the speech model is loading.
        Loading,
        Listening,
        MicrophoneUnavailable,
        ModelMissing,
        RecognizerUnavailable
    }

    // Main-thread coordinator of the real voice pipeline after capture: VAD -> worker recognizer ->
    // RecognizedSpeech. It owns the VAD of the current listening session, phrase numbering, result
    // de-duplication and the concise status. Audio arrives from a capture bridge; it never touches Unity.
    public sealed class VoiceRecognition : IDisposable
    {
        private readonly VoiceActivitySettings _vadSettings;
        private readonly Func<ISpeechRecognizer> _createRecognizer;
        private readonly Func<double> _clock;
        private readonly List<SpeechSegment> _segments = new();
        private SpeechRecognitionWorker _worker;
        private VoiceActivityDetector _vad;
        private double _captureStart;
        private long _nextSequence;
        private long _lastDelivered;
        private bool _enabled = true;
        private bool _listening;
        private bool _microphoneAvailable = true;
        private bool _disposed;

        public VoiceStatus Status { get; private set; } = VoiceStatus.Idle;
        public SpeechLanguage Language { get; set; } = SpeechLanguage.Auto;
        // Used when automatic detection hears neither Russian nor English (normally the game UI language).
        public SpeechLanguage FallbackLanguage { get; set; } = SpeechLanguage.Russian;
        public bool Enabled => _enabled;
        public bool IsListening => _listening && Status == VoiceStatus.Listening;
        public RecognizedSpeech LastRecognized { get; private set; }
        public double LastRecognitionSeconds { get; private set; }
        public int DroppedPhrases => _worker?.DroppedRequests ?? 0;
        public int FailedPhrases { get; private set; }
        public event Action<RecognizedSpeech> Recognized;
        public event Action StatusChanged;

        public VoiceRecognition(VoiceActivitySettings vadSettings, Func<ISpeechRecognizer> createRecognizer, Func<double> clock = null)
        {
            _vadSettings = vadSettings ?? throw new ArgumentNullException(nameof(vadSettings));
            string error = vadSettings.Validate();
            if (error != null) throw new ArgumentException(error, nameof(vadSettings));
            _createRecognizer = createRecognizer ?? throw new ArgumentNullException(nameof(createRecognizer));
            _clock = clock ?? (() => SpeechClock.Now);
        }

        // Player setting. Turning recognition off stops listening and releases the model.
        public void SetEnabled(bool enabled)
        {
            if (_disposed || _enabled == enabled) return;
            _enabled = enabled;
            if (!enabled)
            {
                _listening = false;
                _vad?.Reset();
                // Never block the game: the worker finishes any phrase in progress and frees the model itself.
                StopWorker(0);
            }
            RefreshStatus();
        }

        // A capture session started at this sample rate (the bridge opened the microphone).
        public void BeginListening(int sampleRate)
        {
            if (_disposed || !_enabled) return;
            _vad = _vad != null && _vad.SampleRate == sampleRate ? _vad : new VoiceActivityDetector(_vadSettings, sampleRate);
            _vad.Reset();
            _captureStart = _clock() - _vad.SamplesProcessed / (double)sampleRate;
            _listening = true;
            _microphoneAvailable = true;
            if (_worker == null)
            {
                _worker = new SpeechRecognitionWorker(_createRecognizer);
                _worker.Start();
            }
            RefreshStatus();
        }

        // Listening stopped: a half-spoken phrase is discarded, the loaded model stays for the next session.
        public void EndListening()
        {
            if (!_listening) return;
            _listening = false;
            _vad?.Reset();
            RefreshStatus();
        }

        public void MicrophoneUnavailable()
        {
            _listening = false;
            _microphoneAvailable = false;
            _vad?.Reset();
            RefreshStatus();
        }

        // Main thread, from the capture bridge. Samples are copied into bounded VAD buffers.
        public void Submit(float[] samples, int count)
        {
            if (!_listening || _vad == null || _worker == null) return;
            _segments.Clear();
            _vad.Process(samples, 0, count, _segments);
            foreach (SpeechSegment segment in _segments)
            {
                double ended = _captureStart + segment.EndSample / (double)segment.SampleRate;
                _worker.Submit(new SpeechRequest(++_nextSequence, segment.Samples, segment.SampleRate, ended,
                    SpeechLanguageCodes.Code(Language), SpeechLanguageCodes.Code(FallbackLanguage)));
            }
        }

        // Main thread: delivers finished recognitions once each and refreshes status.
        public void Update()
        {
            if (_worker == null) return;
            while (_worker.TryTake(out SpeechResponse response))
            {
                if (response.Sequence <= _lastDelivered) continue;
                _lastDelivered = response.Sequence;
                LastRecognitionSeconds = response.RecognitionSeconds;
                if (response.Error != null)
                {
                    FailedPhrases++;
                    continue;
                }
                if (string.IsNullOrWhiteSpace(response.Result.Text)) continue;
                LastRecognized = new RecognizedSpeech(response.Sequence, response.Result.Text, response.Timestamp,
                    response.Result.Confidence, response.Result.Language);
                Recognized?.Invoke(LastRecognized);
                if (_disposed) return;
            }
            RefreshStatus();
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _listening = false;
            // Shutdown (scene unload, leaving Play Mode, quitting): wait briefly so no thread outlives it.
            StopWorker(3000);
            Recognized = null;
            StatusChanged = null;
        }

        private void StopWorker(int timeoutMilliseconds)
        {
            _worker?.Stop(timeoutMilliseconds);
            _worker = null;
        }

        private void RefreshStatus()
        {
            VoiceStatus status;
            if (!_enabled) status = VoiceStatus.Disabled;
            else if (!_microphoneAvailable) status = VoiceStatus.MicrophoneUnavailable;
            else if (_worker != null && _worker.State == RecognitionWorkerState.Failed)
                status = _worker.Error == VoiceFailure.ModelMissing ? VoiceStatus.ModelMissing : VoiceStatus.RecognizerUnavailable;
            else if (!_listening) status = VoiceStatus.Idle;
            else status = _worker != null && _worker.State == RecognitionWorkerState.Ready ? VoiceStatus.Listening : VoiceStatus.Loading;
            if (status == Status) return;
            Status = status;
            StatusChanged?.Invoke();
        }
    }
}
