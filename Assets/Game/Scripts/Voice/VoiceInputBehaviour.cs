using System.IO;
using GoLive.Localization;
using UnityEngine;
using Whisper.Utils;

namespace GoLive.Voice
{
    // Unity bridge for the player's REAL operating-system microphone (the default device). It owns only
    // Unity lifecycle, Microphone capture and the player's voice settings; VoiceRecognition owns VAD and
    // recognition. It is unrelated to the in-game PC microphone item. The microphone is opened only while
    // something requests listening (a broadcast) and recognition is enabled. Raw audio stays in memory,
    // is never saved and never leaves the machine.
    [DisallowMultipleComponent]
    public sealed class VoiceInputBehaviour : MonoBehaviour
    {
        public const string EnabledKey = "GoLive.Voice.Enabled";
        public const string LanguageKey = "GoLive.Voice.Language";
        private const int ClipSeconds = 4;
        private const int BlocksPerSecond = 50;
        private const float RetrySeconds = 3f;

        [SerializeField] private LocalizationContext localization;
        // StreamingAssets-relative multilingual ggml models in preference order (the first present is used).
        [SerializeField] private string[] modelFiles = { "Whisper/ggml-base.bin", "Whisper/ggml-tiny.bin" };
        [SerializeField] private bool useGpu;
        [SerializeField, Min(1)] private int maximumThreads = 4;
        [SerializeField] private int preferredSampleRate = 16000;
        [SerializeField] private VoiceActivitySettings activity = new();

        private AudioClip _clip;
        private float[] _block;
        private float[] _tail;
        private int _clipSamples;
        private int _readPosition;
        private bool _requested;
        private float _retryAt;

        public VoiceRecognition Recognition { get; private set; }
        public bool IsCapturing => _clip != null;

        private void Awake()
        {
            string activityError = activity?.Validate() ?? "Voice activity settings are missing.";
            if (activityError != null || modelFiles == null || modelFiles.Length == 0)
            {
                Debug.LogError(activityError ?? "Voice input needs at least one speech model file.", this);
                enabled = false;
                return;
            }
            LogUtils.Level = LogLevel.Warning;
            // Resolved on the main thread; the recognizer itself is created on the worker thread.
            string[] paths = new string[modelFiles.Length];
            for (int i = 0; i < paths.Length; i++) paths[i] = Path.Combine(Application.streamingAssetsPath, modelFiles[i]);
            int threads = Mathf.Clamp(SystemInfo.processorCount / 2, 1, maximumThreads);
            bool gpu = useGpu;
            Recognition = new VoiceRecognition(activity, () => new WhisperSpeechRecognizer(paths, threads, gpu));
            Recognition.SetEnabled(PlayerPrefs.GetInt(EnabledKey, 1) == 1);
            int language = PlayerPrefs.GetInt(LanguageKey, (int)SpeechLanguage.Auto);
            Recognition.Language = language >= (int)SpeechLanguage.Auto && language <= (int)SpeechLanguage.English
                ? (SpeechLanguage)language : SpeechLanguage.Auto;
        }

        private void OnDisable() => StopCapture();

        private void OnDestroy() => Recognition?.Dispose();

        // The broadcast asks for (or releases) the player's voice.
        public void SetListening(bool requested)
        {
            if (Recognition == null || _requested == requested) return;
            _requested = requested;
            _retryAt = 0;
            if (!requested) StopCapture();
        }

        public void SetEnabled(bool value)
        {
            if (Recognition == null) return;
            PlayerPrefs.SetInt(EnabledKey, value ? 1 : 0);
            if (!value) StopCapture();
            Recognition.SetEnabled(value);
        }

        public void SetLanguage(SpeechLanguage language)
        {
            if (Recognition == null) return;
            PlayerPrefs.SetInt(LanguageKey, (int)language);
            Recognition.Language = language;
        }

        private void Update()
        {
            if (localization != null)
                Recognition.FallbackLanguage = localization.CurrentLanguage == GameLanguage.English ? SpeechLanguage.English : SpeechLanguage.Russian;
            // Without a usable recognizer the microphone is not kept open for nothing.
            bool recognizerUsable = Recognition.Status != VoiceStatus.ModelMissing && Recognition.Status != VoiceStatus.RecognizerUnavailable;
            if (!recognizerUsable && _clip != null) StopCapture();
            if (_requested && recognizerUsable && Recognition.Enabled && _clip == null && Time.unscaledTime >= _retryAt) StartCapture();
            if (_clip != null) ReadMicrophone();
            Recognition.Update();
        }

        private void StartCapture()
        {
            if (Microphone.devices.Length == 0)
            {
                Unavailable();
                return;
            }
            Microphone.GetDeviceCaps(null, out int minimum, out int maximum);
            int rate = minimum == 0 && maximum == 0 ? preferredSampleRate : Mathf.Clamp(preferredSampleRate, minimum, maximum);
            _clip = Microphone.Start(null, true, ClipSeconds, rate);
            if (_clip == null)
            {
                Unavailable();
                return;
            }
            _clipSamples = _clip.samples;
            int block = Mathf.Max(1, _clip.frequency / BlocksPerSecond);
            if (_block == null || _block.Length != block) _block = new float[block];
            _readPosition = 0;
            Recognition.BeginListening(_clip.frequency);
        }

        // Reads every complete block the device wrote since the last frame; no per-frame allocation.
        private void ReadMicrophone()
        {
            if (!Microphone.IsRecording(null))
            {
                // Unplugged or revoked device: report once, retry later, never throw every frame.
                StopCapture();
                Unavailable();
                return;
            }
            int position = Microphone.GetPosition(null);
            int available = (position - _readPosition + _clipSamples) % _clipSamples;
            while (available >= _block.Length)
            {
                int untilEnd = _clipSamples - _readPosition;
                if (untilEnd >= _block.Length)
                {
                    _clip.GetData(_block, _readPosition);
                    Recognition.Submit(_block, _block.Length);
                    _readPosition = (_readPosition + _block.Length) % _clipSamples;
                    available -= _block.Length;
                    continue;
                }
                // Only when the clip length is not a whole number of blocks.
                if (_tail == null || _tail.Length != untilEnd) _tail = new float[untilEnd];
                _clip.GetData(_tail, _readPosition);
                Recognition.Submit(_tail, _tail.Length);
                _readPosition = 0;
                available -= untilEnd;
            }
        }

        private void StopCapture()
        {
            if (_clip != null)
            {
                Microphone.End(null);
                Destroy(_clip);
                _clip = null;
            }
            Recognition?.EndListening();
        }

        private void Unavailable()
        {
            Recognition.MicrophoneUnavailable();
            _retryAt = Time.unscaledTime + RetrySeconds;
        }
    }
}
