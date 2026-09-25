using System;
using System.Collections.Generic;
using System.Threading;

namespace GoLive.Voice
{
    // Result of recognizing one phrase. Empty text means "no speech".
    public readonly struct SpeechRecognitionResult
    {
        public string Text { get; }
        public float? Confidence { get; }
        public string Language { get; }

        public SpeechRecognitionResult(string text, float? confidence, string language)
        {
            Text = text ?? "";
            Confidence = confidence;
            Language = language ?? "";
        }
    }

    // A speech-to-text backend. Every member is called only on the recognition worker thread, one call at a
    // time, so implementations need no locking. Test adapters implement it without any native backend.
    public interface ISpeechRecognizer : IDisposable
    {
        // Loads the model. Returns null on success or a stable failure key (VoiceFailure) without throwing
        // for expected problems (model missing, backend unavailable).
        string Initialize();

        // 16 kHz mono samples. language: "ru", "en" or "auto"; fallback is used when auto-detection picks
        // neither Russian nor English.
        SpeechRecognitionResult Recognize(float[] samples16k, string language, string fallbackLanguage);
    }

    public static class VoiceFailure
    {
        public const string ModelMissing = "model_missing";
        public const string BackendUnavailable = "backend_unavailable";
        public const string ModelInvalid = "model_invalid";
        public const string RecognitionFailed = "recognition_failed";
    }

    public enum RecognitionWorkerState { NotStarted, Loading, Ready, Failed, Stopped }

    // A phrase queued for recognition; samples are owned by the worker once submitted.
    public readonly struct SpeechRequest
    {
        public long Sequence { get; }
        public float[] Samples { get; }
        public int SampleRate { get; }
        public double Timestamp { get; }
        public string Language { get; }
        public string FallbackLanguage { get; }

        public SpeechRequest(long sequence, float[] samples, int sampleRate, double timestamp, string language, string fallbackLanguage)
        {
            Sequence = sequence;
            Samples = samples;
            SampleRate = sampleRate;
            Timestamp = timestamp;
            Language = language;
            FallbackLanguage = fallbackLanguage;
        }
    }

    public readonly struct SpeechResponse
    {
        public long Sequence { get; }
        public double Timestamp { get; }
        public SpeechRecognitionResult Result { get; }
        public string Error { get; }
        public double RecognitionSeconds { get; }

        public SpeechResponse(long sequence, double timestamp, SpeechRecognitionResult result, string error, double recognitionSeconds)
        {
            Sequence = sequence;
            Timestamp = timestamp;
            Result = result;
            Error = error;
            RecognitionSeconds = recognitionSeconds;
        }
    }

    // Owns the one background thread that loads the model and recognizes phrases, so inference never runs on
    // the Unity main thread. Explicit lifecycle: Start -> Submit* -> Dispose. The recognizer is created,
    // used and disposed only on the worker thread; Dispose waits for the thread to finish its current phrase.
    public sealed class SpeechRecognitionWorker : IDisposable
    {
        public const int MinimumRecognitionSamples = 17600; // 1.1 s at 16 kHz: whisper ignores shorter input
        private readonly Func<ISpeechRecognizer> _createRecognizer;
        private readonly int _capacity;
        private readonly object _gate = new();
        private readonly Queue<SpeechRequest> _requests = new();
        private readonly Queue<SpeechResponse> _responses = new();
        private Thread _thread;
        private bool _stopping;
        private int _state;
        private string _error;

        public RecognitionWorkerState State => (RecognitionWorkerState)Volatile.Read(ref _state);
        public string Error { get { lock (_gate) return _error; } }
        public int DroppedRequests { get; private set; }
        public int QueueCapacity => _capacity;

        public SpeechRecognitionWorker(Func<ISpeechRecognizer> createRecognizer, int capacity = 2)
        {
            _createRecognizer = createRecognizer ?? throw new ArgumentNullException(nameof(createRecognizer));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            _capacity = capacity;
        }

        public void Start()
        {
            if (State != RecognitionWorkerState.NotStarted) throw new InvalidOperationException("The recognition worker starts once.");
            Volatile.Write(ref _state, (int)RecognitionWorkerState.Loading);
            _thread = new Thread(Run) { IsBackground = true, Name = "GO LIVE speech recognition", Priority = ThreadPriority.BelowNormal };
            _thread.Start();
        }

        // Main thread. Bounded: when the backend falls behind, the oldest waiting phrase is dropped.
        public bool Submit(SpeechRequest request)
        {
            if (request.Samples == null || request.SampleRate <= 0) throw new ArgumentException("A phrase needs samples.", nameof(request));
            lock (_gate)
            {
                RecognitionWorkerState state = State;
                if (_stopping || state == RecognitionWorkerState.Failed || state == RecognitionWorkerState.Stopped || state == RecognitionWorkerState.NotStarted)
                    return false;
                if (_requests.Count == _capacity)
                {
                    _requests.Dequeue();
                    DroppedRequests++;
                }
                _requests.Enqueue(request);
                Monitor.Pulse(_gate);
                return true;
            }
        }

        // Main thread: results in submission order.
        public bool TryTake(out SpeechResponse response)
        {
            lock (_gate)
            {
                if (_responses.Count == 0)
                {
                    response = default;
                    return false;
                }
                response = _responses.Dequeue();
                return true;
            }
        }

        public void Dispose() => Stop(3000);

        // Returns false if the worker was still finishing a phrase after the timeout; it then exits on its own
        // (background thread) and still disposes the recognizer itself, never concurrently with inference.
        public bool Stop(int timeoutMilliseconds)
        {
            Thread thread;
            lock (_gate)
            {
                if (_thread == null)
                {
                    Volatile.Write(ref _state, (int)RecognitionWorkerState.Stopped);
                    return true;
                }
                _stopping = true;
                _requests.Clear();
                Monitor.PulseAll(_gate);
                thread = _thread;
            }
            bool joined = thread.Join(timeoutMilliseconds);
            if (joined) lock (_gate) _thread = null;
            return joined;
        }

        private void Run()
        {
            ISpeechRecognizer recognizer = null;
            try
            {
                string failure;
                try
                {
                    recognizer = _createRecognizer();
                    failure = recognizer.Initialize();
                }
                catch (DllNotFoundException) { failure = VoiceFailure.BackendUnavailable; }
                catch (EntryPointNotFoundException) { failure = VoiceFailure.BackendUnavailable; }
                catch (BadImageFormatException) { failure = VoiceFailure.BackendUnavailable; }
                catch (Exception) { failure = VoiceFailure.ModelInvalid; }
                if (failure != null)
                {
                    Fail(failure);
                    return;
                }
                lock (_gate)
                {
                    if (_stopping) return;
                    Volatile.Write(ref _state, (int)RecognitionWorkerState.Ready);
                }
                while (true)
                {
                    SpeechRequest request;
                    lock (_gate)
                    {
                        while (!_stopping && _requests.Count == 0) Monitor.Wait(_gate);
                        if (_stopping) return;
                        request = _requests.Dequeue();
                    }
                    long started = System.Diagnostics.Stopwatch.GetTimestamp();
                    SpeechRecognitionResult result = default;
                    string error = null;
                    try
                    {
                        float[] prepared = AudioPreparation.ToRecognizerInput(request.Samples, request.SampleRate);
                        result = recognizer.Recognize(prepared, request.Language, request.FallbackLanguage);
                    }
                    catch (Exception)
                    {
                        // A failed phrase is reported once; the backend stays available for the next phrase.
                        error = VoiceFailure.RecognitionFailed;
                    }
                    double seconds = (System.Diagnostics.Stopwatch.GetTimestamp() - started) / (double)System.Diagnostics.Stopwatch.Frequency;
                    lock (_gate)
                    {
                        if (_stopping) return;
                        _responses.Enqueue(new SpeechResponse(request.Sequence, request.Timestamp, result, error, seconds));
                        while (_responses.Count > 16) _responses.Dequeue();
                    }
                }
            }
            finally
            {
                try { recognizer?.Dispose(); }
                catch (Exception) { /* native teardown failure must not escape the worker */ }
                lock (_gate)
                {
                    if (State != RecognitionWorkerState.Failed) Volatile.Write(ref _state, (int)RecognitionWorkerState.Stopped);
                    _requests.Clear();
                }
            }
        }

        private void Fail(string failure)
        {
            lock (_gate)
            {
                _error = failure;
                _requests.Clear();
                Volatile.Write(ref _state, (int)RecognitionWorkerState.Failed);
            }
        }
    }

    public static class AudioPreparation
    {
        public const int RecognizerSampleRate = 16000;

        // Linear resampling to 16 kHz and padding with trailing silence to the backend's minimum length.
        public static float[] ToRecognizerInput(float[] samples, int sampleRate)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (sampleRate <= 0) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            float[] resampled = samples;
            if (sampleRate != RecognizerSampleRate)
            {
                int length = (int)Math.Max(1, Math.Round(samples.Length * (double)RecognizerSampleRate / sampleRate));
                resampled = new float[length];
                double step = (double)sampleRate / RecognizerSampleRate;
                for (int i = 0; i < length; i++)
                {
                    double position = i * step;
                    int index = (int)position;
                    if (index >= samples.Length - 1)
                    {
                        resampled[i] = samples[samples.Length - 1];
                        continue;
                    }
                    double fraction = position - index;
                    resampled[i] = (float)(samples[index] + (samples[index + 1] - samples[index]) * fraction);
                }
            }
            if (resampled.Length >= SpeechRecognitionWorker.MinimumRecognitionSamples) return resampled;
            var padded = new float[SpeechRecognitionWorker.MinimumRecognitionSamples];
            Array.Copy(resampled, padded, resampled.Length);
            return padded;
        }
    }
}
