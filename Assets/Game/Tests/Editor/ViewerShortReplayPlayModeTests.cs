using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using GoLive.Desktop;
using GoLive.Editor.Voice;
using GoLive.Localization;
using GoLive.Viewers;
using GoLive.Voice;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GoLive.Tests
{
    public sealed partial class DesktopFlowPlayModeTests
    {
        // Real GL scene, real production voice worker and real local chat adapter. Only the microphone DEVICE is
        // replaced by three unchanged slices of the user's recording, fed in real time. This is NOT fresh live speech.
        [UnityTest, Explicit("Requires the recorded microphone corpus and configured local chat model"),
         Category("ViewerCoreAcceptance"), Timeout(240000)]
        public IEnumerator SeventyFiveSecondRecordedConversationUsesTheProductionScenePipeline()
        {
            string take = Environment.GetEnvironmentVariable("GO_LIVE_VOICE_CORPUS") ??
                          Path.Combine(Directory.GetCurrentDirectory(), "Logs/VoiceCorpus/20260926-182411");
            string folder = Environment.GetEnvironmentVariable("GO_LIVE_SHORT_ACCEPTANCE_OUTPUT") ??
                            Path.Combine(Directory.GetCurrentDirectory(), "Logs/ViewerShortReplay/" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            Assert.That(Directory.Exists(folder), Is.False, "preserve previous evidence; use a new output directory");
            float[] recorded = WavFile.Read(Path.Combine(take, "raw.wav"), out int rate);
            ShortMarks marks = JsonUtility.FromJson<ShortMarks>(File.ReadAllText(Path.Combine(take, "marks.json")));
            var timeline = new float[rate * 75];
            PlaceRecordedPhrase(timeline, recorded, rate, marks.phrases.Single(m => m.index == 1), 2);
            PlaceRecordedPhrase(timeline, recorded, rate, marks.phrases.Single(m => m.index == 4), 25);
            PlaceRecordedPhrase(timeline, recorded, rate, marks.phrases.Single(m => m.index == 8), 39);
            Directory.CreateDirectory(folder);
            using var evidence = new PlaySink(Path.Combine(folder, "raw.jsonl"));
            WritePlay(evidence, "method", "Recorded real microphone audio, not fresh capture. 75 real seconds in GL; unchanged marks 1/4/8 placed at 2/25/39 seconds. Natural audience, scene-owned VoiceRecognition and StreamSpeechFeed, ordinary selection/planning/validation, actual configured local language model. No viewer seats, probabilities, daily state or total audience are injected.");
            WritePlay(evidence, "source", take);

            ExpectShelfWarning();
            yield return new EnterPlayMode(false);
            yield return Boot();
            _localization.SetLanguage(GameLanguage.Russian);
            VoiceInputBehaviour bridge = One<VoiceInputBehaviour>();
            bridge.enabled = false; // BEFORE starting: no real OS microphone is opened by this replay.
            VoiceRecognition recognition = bridge.Recognition;
            recognition.SetEnabled(true);
            recognition.Language = SpeechLanguage.Russian;
            recognition.FallbackLanguage = SpeechLanguage.Russian;
            ViewerCore core = _runtime.State.Viewers;
            core.Director.ModelEnabled = true;
            var modelField = typeof(ChatDirector).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic);
            var original = (IViewerLanguageModel)modelField.GetValue(core.Director);
            Assert.That(original, Is.Not.Null, "the scene must provide its configured local model adapter");
            var model = new PlayModelRecorder(original, evidence, core.Director);
            modelField.SetValue(core.Director, model);
            var heard = new List<ShortSpeech>();
            var trace = new List<ReactionLogEntry>();
            double started = 0;
            Action<ReactionLogEntry> onTrace = entry =>
            {
                trace.Add(entry);
                WritePlay(evidence, "reaction", JsonUtility.ToJson(new ShortTrace(entry, started > 0 ? SpeechClock.Now - started : -1)));
            };
            // Shown fires after publication; ReactionLog.Added precedes the final entry.Text assignment.
            Action<StreamChatMessage, ReactionIntent> onShown = (line, intent) => WritePlay(evidence, "published",
                JsonUtility.ToJson(new ShortPublished { intent = intent.Id, viewerId = line.ViewerId, viewer = line.SenderName,
                    text = line.Text, source = line.Source.ToString(), realSeconds = started > 0 ? SpeechClock.Now - started : -1 }));
            Action<RecognizedSpeech> onSpeech = speech =>
            {
                SpeechAnalysis analysis = SpeechRelevance.Analyze(speech, core.Community.KnownNames);
                ConversationThread thread = core.Selector.Thread;
                var row = new ShortSpeech { sequence = speech.Sequence, text = speech.Text, language = speech.Language,
                    realSeconds = SpeechClock.Now - started, audioEndSeconds = speech.Timestamp - started,
                    endToTextSeconds = SpeechClock.Now - speech.Timestamp, primaryAct = analysis.PrimaryAct.ToString(),
                    acts = analysis.Acts.ToString(), viewers = core.Roster.AudienceSize,
                    threadViewerId = thread.ViewerId, threadTurns = thread.Turns, threadAge = thread.ViewerId == null ? -1 : core.Events.Now - thread.LastAt,
                    threadWatching = core.Roster.IsWatching(thread.ViewerId), threadEpoch = thread.Epoch,
                    rosterEpoch = core.Roster.Epoch(thread.ViewerId) };
                heard.Add(row);
                WritePlay(evidence, "speech", JsonUtility.ToJson(row));
            };
            Action<PhraseDiagnostics> onDiagnostics = phrase => WritePlay(evidence, "recognition", JsonUtility.ToJson(new ShortRecognition
            {
                sequence = phrase.Sequence, text = phrase.Text, error = phrase.Error, recognitionSeconds = phrase.RecognitionSeconds,
                endToTextSeconds = phrase.EndToTextSeconds, audioSeconds = phrase.AudioSeconds, truncated = phrase.Truncated
            }));
            core.Log.Added += onTrace;
            core.Director.Shown += onShown;
            recognition.Recognized += onSpeech;
            recognition.PhraseFinished += onDiagnostics;
            try
            {
                yield return OpenLinkedStreamly();
                var view = One<StreamlyView>();
                // The ordinary helper advances fifteen simulated minutes; this test deliberately starts without it.
                Click(Field<Button>(view, "startStop"));
                yield return PlayModeWait.Until(() => _runtime.State.Stream.State == StreamState.Live, "the first stream to start");
                Assert.That(_runtime.State.Stream.Audience.CurrentViewers, Is.InRange(1, 3));
                recognition.BeginListening(rate);
                yield return PlayModeWait.Until(() => { recognition.Update(); return recognition.IsListening; }, "the shipping speech model to load", 60);
                recognition.BeginListening(rate); // Set the capture clock to the start of the real-time replay.
                started = SpeechClock.Now;
                var block = new float[rate / 50];
                int sent = 0;
                while (sent < timeline.Length)
                {
                    int available = Math.Min(timeline.Length, (int)((SpeechClock.Now - started) * rate));
                    while (sent + block.Length <= available)
                    {
                        Array.Copy(timeline, sent, block, 0, block.Length);
                        recognition.Submit(block, block.Length);
                        sent += block.Length;
                    }
                    recognition.Update();
                    yield return null;
                }
                recognition.Update();
                WritePlay(evidence, "duration", (SpeechClock.Now - started).ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
                Assert.That(bridge.IsCapturing, Is.False);
                Assert.That(heard.Count, Is.EqualTo(3), "all three recorded phrases, no recognition invented in silence");
                Assert.That(heard[0].primaryAct, Is.EqualTo(SpeechAct.PersonalQuestion.ToString()));
                Assert.That(heard[0].viewers, Is.InRange(1, 3));
                Assert.That(heard[0].text.ToLowerInvariant(), Does.Contain("как дела").And.Contain("настроение").And.Contain("сегодня делали"));
                Assert.That(heard[1].text.ToLowerInvariant(), Does.Contain("секунду"));
                Assert.That(heard[2].text.ToLowerInvariant(), Does.Contain("устал"));
                ReactionLogEntry[] opening = trace.Where(e => e.Speech == heard[0].text && e.Outcome == ReactionOutcome.Shown).ToArray();
                ReactionLogEntry[] filler = trace.Where(e => e.Speech == heard[1].text && e.Outcome == ReactionOutcome.Shown).ToArray();
                ReactionLogEntry[] followup = trace.Where(e => e.Speech == heard[2].text && e.Outcome == ReactionOutcome.Shown).ToArray();
                Assert.That(opening.Length, Is.InRange(1, 2), "normally one meaningful answer, at most a bounded second voice");
                Assert.That(opening[0].Source, Is.EqualTo(ReactionSource.LanguageModel), "exercise the actual local model, not only fallback");
                Assert.That(opening[0].Plan, Does.Contain("day:"));
                Assert.That(opening[0].Text, Does.Not.Match(@"(?i)^(hi|hello|прив(ет)?|йо|я тут)[!.… ]*$"),
                    "a bare greeting is not a personal answer; review actual content separately");
                Assert.That(filler, Is.Empty, "filler causes no response; unrelated ambient activity is reported separately");
                Assert.That(followup, Is.Not.Empty, "short personal follow-up continues the exchange");
                Assert.That(followup[0].ViewerId, Is.EqualTo(opening[0].ViewerId));
                Assert.That(followup[0].Plan, Does.Contain("follow-up"));
                WritePlay(evidence, "accepted", "Opening personal answer, filler silence and same-viewer follow-up observed through the production scene. Semantic content and hard-fact safety still receive a separate review of the raw outputs.");
                yield return CaptureApp("viewer-short-replay-ru", DesktopAppId.Streamly);
                yield return StopBroadcast(view);
            }
            finally
            {
                recognition.Recognized -= onSpeech;
                recognition.PhraseFinished -= onDiagnostics;
                recognition.EndListening();
                core.Log.Added -= onTrace;
                core.Director.Shown -= onShown;
                core.Director.CancelAll("short replay finished");
                model.Drain();
                modelField.SetValue(core.Director, original);
            }
        }

        private static void PlaceRecordedPhrase(float[] target, float[] source, int rate, ShortMark mark, int atSeconds)
        {
            int from = (int)(mark.start * rate), count = (int)((mark.end - mark.start) * rate);
            Array.Copy(source, from, target, atSeconds * rate, count);
        }
        [Serializable] private sealed class ShortMark { public int index; public string text; public double start, end; }
        [Serializable] private sealed class ShortMarks { public List<ShortMark> phrases = new(); }
        [Serializable] private sealed class ShortSpeech
        {
            public long sequence, threadEpoch, rosterEpoch; public string text, language, primaryAct, acts, threadViewerId;
            public double realSeconds, audioEndSeconds, endToTextSeconds; public int viewers;
            public int threadTurns; public double threadAge; public bool threadWatching;
        }
        [Serializable] private sealed class ShortRecognition
        {
            public long sequence; public string text, error;
            public double recognitionSeconds, endToTextSeconds, audioSeconds; public bool truncated;
        }
        [Serializable] private sealed class ShortPublished
        {
            public long intent; public string viewerId, viewer, text, source; public double realSeconds;
        }
        [Serializable] private sealed class ShortTrace
        {
            public string eventKey, speech, outcome, reason, viewerId, viewer, text, source, plan;
            public long intent; public double realSeconds, streamSeconds, latencySeconds;
            public ShortTrace(ReactionLogEntry entry, double at)
            {
                eventKey = entry.EventKey; speech = entry.Speech; outcome = entry.Outcome.ToString(); reason = entry.Reason;
                viewerId = entry.ViewerId; viewer = entry.ViewerName; text = entry.Text; source = entry.Source.ToString();
                plan = entry.Plan; intent = entry.IntentId; realSeconds = at; streamSeconds = entry.StreamSeconds; latencySeconds = entry.LatencySeconds;
            }
        }
    }
}
