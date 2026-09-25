using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Whisper;
using Whisper.Native;
using Whisper.Utils;

namespace GoLive.Voice
{
    // Local, offline speech-to-text through whisper.cpp (com.whisper.unity 1.4.0, whisper.cpp v1.7.5) with a
    // multilingual ggml model from StreamingAssets. Runs only on the recognition worker thread. The native
    // context is created in Initialize and freed in Dispose on that same thread (the package wrapper only
    // frees in a finalizer, so it is not used). Audio never leaves the machine.
    public sealed class WhisperSpeechRecognizer : ISpeechRecognizer
    {
        // Not bound by the package; exported by its whisper.cpp v1.7.5 builds.
        [DllImport("libwhisper")]
        private static extern float whisper_full_get_segment_no_speech_prob(IntPtr context, int segment);

        private const float NoSpeechThreshold = .6f;
        private const float LowConfidence = .45f;
        private readonly string[] _modelPaths;
        private readonly int _threads;
        private readonly bool _useGpu;
        private IntPtr _context;
        private WhisperParams _parameters;

        public string ModelPath { get; private set; }

        // modelPaths: candidates in preference order; the first existing file is loaded.
        public WhisperSpeechRecognizer(string[] modelPaths, int threads, bool useGpu)
        {
            _modelPaths = modelPaths ?? throw new ArgumentNullException(nameof(modelPaths));
            _threads = Math.Max(1, threads);
            _useGpu = useGpu;
        }

        public string Initialize()
        {
            ModelPath = Array.Find(_modelPaths, File.Exists);
            if (ModelPath == null) return VoiceFailure.ModelMissing;
            byte[] model = File.ReadAllBytes(ModelPath);
            WhisperContextParams contextParameters = WhisperContextParams.GetDefaultParams();
            contextParameters.UseGpu = _useGpu;
            unsafe
            {
                // whisper.cpp copies the buffer; the managed array can be collected afterwards.
                fixed (byte* data = model)
                    _context = WhisperNative.whisper_init_from_buffer_with_params((IntPtr)data, new UIntPtr((ulong)model.LongLength),
                        contextParameters.NativeParams);
            }
            if (_context == IntPtr.Zero) return VoiceFailure.ModelInvalid;
            if (WhisperNative.whisper_is_multilingual(_context) == 0) return VoiceFailure.ModelInvalid;
            _parameters = WhisperParams.GetDefaultParams(WhisperSamplingStrategy.WHISPER_SAMPLING_GREEDY);
            _parameters.ThreadsCount = _threads;
            _parameters.Translate = false;
            _parameters.NoContext = true;
            _parameters.SingleSegment = true;
            _parameters.PrintProgress = false;
            _parameters.PrintRealtime = false;
            _parameters.PrintTimestamps = false;
            _parameters.PrintSpecial = false;
            _parameters.EnableTokens = false;
            return null;
        }

        public SpeechRecognitionResult Recognize(float[] samples16k, string language, string fallbackLanguage)
        {
            if (_context == IntPtr.Zero) throw new InvalidOperationException("The speech model is not loaded.");
            SpeechRecognitionResult result = Run(samples16k, language);
            // Auto-detection may choose a third language on short phrases; the game supports Russian and English.
            if (language == "auto" && result.Language != "ru" && result.Language != "en" && (fallbackLanguage == "ru" || fallbackLanguage == "en"))
                result = Run(samples16k, fallbackLanguage);
            return result;
        }

        private unsafe SpeechRecognitionResult Run(float[] samples, string language)
        {
            _parameters.Language = language;
            int code;
            fixed (float* data = samples) code = WhisperNative.whisper_full(_context, _parameters.NativeParams, data, samples.Length);
            if (code != 0) throw new InvalidOperationException("whisper_full failed: " + code);

            var text = new StringBuilder();
            float noSpeech = 0;
            double probability = 0;
            int tokens = 0;
            int eot = WhisperNative.whisper_token_eot(_context);
            int segments = WhisperNative.whisper_full_n_segments(_context);
            for (int segment = 0; segment < segments; segment++)
            {
                text.Append(TextUtils.StringFromNativeUtf8(WhisperNative.whisper_full_get_segment_text(_context, segment)));
                noSpeech = Math.Max(noSpeech, whisper_full_get_segment_no_speech_prob(_context, segment));
                int count = WhisperNative.whisper_full_n_tokens(_context, segment);
                for (int token = 0; token < count; token++)
                {
                    WhisperNativeTokenData data = WhisperNative.whisper_full_get_token_data(_context, segment, token);
                    if (data.id >= eot) continue;
                    probability += data.p;
                    tokens++;
                }
            }
            float? confidence = tokens > 0 ? (float)(probability / tokens) : (float?)null;
            string detected = Marshal.PtrToStringAnsi(WhisperNative.whisper_lang_str(WhisperNative.whisper_full_lang_id(_context))) ?? "";
            // Whisper invents text for noise ("Субтитры...", "Thank you."); a likely non-speech, low-confidence
            // result is treated as silence.
            if (noSpeech > NoSpeechThreshold && (confidence ?? 0) < LowConfidence) return new SpeechRecognitionResult("", confidence, detected);
            return new SpeechRecognitionResult(WhisperArtifacts.Clean(text.ToString()), confidence, detected);
        }

        public void Dispose()
        {
            if (_context == IntPtr.Zero) return;
            WhisperNative.whisper_free(_context);
            _context = IntPtr.Zero;
            _parameters = null;
        }
    }
}
