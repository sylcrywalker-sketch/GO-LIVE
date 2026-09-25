using System;
using System.Collections.Generic;

namespace GoLive.Voice
{
    // Configuration of the energy-based phrase segmenter. Times are milliseconds; levels are RMS of samples in [-1, 1].
    [Serializable]
    public sealed class VoiceActivitySettings
    {
        public int FrameMilliseconds = 20;
        // A frame is speech when louder than this floor and SpeechToNoiseRatio times the adaptive noise level.
        public float MinimumSpeechRms = .012f;
        public float SpeechToNoiseRatio = 3f;
        // Voiced time before a phrase opens: clicks, taps and short noise bursts never open one.
        public int MinimumSpeechMilliseconds = 160;
        // Voiced time a closed phrase needs to be recognized.
        public int MinimumPhraseMilliseconds = 400;
        // Trailing silence that ends a phrase.
        public int SilenceTimeoutMilliseconds = 650;
        // Hard cap: longer continuous speech is cut and recognized in parts.
        public int MaximumPhraseMilliseconds = 12000;
        // Audio kept from before onset so the first syllable is not clipped.
        public int PreRollMilliseconds = 250;
        // How quickly the noise estimate follows quieter (fast) and louder (slow) background. A sustained sound
        // above the threshold (fan, hum) raises it very slowly, so it stops opening phrases after a few
        // seconds; the quiet gaps between words pull it straight back down.
        public float NoiseFallRate = .2f;
        public float NoiseRiseRate = .01f;
        public float SustainedSoundRiseRate = .002f;

        public string Validate()
        {
            if (FrameMilliseconds < 5 || FrameMilliseconds > 100) return "VAD frame must be 5-100 ms.";
            if (!(MinimumSpeechRms > 0) || !(SpeechToNoiseRatio >= 1)) return "VAD thresholds must be positive; ratio at least 1.";
            if (MinimumSpeechMilliseconds < FrameMilliseconds || MinimumPhraseMilliseconds < MinimumSpeechMilliseconds)
                return "VAD minimum phrase must cover the minimum speech onset.";
            if (SilenceTimeoutMilliseconds < FrameMilliseconds || MaximumPhraseMilliseconds <= MinimumPhraseMilliseconds + SilenceTimeoutMilliseconds)
                return "VAD silence timeout and maximum phrase are inconsistent.";
            if (PreRollMilliseconds < 0 || PreRollMilliseconds > 1000) return "VAD pre-roll must be 0-1000 ms.";
            if (!(NoiseFallRate > 0 && NoiseFallRate <= 1) || !(NoiseRiseRate > 0 && NoiseRiseRate <= 1) ||
                !(SustainedSoundRiseRate > 0 && SustainedSoundRiseRate <= NoiseRiseRate)) return "VAD noise rates must be in (0, 1].";
            return null;
        }
    }

    // One closed phrase: mono samples at the capture rate, ownership passes to the consumer.
    public sealed class SpeechSegment
    {
        public float[] Samples { get; }
        public int SampleRate { get; }
        public double VoicedSeconds { get; }
        // Capture sample index (since the detector started) where the phrase ended.
        public long EndSample { get; }
        public bool Truncated { get; }

        public SpeechSegment(float[] samples, int sampleRate, double voicedSeconds, long endSample, bool truncated)
        {
            Samples = samples;
            SampleRate = sampleRate;
            VoicedSeconds = voicedSeconds;
            EndSample = endSample;
            Truncated = truncated;
        }
    }

    // Energy-based voice activity detection and phrase segmentation for one capture stream:
    // silence -> speech begins -> collect -> speech ends -> phrase. All buffers are allocated once and
    // bounded by MaximumPhraseMilliseconds + PreRollMilliseconds; only a closed phrase is copied out.
    // Limits: it measures loudness, not voice. Loud steady noise raises the adaptive floor; loud
    // non-speech sounds (music, typing close to the mic) can open phrases that the recognizer must reject.
    public sealed class VoiceActivityDetector
    {
        private enum Phase { Silence, Onset, Speech }
        private readonly VoiceActivitySettings _settings;
        private readonly int _frameSamples;
        private readonly float[] _frame;
        private readonly float[] _preRoll;
        private readonly float[] _phrase;
        private int _frameFill;
        private int _preRollStart;
        private int _preRollCount;
        private int _phraseLength;
        private int _voicedFrames;
        private int _trailingSilentFrames;
        private Phase _phase;
        private float _noise;

        public int SampleRate { get; }
        public long SamplesProcessed { get; private set; }
        public bool InSpeech => _phase == Phase.Speech;
        public float NoiseLevel => _noise;
        public int BufferCapacitySamples => _phrase.Length + _preRoll.Length + _frame.Length;

        public VoiceActivityDetector(VoiceActivitySettings settings, int sampleRate)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            string error = settings.Validate();
            if (error != null) throw new ArgumentException(error, nameof(settings));
            if (sampleRate < 8000 || sampleRate > 192000) throw new ArgumentOutOfRangeException(nameof(sampleRate));
            SampleRate = sampleRate;
            _frameSamples = Math.Max(1, sampleRate * settings.FrameMilliseconds / 1000);
            _frame = new float[_frameSamples];
            _preRoll = new float[Math.Max(1, sampleRate * settings.PreRollMilliseconds / 1000)];
            _phrase = new float[_preRoll.Length + sampleRate * settings.MaximumPhraseMilliseconds / 1000];
            _noise = settings.MinimumSpeechRms / settings.SpeechToNoiseRatio;
        }

        // Consumes captured samples; closed phrases are appended to completed.
        public void Process(float[] samples, int offset, int count, List<SpeechSegment> completed)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            if (offset < 0 || count < 0 || offset + count > samples.Length) throw new ArgumentOutOfRangeException(nameof(count));
            for (int i = 0; i < count; i++)
            {
                _frame[_frameFill++] = samples[offset + i];
                if (_frameFill < _frameSamples) continue;
                _frameFill = 0;
                SamplesProcessed += _frameSamples;
                ProcessFrame(completed);
            }
        }

        // Discards any partial phrase (listening stopped): nothing half-spoken is recognized later.
        public void Reset()
        {
            _frameFill = 0;
            _preRollStart = 0;
            _preRollCount = 0;
            _phraseLength = 0;
            _voicedFrames = 0;
            _trailingSilentFrames = 0;
            _phase = Phase.Silence;
        }

        private void ProcessFrame(List<SpeechSegment> completed)
        {
            double sum = 0;
            for (int i = 0; i < _frameSamples; i++) sum += _frame[i] * (double)_frame[i];
            float rms = (float)Math.Sqrt(sum / _frameSamples);
            bool voiced = rms >= _settings.MinimumSpeechRms && rms >= _noise * _settings.SpeechToNoiseRatio;
            float rate = rms < _noise ? _settings.NoiseFallRate : voiced ? _settings.SustainedSoundRiseRate : _settings.NoiseRiseRate;
            _noise += (rms - _noise) * rate;

            switch (_phase)
            {
                case Phase.Silence:
                    if (!voiced)
                    {
                        RememberPreRoll();
                        return;
                    }
                    StartPhrase();
                    _phase = Phase.Onset;
                    Append();
                    _voicedFrames = 1;
                    _trailingSilentFrames = 0;
                    if (Ms(_voicedFrames) >= _settings.MinimumSpeechMilliseconds) _phase = Phase.Speech;
                    return;
                case Phase.Onset:
                    Append();
                    if (voiced)
                    {
                        _voicedFrames++;
                        _trailingSilentFrames = 0;
                        if (Ms(_voicedFrames) >= _settings.MinimumSpeechMilliseconds) _phase = Phase.Speech;
                    }
                    else if (Ms(++_trailingSilentFrames) >= _settings.MinimumSpeechMilliseconds)
                    {
                        // A burst shorter than the onset: noise, not a phrase.
                        Reset();
                    }
                    return;
                default:
                    Append();
                    if (voiced)
                    {
                        _voicedFrames++;
                        _trailingSilentFrames = 0;
                    }
                    else _trailingSilentFrames++;
                    bool full = _phraseLength + _frameSamples > _phrase.Length;
                    if (Ms(_trailingSilentFrames) >= _settings.SilenceTimeoutMilliseconds || full) Close(completed, full);
                    return;
            }
        }

        private void Close(List<SpeechSegment> completed, bool truncated)
        {
            // Keep a short tail of the silence; drop the rest of the timeout.
            int keepSilence = Math.Min(_trailingSilentFrames, Math.Max(1, 120 / _settings.FrameMilliseconds));
            int length = _phraseLength - (_trailingSilentFrames - keepSilence) * _frameSamples;
            if (Ms(_voicedFrames) >= _settings.MinimumPhraseMilliseconds && length > 0)
            {
                var samples = new float[length];
                Array.Copy(_phrase, samples, length);
                completed.Add(new SpeechSegment(samples, SampleRate, Ms(_voicedFrames) / 1000.0,
                    SamplesProcessed - (_trailingSilentFrames - keepSilence) * (long)_frameSamples, truncated));
            }
            Reset();
        }

        private void StartPhrase()
        {
            _phraseLength = 0;
            for (int i = 0; i < _preRollCount; i++) _phrase[_phraseLength++] = _preRoll[(_preRollStart + i) % _preRoll.Length];
            _preRollCount = 0;
            _preRollStart = 0;
        }

        private void Append()
        {
            int count = Math.Min(_frameSamples, _phrase.Length - _phraseLength);
            Array.Copy(_frame, 0, _phrase, _phraseLength, count);
            _phraseLength += count;
        }

        private void RememberPreRoll()
        {
            for (int i = 0; i < _frameSamples; i++)
            {
                int index = (_preRollStart + _preRollCount) % _preRoll.Length;
                _preRoll[index] = _frame[i];
                if (_preRollCount < _preRoll.Length) _preRollCount++;
                else _preRollStart = (_preRollStart + 1) % _preRoll.Length;
            }
        }

        private int Ms(int frames) => frames * _settings.FrameMilliseconds;
    }
}
