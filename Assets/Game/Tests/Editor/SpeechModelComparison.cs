using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using GoLive.Editor.Voice;
using GoLive.Viewers;
using GoLive.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace GoLive.Tests
{
    // Development evidence, not a regression test: compares whisper.cpp models and decoding settings on REAL recorded
    // speech (Logs/VoiceCorpus/<take>, recorded with GO! LIVE/Voice/Speech Corpus Recorder). For every configuration it
    // recognizes each marked phrase (oracle boundaries) through the production WhisperSpeechRecognizer and reports text,
    // character/word error, content-word recall, invented words, the speech act the chat would see, latency and memory.
    // The production VAD then segments the same take to separate segmentation errors from model errors.
    //   GO_LIVE_VOICE_CORPUS   take folder (default: newest under Logs/VoiceCorpus)
    //   GO_LIVE_STT_CONFIGS    optional ';'-separated subset of configuration names
    //   GO_LIVE_STT_VAD        configuration name(s) to also run over production VAD segments (default: none)
    //   GO_LIVE_STT_MODEL_ROOT optional benchmark model directory (falls back to StreamingAssets/Whisper)
    //   GO_LIVE_STT_OUTPUT     optional NEW output directory; an existing directory is rejected
    public sealed class SpeechModelComparison
    {
        [Serializable] private sealed class Mark { public int index; public string text; public double start, end; }
        [Serializable] private sealed class Marks { public List<Mark> phrases = new(); }

        private sealed class Config
        {
            public string Name, Model, Language;
            public bool Gpu, Beam;
            public string Prompt = "";
            public int Threads = 4;
            // Experiment only: append zeros after the unchanged production preparation and VAD boundaries.
            public int ExtraTailMilliseconds;
            public int ExtraLeadMilliseconds;
        }

        [Serializable] private sealed class Row
        {
            public int Phrase;
            public string Reference, Text, Language, Acts, ReferenceActs;
            public double Seconds, AudioSeconds, Cer, Wer, Recall;
            public int Invented;
        }

        [Serializable] private sealed class Evidence
        {
            public string kind, config, model, language, error, text, utc;
            public int index, ramGpuBeforeMb, ramGpuAfterMb, extraTailMilliseconds, extraLeadMilliseconds, productionContextSamplesPerSide;
            public long ramBeforeBytes, ramAfterBytes;
            public double start, end, seconds, voicedSeconds, onsetBeforeMs;
            public bool gpuRequested, truncated, beamSearch;
            public string overlaps;
            public Row score;
        }

        private static void Record(StreamWriter writer, Evidence evidence)
        {
            evidence.utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            writer.WriteLine(JsonUtility.ToJson(evidence));
            writer.Flush();
        }

        private const string StreamPrompt = "Привет, чат! Сегодня на стриме болтаем и играем. Как дела, ребят?";

        private static readonly Config[] Configs =
        {
            new() { Name = "tiny-auto", Model = "ggml-tiny.bin", Language = "auto" },
            new() { Name = "tiny-ru", Model = "ggml-tiny.bin", Language = "ru" },
            new() { Name = "tiny-ru-gpu", Model = "ggml-tiny.bin", Language = "ru", Gpu = true },
            new() { Name = "tiny-ru-gpu-beam", Model = "ggml-tiny.bin", Language = "ru", Gpu = true, Beam = true },
            new() { Name = "base-auto", Model = "ggml-base.bin", Language = "auto" },
            new() { Name = "base-ru", Model = "ggml-base.bin", Language = "ru" },
            new() { Name = "base-ru-gpu", Model = "ggml-base.bin", Language = "ru", Gpu = true },
            new() { Name = "base-ru-gpu-beam", Model = "ggml-base.bin", Language = "ru", Gpu = true, Beam = true },
            new() { Name = "base-ru-beam", Model = "ggml-base.bin", Language = "ru", Beam = true },
            new() { Name = "base-ru-prompt", Model = "ggml-base.bin", Language = "ru", Prompt = StreamPrompt },
            new() { Name = "base-q5_1-ru", Model = "ggml-base-q5_1.bin", Language = "ru" },
            new() { Name = "small-auto", Model = "ggml-small.bin", Language = "auto" },
            new() { Name = "small-ru", Model = "ggml-small.bin", Language = "ru" },
            new() { Name = "small-ru-beam", Model = "ggml-small.bin", Language = "ru", Beam = true },
            new() { Name = "small-ru-prompt", Model = "ggml-small.bin", Language = "ru", Prompt = StreamPrompt },
            new() { Name = "small-ru-gpu", Model = "ggml-small.bin", Language = "ru", Gpu = true },
            new() { Name = "small-q5_1-ru", Model = "ggml-small-q5_1.bin", Language = "ru" },
            new() { Name = "small-q5_1-ru-gpu", Model = "ggml-small-q5_1.bin", Language = "ru", Gpu = true },
            new() { Name = "small-q5_1-ru-gpu-beam", Model = "ggml-small-q5_1.bin", Language = "ru", Gpu = true, Beam = true },
            new() { Name = "turbo-q5_0-ru-gpu", Model = "ggml-large-v3-turbo-q5_0.bin", Language = "ru", Gpu = true },
            new() { Name = "turbo-q5_0-ru-gpu-beam", Model = "ggml-large-v3-turbo-q5_0.bin", Language = "ru", Gpu = true, Beam = true },
            new() { Name = "small-q5_1-ru-gpu-tail500", Model = "ggml-small-q5_1.bin", Language = "ru", Gpu = true, ExtraTailMilliseconds = 500 },
            new() { Name = "small-q5_1-ru-gpu-beam-tail500", Model = "ggml-small-q5_1.bin", Language = "ru", Gpu = true, Beam = true, ExtraTailMilliseconds = 500 },
            new() { Name = "turbo-q5_0-ru-gpu-tail500", Model = "ggml-large-v3-turbo-q5_0.bin", Language = "ru", Gpu = true, ExtraTailMilliseconds = 500 },
            new() { Name = "turbo-q5_0-ru-gpu-beam-tail500", Model = "ggml-large-v3-turbo-q5_0.bin", Language = "ru", Gpu = true, Beam = true, ExtraTailMilliseconds = 500 },
            new() { Name = "turbo-q5_0-ru-gpu-beam-lead500", Model = "ggml-large-v3-turbo-q5_0.bin", Language = "ru", Gpu = true, Beam = true, ExtraLeadMilliseconds = 500 },
            new() { Name = "turbo-q5_0-ru-gpu-beam-lead500-tail500", Model = "ggml-large-v3-turbo-q5_0.bin", Language = "ru", Gpu = true, Beam = true, ExtraLeadMilliseconds = 500, ExtraTailMilliseconds = 500 },
            new() { Name = "tiny-ru-gpu-lead500-tail500", Model = "ggml-tiny.bin", Language = "ru", Gpu = true, ExtraLeadMilliseconds = 500, ExtraTailMilliseconds = 500 },
            new() { Name = "base-ru-gpu-lead500-tail500", Model = "ggml-base.bin", Language = "ru", Gpu = true, ExtraLeadMilliseconds = 500, ExtraTailMilliseconds = 500 },
            new() { Name = "base-ru-gpu-beam-lead500-tail500", Model = "ggml-base.bin", Language = "ru", Gpu = true, Beam = true, ExtraLeadMilliseconds = 500, ExtraTailMilliseconds = 500 },
            new() { Name = "small-q5_1-ru-gpu-lead500-tail500", Model = "ggml-small-q5_1.bin", Language = "ru", Gpu = true, ExtraLeadMilliseconds = 500, ExtraTailMilliseconds = 500 },
            new() { Name = "small-q5_1-ru-gpu-beam-lead500-tail500", Model = "ggml-small-q5_1.bin", Language = "ru", Gpu = true, Beam = true, ExtraLeadMilliseconds = 500, ExtraTailMilliseconds = 500 }
        };

        [Test, Explicit("Development evidence: needs a recorded corpus and whisper models"), Category("SpeechModelComparison")]
        public void CompareModelsOnRecordedSpeech()
        {
            string take = Environment.GetEnvironmentVariable("GO_LIVE_VOICE_CORPUS");
            if (string.IsNullOrEmpty(take))
            {
                string root = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "VoiceCorpus");
                take = Directory.Exists(root) ? Directory.GetDirectories(root).OrderBy(d => d).LastOrDefault() : null;
            }
            Assert.That(take != null && File.Exists(Path.Combine(take, "raw.wav")), "record a take with GO! LIVE/Voice/Speech Corpus Recorder");
            float[] raw = WavFile.Read(Path.Combine(take, "raw.wav"), out int rate);
            Marks marks = JsonUtility.FromJson<Marks>(File.ReadAllText(Path.Combine(take, "marks.json")));
            string output = Environment.GetEnvironmentVariable("GO_LIVE_STT_OUTPUT");
            if (string.IsNullOrWhiteSpace(output))
                output = Path.Combine(take, "comparison", DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            Assert.That(Directory.Exists(output) || File.Exists(output), Is.False, "GO_LIVE_STT_OUTPUT must name a new output directory");
            Directory.CreateDirectory(output);
            using var evidence = new StreamWriter(Path.Combine(output, "raw.jsonl"), false, new UTF8Encoding(false));
            string[] selected = Selection(Environment.GetEnvironmentVariable("GO_LIVE_STT_CONFIGS"));
            string[] vadConfigs = Selection(Environment.GetEnvironmentVariable("GO_LIVE_STT_VAD"));
            string modelRoot = Environment.GetEnvironmentVariable("GO_LIVE_STT_MODEL_ROOT");
            var failures = new List<string>();
            foreach (string name in selected.Concat(vadConfigs).Distinct())
            {
                if (!Configs.Any(c => c.Name == name)) failures.Add("Unknown configuration: " + name);
                else if (selected.Length > 0 && !selected.Contains(name)) failures.Add("VAD configuration not selected: " + name);
            }
            foreach (string failure in failures) Record(evidence, new Evidence { kind = "preflight-error", error = failure });
            Assert.That(failures, Is.Empty);
            Assert.That(marks?.phrases, Is.Not.Null.And.Not.Empty);
            Assert.That(marks.phrases.Select(m => m.index).Distinct().Count(), Is.EqualTo(marks.phrases.Count));
            foreach (Mark mark in marks.phrases)
                Assert.That(mark.start >= 0 && mark.end > mark.start && mark.end <= raw.Length / (double)rate, "Invalid phrase bounds: " + mark.index);
            Record(evidence, new Evidence { kind = "run-start", text = Path.GetFullPath(take), seconds = raw.Length / (double)rate });
            var report = new StringBuilder();
            report.AppendLine($"# Speech model comparison: {Path.GetFileName(take)}");
            report.AppendLine();
            report.AppendLine($"Take: {raw.Length / (double)rate:0.0} s at {rate} Hz, {marks.phrases.Count} marked phrases. Oracle boundaries = the speaker's Space marks.");
            report.AppendLine("All ru configurations force Russian, including any deliberately English control phrases. Transcript means production adapter output after artifact/no-speech filtering, not native tokens. Content recall is a lexical heuristic; review conversational meaning manually. RAM is Windows process PrivateUsage before/after real first-phrase warm-up; GPU memory is total device-0 nvidia-smi usage, not per-process or peak. Delta units are MiB; -1 means unavailable. Warm-up text and cold latency are recorded separately in raw.jsonl. GPU requested does not prove GPU execution: retain the Unity/native backend log.");
            report.AppendLine($"Production input: {AudioPreparation.RecognizerSampleRate} Hz, minimum {SpeechRecognitionWorker.MinimumRecognitionSamples} samples, then {AudioPreparation.DecoderContextSamples} zero samples on each side ({AudioPreparation.DecoderContextSamples / 16} ms). Capture timestamps and VAD boundaries are unchanged. Unsuffixed configurations use this production preparation exactly once; experimental suffixes add context beyond it.");
            report.AppendLine("Configurations suffixed tail500 are benchmark-only endpoint experiments: 500 ms zeros appended AFTER production AudioPreparation, without changing VAD samples/boundaries or capture delay. Their RTF denominator remains original marked audio duration. Unsuffixed configurations use unchanged production preparation.");
            report.AppendLine("Configurations containing lead500 prepend 500 ms zeros to the same prepared samples; lead500-tail500 adds 500 ms zeros to both sides. These are boundary-context experiments, not extra captured audio. Compare all marked phrases and VAD segments before any production change.");
            report.AppendLine();
            var summary = new StringBuilder();
            summary.AppendLine("| Config | Load s | +RAM MiB | +GPU MiB | Mean latency s | p90 s | RTF | CER | WER | Content recall | Invented words | Act match |");
            summary.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            var perPhrase = new Dictionary<string, List<Row>>();
            var activity = AssetDatabase.LoadAssetAtPath<VoiceActivityConfig>("Assets/Game/Config/Voice/VoiceActivity.asset");
            Assert.That(vadConfigs.Length == 0 || activity != null, "Production VAD settings asset missing");

            foreach (Config config in Configs)
            {
                if (selected.Length > 0 && !selected.Contains(config.Name)) continue;
                string path = Path.Combine(Application.streamingAssetsPath, "Whisper", config.Model);
                if (!string.IsNullOrWhiteSpace(modelRoot) && File.Exists(Path.Combine(modelRoot, config.Model)))
                    path = Path.Combine(modelRoot, config.Model);
                if (!File.Exists(path))
                {
                    summary.AppendLine($"| {config.Name} | model file missing ({config.Model}) | | | | | | | | | | |");
                    failures.Add(config.Name + ": model file missing: " + path);
                    Record(evidence, new Evidence { kind = "model-error", config = config.Name, model = path, error = failures.Last() });
                    continue;
                }
                Record(evidence, new Evidence { kind = "config-start", config = config.Name, model = path, language = config.Language,
                    gpuRequested = config.Gpu, beamSearch = config.Beam, productionContextSamplesPerSide = AudioPreparation.DecoderContextSamples,
                    extraTailMilliseconds = config.ExtraTailMilliseconds, extraLeadMilliseconds = config.ExtraLeadMilliseconds });
                try
                {
                long ramBefore = PrivateBytes();
                int gpuBefore = GpuUsedMegabytes();
                var watch = Stopwatch.StartNew();
                using var recognizer = new WhisperSpeechRecognizer(new[] { path }, config.Threads, config.Gpu,
                    new WhisperDecoding { BeamSearch = config.Beam, RussianPrompt = config.Prompt });
                string failure = recognizer.Initialize();
                double load = watch.Elapsed.TotalSeconds;
                if (failure != null)
                {
                    summary.AppendLine($"| {config.Name} | failed: {failure} | | | | | | | | | | |");
                    failures.Add(config.Name + ": " + failure);
                    Record(evidence, new Evidence { kind = "model-error", config = config.Name, model = path, error = failure });
                    continue;
                }
                // Use real speech through production preparation: 16000-sample silence is below whisper's
                // effective minimum and returns early, leaving Vulkan compute setup in the first timed phrase.
                Mark warmMark = marks.phrases[0];
                float[] warmInput = Prepare(Slice(raw, rate, warmMark.start, warmMark.end), rate, config.ExtraTailMilliseconds, config.ExtraLeadMilliseconds);
                Record(evidence, new Evidence { kind = "warmup-start", config = config.Name, index = warmMark.index,
                    start = warmMark.start, end = warmMark.end });
                var warmTimer = Stopwatch.StartNew();
                SpeechRecognitionResult warmResult = recognizer.Recognize(warmInput, config.Language, "ru");
                Record(evidence, new Evidence { kind = "warmup", config = config.Name, index = warmMark.index,
                    text = warmResult.Text, language = warmResult.Language, seconds = warmTimer.Elapsed.TotalSeconds });
                long ramAfter = PrivateBytes();
                int gpuAfter = GpuUsedMegabytes();
                Record(evidence, new Evidence { kind = "loaded", config = config.Name, seconds = load, ramBeforeBytes = ramBefore,
                    ramAfterBytes = ramAfter, ramGpuBeforeMb = gpuBefore, ramGpuAfterMb = gpuAfter });
                var rows = new List<Row>();
                foreach (Mark mark in marks.phrases)
                {
                    float[] phrase = Slice(raw, rate, mark.start, mark.end);
                    float[] input = Prepare(phrase, rate, config.ExtraTailMilliseconds, config.ExtraLeadMilliseconds);
                    var timer = Stopwatch.StartNew();
                    Record(evidence, new Evidence { kind = "phrase-start", config = config.Name, index = mark.index, start = mark.start, end = mark.end, text = mark.text });
                    timer.Restart();
                    try
                    {
                        SpeechRecognitionResult result = recognizer.Recognize(input, config.Language, "ru");
                        Row row = Score(mark.index, mark.text, result.Text, result.Language, timer.Elapsed.TotalSeconds, phrase.Length / (double)rate);
                        rows.Add(row);
                        Record(evidence, new Evidence { kind = "phrase", config = config.Name, index = mark.index, start = mark.start, end = mark.end, score = row, text = result.Text, language = result.Language, seconds = row.Seconds });
                    }
                    catch (Exception exception)
                    {
                        failures.Add(config.Name + " phrase " + mark.index + ": " + exception.Message);
                        rows.Add(Score(mark.index, mark.text, "", "", timer.Elapsed.TotalSeconds, phrase.Length / (double)rate));
                        Record(evidence, new Evidence { kind = "phrase-error", config = config.Name, index = mark.index, start = mark.start, end = mark.end, error = exception.ToString(), seconds = timer.Elapsed.TotalSeconds });
                    }
                }
                perPhrase[config.Name] = rows;
                summary.AppendLine(Summary(config.Name, load, ramBefore < 0 || ramAfter < 0 ? -1 : ramAfter - ramBefore,
                    gpuBefore < 0 || gpuAfter < 0 ? -1 : gpuAfter - gpuBefore, rows));
                File.WriteAllText(Path.Combine(output, "summary.md"), summary.ToString(), new UTF8Encoding(false));

                if (!vadConfigs.Contains(config.Name)) continue;
                report.AppendLine($"## Production VAD segmentation with {config.Name}");
                report.AppendLine();
                report.AppendLine(VadRun(raw, rate, marks, activity.Settings, recognizer, config.Language, config.Name,
                    config.ExtraTailMilliseconds, config.ExtraLeadMilliseconds, evidence, failures));
                }
                catch (Exception exception)
                {
                    failures.Add(config.Name + ": " + exception.Message);
                    Record(evidence, new Evidence { kind = "config-error", config = config.Name, error = exception.ToString() });
                }
            }

            report.AppendLine("## Summary");
            report.AppendLine();
            report.AppendLine("CER/WER: character/word error rate after lowercasing, ё→е and dropping punctuation. Content recall: share of the " +
                              "reference's content words (3+ letters, not filler) whose first 4 letters appear in the transcript. Invented words: " +
                              "transcript words matching no reference word by the same rule. Act match: the Viewer Core speech acts of the " +
                              "transcript equal those of the reference text. Latency: recognition only (oracle phrase audio), after one warm-up call.");
            report.AppendLine();
            report.Append(summary);
            report.AppendLine();
            report.AppendLine("## Per phrase");
            report.AppendLine();
            foreach (Mark mark in marks.phrases)
            {
                report.AppendLine($"### {mark.index}. «{mark.text}» ({mark.end - mark.start:0.0} s)");
                report.AppendLine();
                report.AppendLine("| Config | Transcript | Lang | CER | Recall | Acts | s |");
                report.AppendLine("|---|---|---|---:|---:|---|---:|");
                foreach (var pair in perPhrase)
                {
                    Row row = pair.Value.First(r => r.Phrase == mark.index);
                    report.AppendLine($"| {pair.Key} | {Escape(row.Text)} | {row.Language} | {row.Cer:0.00} | {row.Recall:0.00} | {row.Acts}{(row.Acts == row.ReferenceActs ? "" : " (ref " + row.ReferenceActs + ")")} | {row.Seconds:0.00} |");
                }
                report.AppendLine();
            }
            File.WriteAllText(Path.Combine(output, "report.md"), report.ToString(), new UTF8Encoding(false));
            Debug.Log(summary.ToString());
            TestContext.WriteLine(summary.ToString());
            TestContext.WriteLine("Evidence: " + output);
            Record(evidence, new Evidence { kind = "run-finish", error = string.Join("\n", failures) });
            Assert.That(failures, Is.Empty, "Comparison incomplete; see raw.jsonl and report.md in " + output);
        }

        private static string[] Selection(string value) => (value ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToArray();

        private static float[] Prepare(float[] samples, int rate, int extraTailMilliseconds, int extraLeadMilliseconds)
        {
            float[] input = AudioPreparation.ToRecognizerInput(samples, rate);
            if (extraTailMilliseconds <= 0 && extraLeadMilliseconds <= 0) return input;
            int lead = AudioPreparation.RecognizerSampleRate * extraLeadMilliseconds / 1000;
            int tail = AudioPreparation.RecognizerSampleRate * extraTailMilliseconds / 1000;
            var padded = new float[input.Length + lead + tail];
            Array.Copy(input, 0, padded, lead, input.Length);
            return padded;
        }

        // The production VAD over the whole take: which segments each phrase became, how their edges relate to the
        // spoken interval, and what the recognizer made of them.
        private static string VadRun(float[] raw, int rate, Marks marks, VoiceActivitySettings settings, WhisperSpeechRecognizer recognizer, string language,
            string config, int extraTailMilliseconds, int extraLeadMilliseconds, StreamWriter evidence, List<string> failures)
        {
            var vad = new VoiceActivityDetector(settings, rate);
            var segments = new List<SpeechSegment>();
            int block = rate / 50;
            for (int offset = 0; offset < raw.Length; offset += block)
                vad.Process(raw, offset, Math.Min(block, raw.Length - offset), segments);
            float[] tail = new float[rate];
            vad.Process(tail, 0, tail.Length, segments);
            var text = new StringBuilder();
            text.AppendLine($"{segments.Count} segments for {marks.phrases.Count} phrases (settings: min speech {settings.MinimumSpeechMilliseconds} ms, " +
                            $"silence timeout {settings.SilenceTimeoutMilliseconds} ms, pre-roll {settings.PreRollMilliseconds} ms, floor {settings.MinimumSpeechRms}).");
            text.AppendLine();
            text.AppendLine("| # | Start s | End s | Voiced s | Cut | Overlaps phrases | Onset before start (ms) | Transcript |");
            text.AppendLine("|---:|---:|---:|---:|---|---|---:|---|");
            int index = 0;
            foreach (SpeechSegment segment in segments)
            {
                index++;
                double end = segment.EndSample / (double)rate;
                double start = end - segment.Samples.Length / (double)rate;
                var overlapping = marks.phrases.Where(m => m.start < end && m.end > start).Select(m => m.index.ToString(CultureInfo.InvariantCulture)).ToList();
                // Energy onset in the 400 ms before the segment: speech the segment may have clipped.
                double onset = OnsetBefore(raw, rate, start, settings.MinimumSpeechRms * .5f);
                var item = new Evidence { kind = "vad-start", config = config, index = index, start = start, end = end,
                    voicedSeconds = segment.VoicedSeconds, truncated = segment.Truncated, overlaps = string.Join(",", overlapping), onsetBeforeMs = onset };
                Record(evidence, item);
                var timer = Stopwatch.StartNew();
                SpeechRecognitionResult result = default;
                try
                {
                    result = recognizer.Recognize(Prepare(segment.Samples, rate, extraTailMilliseconds, extraLeadMilliseconds), language, "ru");
                    item.kind = "vad"; item.text = result.Text; item.language = result.Language;
                }
                catch (Exception exception)
                {
                    item.kind = "vad-error"; item.error = exception.ToString();
                    failures.Add(config + " VAD " + index + ": " + exception.Message);
                }
                item.seconds = timer.Elapsed.TotalSeconds;
                Record(evidence, item);
                text.AppendLine($"| {index} | {start:0.00} | {end:0.00} | {segment.VoicedSeconds:0.00} | {(segment.Truncated ? "max length" : "")} | " +
                                $"{string.Join(",", overlapping)} | {onset:0} | {Escape(result.Text)} |");
            }
            text.AppendLine();
            foreach (Mark mark in marks.phrases)
            {
                int count = segments.Count(s =>
                {
                    double end = s.EndSample / (double)rate, start = end - s.Samples.Length / (double)rate;
                    return start < mark.end && end > mark.start;
                });
                if (count != 1) text.AppendLine($"- phrase {mark.index} «{mark.text}» → {count} segments");
            }
            return text.ToString();
        }

        private static double OnsetBefore(float[] raw, int rate, double start, float threshold)
        {
            int frame = rate / 50;
            int from = Math.Max(0, (int)((start - .4) * rate));
            int to = Math.Max(0, (int)(start * rate));
            for (int position = from; position + frame <= to; position += frame)
            {
                double sum = 0;
                for (int i = position; i < position + frame; i++) sum += raw[i] * (double)raw[i];
                if (Math.Sqrt(sum / frame) >= threshold) return (start - position / (double)rate) * 1000;
            }
            return 0;
        }

        private static Row Score(int phrase, string reference, string text, string language, double seconds, double audioSeconds)
        {
            List<string> referenceWords = SpeechRelevance.Tokens(reference);
            List<string> words = SpeechRelevance.Tokens(text);
            var content = referenceWords.Where(w => w.Length >= 3 && !Filler.Contains(w)).ToList();
            int recalled = content.Count(w => words.Any(t => SameStem(t, w)));
            int invented = words.Count(t => !referenceWords.Any(w => SameStem(t, w)));
            return new Row
            {
                Phrase = phrase, Reference = reference, Text = text, Language = language, Seconds = seconds, AudioSeconds = audioSeconds,
                Cer = Distance(string.Join(" ", referenceWords), string.Join(" ", words)) / (double)Math.Max(1, string.Join(" ", referenceWords).Length),
                Wer = Distance(referenceWords, words) / (double)Math.Max(1, referenceWords.Count),
                Recall = content.Count == 0 ? 1 : recalled / (double)content.Count, Invented = invented,
                Acts = Acts(text), ReferenceActs = Acts(reference)
            };
        }

        private static string Acts(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "(nothing)";
            SpeechAnalysis analysis = SpeechRelevance.Analyze(new RecognizedSpeech(1, text, 0, null, "ru"), Array.Empty<ViewerNameForms>());
            return analysis.Acts.ToString();
        }

        private static readonly HashSet<string> Filler = new() { "так", "ну", "вот", "это", "как", "что", "вы", "ты", "мне", "все", "всё" };

        private static bool SameStem(string a, string b)
        {
            int length = Math.Min(4, Math.Min(a.Length, b.Length));
            if (a.Length < 3 || b.Length < 3) return a == b;
            return string.CompareOrdinal(a, 0, b, 0, length) == 0;
        }

        private static string Summary(string name, double load, long ramBytes, int gpuMegabytes, List<Row> rows)
        {
            var latencies = rows.Select(r => r.Seconds).OrderBy(v => v).ToList();
            double audio = rows.Sum(r => r.AudioSeconds);
            int matches = rows.Count(r => r.Acts == r.ReferenceActs);
            return $"| {name} | {load:0.00} | {(ramBytes == -1 ? -1 : ramBytes / 1048576.0):0} | {gpuMegabytes} | {latencies.Average():0.00} | " +
                   $"{latencies[(int)Math.Round(.9 * (latencies.Count - 1))]:0.00} | {latencies.Sum() / Math.Max(.001, audio):0.00} | " +
                   $"{rows.Average(r => r.Cer):0.00} | {rows.Average(r => r.Wer):0.00} | {rows.Average(r => r.Recall):0.00} | " +
                   $"{rows.Sum(r => r.Invented)} | {matches}/{rows.Count} |";
        }

        private static float[] Slice(float[] raw, int rate, double start, double end)
        {
            int from = Math.Clamp((int)(start * rate), 0, raw.Length);
            int to = Math.Clamp((int)(end * rate), from, raw.Length);
            var slice = new float[to - from];
            Array.Copy(raw, from, slice, 0, slice.Length);
            return slice;
        }

        private static int Distance(string a, string b)
        {
            var previous = new int[b.Length + 1];
            var current = new int[b.Length + 1];
            for (int j = 0; j <= b.Length; j++) previous[j] = j;
            for (int i = 1; i <= a.Length; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Length; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                (previous, current) = (current, previous);
            }
            return previous[b.Length];
        }

        private static int Distance(List<string> a, List<string> b)
        {
            var previous = new int[b.Count + 1];
            var current = new int[b.Count + 1];
            for (int j = 0; j <= b.Count; j++) previous[j] = j;
            for (int i = 1; i <= a.Count; i++)
            {
                current[0] = i;
                for (int j = 1; j <= b.Count; j++)
                    current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1), previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
                (previous, current) = (current, previous);
            }
            return previous[b.Count];
        }

        private static long PrivateBytes()
        {
            GC.Collect();
            try
            {
                using Process process = Process.GetCurrentProcess();
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    var counters = new ProcessMemoryCounters { Size = (uint)Marshal.SizeOf<ProcessMemoryCounters>() };
                    return GetProcessMemoryInfo(process.Handle, ref counters, counters.Size)
                        ? checked((long)counters.PrivateUsage.ToUInt64()) : -1;
                }
                process.Refresh();
                return process.PrivateMemorySize64 > 0 ? process.PrivateMemorySize64 : -1;
            }
            catch (Exception) { return -1; }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessMemoryCounters
        {
            public uint Size, PageFaultCount;
            public UIntPtr PeakWorkingSetSize, WorkingSetSize, QuotaPeakPagedPoolUsage, QuotaPagedPoolUsage,
                QuotaPeakNonPagedPoolUsage, QuotaNonPagedPoolUsage, PagefileUsage, PeakPagefileUsage, PrivateUsage;
        }

        [DllImport("psapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetProcessMemoryInfo(IntPtr process, ref ProcessMemoryCounters counters, uint size);

        private static int GpuUsedMegabytes()
        {
            try
            {
                using Process process = Process.Start(new ProcessStartInfo("nvidia-smi", "--query-gpu=memory.used --format=csv,noheader,nounits")
                    { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true });
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(5000);
                return int.TryParse(output.Trim().Split('\n')[0], out int value) ? value : -1;
            }
            catch (Exception) { return -1; }
        }

        private static string Escape(string text) => string.IsNullOrEmpty(text) ? "∅" : text.Replace("|", "/");
    }
}
