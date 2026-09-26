using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using GoLive.Desktop;
using GoLive.Viewers;
using GoLive.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GoLive.Tests
{
    // Viewer Core end-to-end acceptance in the real GL scene with the REAL local model and the REAL speech pipeline:
    // recorded phrases -> VAD -> whisper -> StreamSpeechFeed -> speech event -> relevance -> viewer -> local model ->
    // validator -> visible chat. Only the OS microphone device is replaced by WAV files (GO_LIVE_ACCEPTANCE_WAV, 16 kHz
    // mono 16-bit PCM: interesting_*.wav and filler_*.wav); the scene's own microphone capture is switched off.
    public sealed partial class DesktopFlowPlayModeTests
    {
        [UnityTest, Explicit("Needs a running local model and recorded phrases"), Category("ViewerCoreAcceptance"), Timeout(900000)]
        public IEnumerator RecordedSpeechReachesViewerChatThroughTheLocalModel()
        {
            string folder = Environment.GetEnvironmentVariable("GO_LIVE_ACCEPTANCE_WAV");
            Assert.That(Directory.Exists(folder), Is.True, "set GO_LIVE_ACCEPTANCE_WAV to the recorded phrases folder");
            var report = new StringBuilder();
            ExpectShelfWarning();
            yield return new EnterPlayMode(false);
            yield return Boot();
            ViewerCore viewers = _runtime.State.Viewers;
            viewers.Director.ModelEnabled = true;
            Assert.That(viewers.Director.Health, Is.EqualTo(ChatModelHealth.Available), "start the local model first (GO! LIVE/Viewer Core/Start Local Model)");
            yield return OpenLinkedStreamly();
            var view = One<StreamlyView>();
            var voiceBridge = One<VoiceInputBehaviour>();
            voiceBridge.enabled = false; // This replay substitutes recorded input before the broadcast requests the device.
            yield return StartBroadcast(view);
            // The simulation decides the audience; let it run until a few people watch (nobody reacts to an empty room).
            for (int minute = 0; minute < 60 && _runtime.State.Stream.Audience.CurrentViewers < 3; minute++)
            {
                _runtime.State.Stream.Tick(60, StreamSessionTests.PrimeTime);
                yield return PlayModeWait.Frames(1);
            }
            yield return PlayModeWait.Frames(5);

            // Scene ownership includes the evaluated model, decoding and preparation settings.
            // DesktopRuntimeBehaviour already forwards this instance's recognized speech to StreamSpeechFeed.
            VoiceRecognition recognition = voiceBridge.Recognition;
            recognition.SetEnabled(true);
            recognition.Language = SpeechLanguage.Russian;
            var heard = new List<RecognizedSpeech>();
            recognition.Recognized += speech =>
            {
                heard.Add(speech);
            };
            recognition.BeginListening(16000);
            yield return PlayModeWait.Until(() => { recognition.Update(); return recognition.Status == VoiceStatus.Listening; }, "the speech model to load", 60);
            Note(report, $"LIVE: {viewers.Roster.AudienceSize} viewers, model {viewers.Director.Health}, channel {_runtime.State.Trich.Name}");

            bool reacted = false;
            foreach (string file in Directory.GetFiles(folder, "interesting_*.wav").OrderBy(f => f))
            {
                ReactionLogEntry speech = null;
                yield return Speak(recognition, heard, file, report, entry => speech = entry);
                if (speech == null) continue;
                var shown = new List<ReactionLogEntry>();
                yield return PlayModeWait.Until(() =>
                {
                    var entries = viewers.Log.Entries.Where(e => e.EventKey == speech.EventKey).ToList();
                    shown = entries.Where(e => e.Outcome == ReactionOutcome.Shown).ToList();
                    int scheduled = entries.Count(e => e.Outcome == ReactionOutcome.Scheduled);
                    int finished = entries.Count(e => e.Outcome == ReactionOutcome.Shown || e.Outcome == ReactionOutcome.Discarded || e.Outcome == ReactionOutcome.Dropped);
                    return entries.Any(e => e.Outcome == ReactionOutcome.Rejected) || (scheduled > 0 && finished >= scheduled);
                }, "the chat to react (or let the moment pass)", 30);
                foreach (ReactionLogEntry entry in viewers.Log.Entries.Where(e => e.EventKey == speech.EventKey))
                    Note(report, $"    {entry.Outcome,-9} {entry.ViewerName} [{entry.Source} {entry.LatencySeconds * 1000:0} ms, ~{entry.PromptCharacters / 4} tok] " +
                                      $"{entry.Reason} {(entry.Text == null ? "" : ": " + entry.Text)}");
                if (shown.Any(e => e.Source == ReactionSource.LanguageModel))
                {
                    reacted = true;
                    yield return CaptureApp("viewer-core-b12-ru-reaction", DesktopAppId.Streamly);
                    break;
                }
            }
            Directory.CreateDirectory(DesktopVisualCapture.OutputDirectory);
            File.WriteAllText(Path.Combine(DesktopVisualCapture.OutputDirectory, "viewer-core-b12.txt"), report.ToString(), new UTF8Encoding(false));
            Assert.That(reacted, Is.True, "an interesting phrase got a model-written viewer reaction");

            foreach (string file in Directory.GetFiles(folder, "filler_*.wav").OrderBy(f => f))
            {
                ReactionLogEntry filler = null;
                yield return Speak(recognition, heard, file, report, entry => filler = entry);
                Assert.That(filler, Is.Not.Null, "the filler phrase was recognized");
                Assert.That(filler.Outcome, Is.EqualTo(ReactionOutcome.Rejected), "low-value speech gets no reaction");
                Assert.That(filler.Reason, Is.EqualTo("speech below threshold"));
            }
            recognition.EndListening();
            Directory.CreateDirectory(DesktopVisualCapture.OutputDirectory);
            File.WriteAllText(Path.Combine(DesktopVisualCapture.OutputDirectory, "viewer-core-b12.txt"), report.ToString(), new UTF8Encoding(false));
            yield return StopBroadcast(view);
        }

        // Feeds one recording through the real VAD/recognizer in 20 ms blocks (plus trailing silence), then returns the
        // viewer core's trace entry for the resulting speech event.
        private IEnumerator Speak(VoiceRecognition recognition, List<RecognizedSpeech> heard, string file, StringBuilder report, Action<ReactionLogEntry> found)
        {
            int before = heard.Count;
            float[] samples = ReadWav(file);
            var block = new float[320];
            float[] padded = samples.Concat(new float[16000 * 2]).ToArray();
            for (int offset = 0; offset < padded.Length; offset += block.Length)
            {
                int count = Math.Min(block.Length, padded.Length - offset);
                Array.Copy(padded, offset, block, 0, count);
                recognition.Submit(block, count);
                recognition.Update();
                if (offset % (block.Length * 5) == 0) yield return null;
            }
            yield return PlayModeWait.Until(() => { recognition.Update(); return heard.Count > before; }, "whisper to recognize " + Path.GetFileName(file), 30);
            RecognizedSpeech speech = heard[heard.Count - 1];
            ReactionLogEntry entry = null;
            yield return PlayModeWait.Until(() =>
            {
                entry = _runtime.State.Viewers.Log.Entries.LastOrDefault(e => e.EventKind == StreamEventKind.StreamerSpeech && e.Speech == speech.Text);
                return entry != null;
            }, "the speech event", 10);
            Note(report, $"{Path.GetFileName(file)} -> whisper «{speech.Text}» ({speech.Language}) -> relevance {entry.Relevance:0.00} -> {entry.Outcome} {entry.Reason}");
            found(entry);
        }

        private static void Note(StringBuilder report, string line)
        {
            report.AppendLine(line);
            TestContext.WriteLine(line);
        }

        private static float[] ReadWav(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            int position = 12;
            while (position + 8 <= bytes.Length)
            {
                string id = Encoding.ASCII.GetString(bytes, position, 4);
                int size = BitConverter.ToInt32(bytes, position + 4);
                if (id == "data")
                {
                    var samples = new float[size / 2];
                    for (int i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(bytes, position + 8 + i * 2) / 32768f;
                    return samples;
                }
                position += 8 + size + (size & 1);
            }
            throw new InvalidDataException(path + " has no PCM data");
        }
    }
}
