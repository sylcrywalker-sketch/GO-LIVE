using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace GoLive.Editor.Voice
{
    // Development-only recorder of REAL microphone speech for the speech-recognition comparison. It opens the default
    // device exactly like VoiceInputBehaviour (16 kHz preferred, clamped to the device caps), records one continuous
    // take while the speaker reads a scripted list, and marks each phrase boundary with Space. Output stays on this
    // machine under Logs/VoiceCorpus: raw.wav (the whole take at the capture rate), marks.json (reference text and
    // spoken intervals) and info.json (device and levels). Nothing is uploaded.
    public sealed class SpeechCorpusWindow : EditorWindow
    {
        private const int PreferredRate = 16000;
        private const int ClipSeconds = 10;

        // Natural stream speech: the failed live session, the short acceptance script, filler, names, English and mixed.
        internal static readonly string[] Script =
        {
            "Всем привет, парни! Как дела? Как настроение? Что сегодня делали?",
            "Привет, привет, дорогой друг. Как вы вообще? Расскажите, что думаете.",
            "Вы можете мне хоть что-то ответить?",
            "Так, секунду.",
            "А ты во что сегодня играл?",
            "Чат, во что сегодня поиграем?",
            "Ребят, как вам звук сегодня, нормально слышно?",
            "Жесть. Устал, наверное?",
            "Сегодня просто болтаем, никуда не торопимся.",
            "Блин, опять всё зависло, капец.",
            "Если я сейчас проиграю, я удаляю игру.",
            "Спасибо за фоллоу, очень приятно!",
            "Завтра куплю новую видеокарту, обещаю.",
            "Короче, ну, это самое...",
            "Мика, ты тут? Как ты?",
            "Найт Оул, опять ночная смена?",
            "А вы откуда вообще, ребят?",
            "Что делаете сегодня вечером?",
            "Пойду чайку налью, сейчас вернусь.",
            "Ну что, как вам мой новый микрофон?",
            "Слушайте, а какие игры вы любите?",
            "Стрим только начался, сейчас разогреемся.",
            "Эээ... ладно.",
            "Hello chat, how are you doing today?",
            "Хай, hello, how are you?"
        };

        [Serializable] private sealed class Mark { public int index; public string text; public double start, end; }
        [Serializable] private sealed class Marks { public List<Mark> phrases = new(); }
        [Serializable]
        private sealed class Info
        {
            public string device, created, unityVersion;
            public int sampleRate, deviceMinimumRate, deviceMaximumRate;
            public double seconds, peak, rms, noiseFloorRms, clippedFraction;
            public string[] devices;
        }

        private AudioClip _clip;
        private int _readPosition;
        private readonly List<float> _samples = new();
        private float[] _buffer;
        private int _rate, _minimum, _maximum;
        private int _phrase;
        private double _phraseStart;
        private Marks _marks;
        private string _lastSaved;
        private float _level;
        private Vector2 _scroll;

        [MenuItem("GO! LIVE/Voice/Speech Corpus Recorder")]
        public static void Open() => GetWindow<SpeechCorpusWindow>("Speech Corpus");

        private bool Recording => _clip != null;

        private void OnDisable() => Stop(false);

        private void Update()
        {
            if (!Recording) return;
            Read();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Records YOUR real voice from the default microphone (the same device the game uses) for the speech-recognition " +
                "comparison. Read each phrase naturally, like on stream (not like dictation), then press Space (or Next) when you " +
                "finish it. Pause briefly between phrases. The files stay on this PC in Logs/VoiceCorpus.", MessageType.Info);
            EditorGUILayout.LabelField("Devices", string.Join(", ", Microphone.devices));
            if (!Recording)
            {
                if (GUILayout.Button("Start recording", GUILayout.Height(32))) Begin();
                if (_lastSaved != null) EditorGUILayout.HelpBox("Saved: " + _lastSaved, MessageType.None);
                return;
            }

            Event current = Event.current;
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Space)
            {
                Next();
                current.Use();
            }
            double now = _samples.Count / (double)_rate;
            EditorGUILayout.LabelField("Recording", $"{now:0.0} s at {_rate} Hz   level {_level:0.000}");
            Rect bar = GUILayoutUtility.GetRect(100, 14);
            EditorGUI.ProgressBar(bar, Mathf.Clamp01(_level * 8), "");
            GUILayout.Space(8);
            if (_phrase < Script.Length)
            {
                var style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 22, wordWrap = true };
                GUILayout.Label($"{_phrase + 1}/{Script.Length}", EditorStyles.miniLabel);
                GUILayout.Label(Script[_phrase], style);
                if (GUILayout.Button("Next (Space)", GUILayout.Height(28))) Next();
            }
            else GUILayout.Label("All phrases recorded. Press Stop and save.", EditorStyles.boldLabel);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Stop and save", GUILayout.Height(28))) Stop(true);
                if (GUILayout.Button("Discard", GUILayout.Height(28))) Stop(false);
            }
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            for (int i = 0; i < Script.Length; i++)
                GUILayout.Label((i < _phrase ? "✓ " : i == _phrase ? "▶ " : "   ") + Script[i], EditorStyles.miniLabel);
            EditorGUILayout.EndScrollView();
        }

        private void Begin()
        {
            if (Microphone.devices.Length == 0)
            {
                ShowNotification(new GUIContent("No microphone"));
                return;
            }
            Microphone.GetDeviceCaps(null, out _minimum, out _maximum);
            int rate = _minimum == 0 && _maximum == 0 ? PreferredRate : Mathf.Clamp(PreferredRate, _minimum, _maximum);
            _clip = Microphone.Start(null, true, ClipSeconds, rate);
            if (_clip == null) return;
            _rate = _clip.frequency;
            _readPosition = 0;
            _samples.Clear();
            _buffer = new float[_clip.samples];
            _phrase = 0;
            _phraseStart = 0;
            _marks = new Marks();
            _lastSaved = null;
            Focus();
        }

        private void Read()
        {
            int position = Microphone.GetPosition(null);
            int total = _clip.samples;
            int available = (position - _readPosition + total) % total;
            if (available == 0) return;
            int first = Math.Min(available, total - _readPosition);
            var chunk = new float[first];
            _clip.GetData(chunk, _readPosition);
            _samples.AddRange(chunk);
            if (available > first)
            {
                chunk = new float[available - first];
                _clip.GetData(chunk, 0);
                _samples.AddRange(chunk);
            }
            _readPosition = position;
            int window = Math.Min(_samples.Count, _rate / 10);
            double sum = 0;
            for (int i = _samples.Count - window; i < _samples.Count; i++) sum += _samples[i] * (double)_samples[i];
            _level = window > 0 ? (float)Math.Sqrt(sum / window) : 0;
        }

        private void Next()
        {
            if (_phrase >= Script.Length) return;
            Read();
            double now = _samples.Count / (double)_rate;
            _marks.phrases.Add(new Mark { index = _phrase + 1, text = Script[_phrase], start = _phraseStart, end = now });
            _phraseStart = now;
            _phrase++;
        }

        private void Stop(bool save)
        {
            if (!Recording) return;
            Read();
            Microphone.End(null);
            DestroyImmediate(_clip);
            _clip = null;
            if (!save || _samples.Count == 0) return;
            string folder = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "VoiceCorpus", DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            Directory.CreateDirectory(folder);
            float[] samples = _samples.ToArray();
            WavFile.Write(Path.Combine(folder, "raw.wav"), samples, samples.Length, _rate);
            File.WriteAllText(Path.Combine(folder, "marks.json"), JsonUtility.ToJson(_marks, true), new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder, "info.json"), JsonUtility.ToJson(Levels(samples), true), new UTF8Encoding(false));
            _lastSaved = folder;
            Debug.Log($"Speech corpus saved: {folder} ({samples.Length / (double)_rate:0.0} s, {_marks.phrases.Count} phrases)");
        }

        private Info Levels(float[] samples)
        {
            double peak = 0, sum = 0;
            int clipped = 0;
            var frames = new List<double>();
            int frame = Math.Max(1, _rate / 50);
            for (int start = 0; start + frame <= samples.Length; start += frame)
            {
                double frameSum = 0;
                for (int i = start; i < start + frame; i++)
                {
                    double value = samples[i];
                    frameSum += value * value;
                    peak = Math.Max(peak, Math.Abs(value));
                    if (Math.Abs(value) >= .999) clipped++;
                }
                sum += frameSum;
                frames.Add(Math.Sqrt(frameSum / frame));
            }
            frames.Sort();
            return new Info
            {
                device = "default (null)", devices = Microphone.devices, created = DateTime.Now.ToString("s"), unityVersion = Application.unityVersion,
                sampleRate = _rate, deviceMinimumRate = _minimum, deviceMaximumRate = _maximum, seconds = samples.Length / (double)_rate,
                peak = peak, rms = Math.Sqrt(sum / Math.Max(1, samples.Length)), noiseFloorRms = frames.Count > 0 ? frames[frames.Count / 10] : 0,
                clippedFraction = clipped / (double)Math.Max(1, samples.Length)
            };
        }
    }
}
