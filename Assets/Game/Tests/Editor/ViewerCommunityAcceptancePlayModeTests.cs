using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Desktop;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Localization;
using GoLive.PcBuilding;
using GoLive.Persistence;
using GoLive.Viewers;
using GoLive.Voice;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace GoLive.Tests
{
    public sealed partial class DesktopFlowPlayModeTests
    {
        // Actual GL scene, input, save pipeline, ordinary viewer core and HTTP adapter. This is NOT a real-microphone
        // acceptance: scripted recognition results isolate community knowledge. Only witness seat assignment and
        // a high-audience configuration are controlled fixtures; total viewers always come from AudienceSimulation.
        [UnityTest, Explicit("Requires the configured local model"), Category("ViewerCoreAcceptance"), Timeout(900000)]
        public IEnumerator LivingCommunityPersistenceOutageAndBilingualChat()
        {
            ExpectShelfWarning();
            yield return new EnterPlayMode(false);
            yield return Boot();
            One<VoiceInputBehaviour>().enabled = false;
            Directory.CreateDirectory(DesktopVisualCapture.OutputDirectory);
            string output = Path.Combine(DesktopVisualCapture.OutputDirectory, "living-community-play.jsonl");
            using var evidence = new PlaySink(output);
            var core = _runtime.State.Viewers;
            var stream = _runtime.State.Stream;
            var clock = One<GameClockBehaviour>().Clock;
            var wallet = One<WalletBehaviour>().Wallet;
            var modelField = typeof(ChatDirector).GetField("_model", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(modelField, Is.Not.Null);
            var originalModel = (IViewerLanguageModel)modelField.GetValue(core.Director);
            var model = new PlayModelRecorder(originalModel, evidence, core.Director);
            modelField.SetValue(core.Director, model);
            bool recording = true;
            int receipts = 0;
            var published = new List<(StreamChatMessage line, ReactionIntent intent)>();
            Action<ReactionLogEntry> onTrace = entry => { if (recording) WritePlay(evidence, "trace", JsonUtility.ToJson(new PlayTrace(entry))); };
            Action<StreamChatMessage, ReactionIntent> onShown = (line, intent) => {
                if (!recording) return;
                published.Add((line, intent));
                WritePlay(evidence, "chat", $"{line.Id} | intent={intent.Id} | event={intent.Event.Key} | viewer={line.ViewerId} | {line.SenderName} | {line.Source} | {line.DonationCents} | {line.Text}");
            };
            Action<DonationReceipt> onReceipt = receipt => { receipts++; if (recording) WritePlay(evidence, "receipt", $"{receipt.Id} | {receipt.SenderName} | {receipt.AmountCents}"); };
            core.Log.Added += onTrace;
            core.Director.Shown += onShown;
            _runtime.State.Donation.Received += onReceipt;
            try
            {
            core.Director.ModelEnabled = true;
            clock.AdvanceMinutes((22 * 60 - clock.Current.MinuteOfDay + 1440) % 1440);
            typeof(StreamSession).GetField("_seeds", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(stream, new AudienceRandom(260926));
            yield return OpenLinkedStreamly();
            var view = One<StreamlyView>();
            Click(Field<Button>(view, "startStop"));
            yield return PlayModeWait.Until(() => stream.State == StreamState.Live, "first live stream");
            for (int step = 0; step < 60 && stream.Audience.CurrentViewers < 2; step++)
            {
                stream.Tick(10, StreamSessionTests.PrimeTime);
                yield return PlayModeWait.Frames(1);
            }
            Assert.That(stream.Audience.CurrentViewers, Is.GreaterThanOrEqualTo(2));
            ViewerProfile witness = core.Community.Profiles.Single(p => p.DisplayName == "NightOwl");
            ViewerProfile absent = core.Community.Profiles.Single(p => p.DisplayName == "PixelFox");
            SetControlledSeats(core, witness);
            WritePlay(evidence, "fixture", "Stream 1: controlled held visit, NightOwl occupies one real seat; PixelFox absent. Remaining viewers stay aggregated. Natural attendance is separately unit-tested.");
            int sentimentBefore = core.Community.State(witness.Id).Sentiment;
            long sequence = 900000;
            OfferPlaySpeech(++sequence, "спасибо NightOwl", "ru");
            yield return PlayModeWait.Frames(1);
            Assert.That(core.Community.State(witness.Id).Sentiment, Is.GreaterThan(sentimentBefore));
            OfferPlaySpeech(++sequence, "Я впервые проиграл финал турнира", "ru");
            yield return PlayModeWait.Frames(1);
            ViewerMemory memory = core.Community.State(witness.Id).Memories.Summary.Single(m => m.CanonicalSubject == "final");
            Assert.That(core.Community.State(absent.Id).Memories.Summary, Is.Empty);
            WritePlay(evidence, "firsthand", JsonUtility.ToJson(core.Community.Capture()));
            yield return PlayModeWait.Frames(10);
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CaptureCommunity("viewer-community-ru-low");
            _localization.SetLanguage(GameLanguage.English);
            yield return CaptureCommunity("viewer-community-en-low");
            // Exercise a real paid outcome before saving; zero/zero balance comparisons prove nothing.
            core.Director.ModelEnabled = false;
            for (int step = 0; step < 1200 && receipts == 0; step++)
            {
                stream.Tick(5, StreamSessionTests.PrimeTime);
                yield return PlayModeWait.Frames(1);
            }
            Assert.That(receipts, Is.GreaterThan(0), "Persistence evidence requires a real accepted simulation receipt");
            string receiptSaved = _runtime.State.Donation.History.Last().Id;
            yield return StopBroadcast(view);

            var save = One<GameSaveController>();
            var data = (GameSaveData)Method(save, "Capture").Invoke(save, null);
            string saved = JsonUtility.ToJson(data);
            int sentimentSaved = core.Community.State(witness.Id).Sentiment;
            long balanceSaved = wallet.BalanceCents;
            long donationSaved = _runtime.State.Donation.TotalCents;
            int receiptsSaved = receipts;
            for (int load = 0; load < 2; load++)
            {
                var loaded = JsonUtility.FromJson<GameSaveData>(saved);
                object[] validation = { loaded, null, null };
                Assert.That((bool)Method(save, "ValidateSaveData").Invoke(save, validation), Is.True);
                Method(save, "Apply").Invoke(save, new[] { loaded, validation[1], validation[2] });
                yield return PlayModeWait.Frames(3);
                Assert.That(core.Community.State(witness.Id).Sentiment, Is.EqualTo(sentimentSaved));
                Assert.That(core.Community.State(witness.Id).Memories.Summary.Count(m => m.MemoryId == memory.MemoryId), Is.EqualTo(1));
                Assert.That(core.Community.State(absent.Id).Memories.Summary, Is.Empty);
                Assert.That(wallet.BalanceCents, Is.EqualTo(balanceSaved));
                Assert.That(_runtime.State.Donation.TotalCents, Is.EqualTo(donationSaved));
                Assert.That(_runtime.State.Donation.History.Any(r => r.Id == receiptSaved), Is.True);
                Assert.That(receipts, Is.EqualTo(receiptsSaved), "Applying a save must not publish payouts again");
                Assert.That(core.Roster.Named, Is.Empty);
                Assert.That(core.Director.QueueDepth, Is.Zero);
            }
            WritePlay(evidence, "save-load-twice", JsonUtility.ToJson(core.Community.Capture()));
            clock.AdvanceMinutes(121);
            // High audience is an explicit visual/performance fixture using the unchanged simulation equations.
            // Clone the configuration; never mutate a project asset or set CurrentViewers directly.
            var tuningField = typeof(StreamSession).GetField("_tuning", BindingFlags.Instance | BindingFlags.NonPublic);
            var high = JsonUtility.FromJson<AudienceTuning>(JsonUtility.ToJson(tuningField.GetValue(stream)));
            high.DiscoveryViewers = 55;
            tuningField.SetValue(stream, high);
            yield return FaceCase(); yield return Press(Key.F);
            yield return PlayModeWait.Until(() => _session.Session.Power == PcPowerState.Running, "loaded PC to boot");
            yield return FaceMonitor(); yield return Press(Key.F); yield return SitAndFocus();
            yield return OpenApp(DesktopAppId.Streamly);
            Type(Field<TMP_InputField>(view, "channelCode"), _runtime.State.Trich.ChannelCode);
            Click(Field<Button>(view, "connect"));
            Click(Field<Button>(view, "startStop"));
            yield return PlayModeWait.Until(() => stream.State == StreamState.Live, "later broadcast");
            core.Director.ModelEnabled = false;
            for (int step = 0; step < 40 && stream.Audience.CurrentViewers < 15; step++)
            {
                stream.Tick(10, StreamSessionTests.PrimeTime);
                yield return PlayModeWait.Frames(1);
            }
            Assert.That(stream.Audience.CurrentViewers, Is.GreaterThanOrEqualTo(15));
            SetControlledSeats(core, witness, absent);
            core.Director.CancelAll("acceptance scene boundary");
            core.Director.ModelEnabled = true;
            var crossEvents = new HashSet<string>();
            for (int attempt = 0; attempt < 4; attempt++)
            {
                // Ask each listener separately: the ordinary global budget may legitimately admit only
                // the first name of a multi-name question. Do not bypass that budget in acceptance.
                string target = attempt % 2 == 0 ? witness.DisplayName : absent.DisplayName;
                OfferPlaySpeech(++sequence, target + ", как лучше подготовиться к финалу турнира?", "ru");
                crossEvents.Add(core.Events.LatestSpeech.Key);
                for (int second = 0; second < 10; second++) { stream.Tick(1, StreamSessionTests.PrimeTime); yield return PlayModeWait.Frames(1); }
                yield return PlayModeWait.Until(() => core.Director.QueueDepth == 0, "cross-stream requests to finish", 30);
                if (model.Completed(witness.Id, crossEvents).Length > 0 && model.Completed(absent.Id, crossEvents).Length > 0 &&
                    published.Any(p => p.line.ViewerId == witness.Id && crossEvents.Contains(p.intent.Event.Key) && p.line.Source == ReactionSource.LanguageModel)) break;
            }
            PlayGeneration[] witnessRequests = model.Completed(witness.Id, crossEvents);
            PlayGeneration[] absentRequests = model.Completed(absent.Id, crossEvents);
            Assert.That(witnessRequests, Is.Not.Empty, "Actual director generation must target the witness for this speech");
            Assert.That(absentRequests, Is.Not.Empty, "Actual director generation must target the returning non-witness");
            Assert.That(witnessRequests.Any(r => r.user.Contains("MEMORY: ReportedFailure; subject=final")), Is.True);
            Assert.That(absentRequests.All(r => !r.user.Contains("MEMORY:")), Is.True);
            Assert.That(published.Any(p => p.line.ViewerId == witness.Id && crossEvents.Contains(p.intent.Event.Key) && p.line.Source == ReactionSource.LanguageModel), Is.True);
            WritePlay(evidence, "cross-stream", "Real correlated director requests: witness received the reported final failure; returning absent viewer received no MEMORY. All raw results retained; historical callback optional.");

            // The exact Reaction Monitor switch; domain simulation, rewards and witnessed facts continue.
            core.Director.ModelEnabled = false;
            long fallbackBefore = core.Director.Stats.ShownFromFallback;
            long donationBefore = _runtime.State.Donation.TotalCents;
            long walletBefore = wallet.BalanceCents;
            int followBefore = stream.Audience.Follows;
            int subBefore = stream.Audience.Subscriptions;
            int relationshipBefore = core.Community.State(witness.Id).Sentiment;
            bool donationCaptured = false;
            for (int step = 0; step < 120 && (step < 12 || !donationCaptured || stream.Audience.Subscriptions == subBefore || stream.Audience.Follows == followBefore || _runtime.State.Donation.TotalCents == donationBefore); step++)
            {
                stream.Tick(30, StreamSessionTests.PrimeTime);
                yield return PlayModeWait.Frames(1);
                SetControlledSeats(core, witness, absent);
                if (step == 3) OfferPlaySpeech(++sequence, "спасибо NightOwl", "ru");
                if (step == 6) OfferPlaySpeech(++sequence, "Я наконец победил босса впервые", "ru");
                // Keep pending jobs below their expiry while allowing normal human typing delays.
                for (int second = 0; second < 12; second++) { stream.Tick(1, StreamSessionTests.PrimeTime); yield return PlayModeWait.Frames(1); }
                if (!donationCaptured && core.Chat.Messages.Skip(Math.Max(0, core.Chat.Messages.Count - 4)).Any(m => m.DonationCents > 0))
                {
                    _localization.SetLanguage(GameLanguage.Russian);
                    yield return CaptureCommunity("viewer-community-ru-donation");
                    _localization.SetLanguage(GameLanguage.English);
                    yield return CaptureCommunity("viewer-community-en-donation");
                    donationCaptured = true;
                }
            }
            Assert.That(stream.State, Is.EqualTo(StreamState.Live));
            Assert.That(core.Director.Stats.ShownFromFallback, Is.GreaterThan(fallbackBefore));
            Assert.That(core.Community.State(witness.Id).Sentiment, Is.GreaterThan(relationshipBefore));
            Assert.That(core.Community.State(witness.Id).Memories.Summary.Any(m => m.CanonicalSubject == "boss"), Is.True);
            Assert.That(wallet.BalanceCents - walletBefore, Is.EqualTo(_runtime.State.Donation.TotalCents - donationBefore));
            Assert.That(_runtime.State.Donation.TotalCents, Is.GreaterThan(donationBefore));
            Assert.That(stream.Audience.Follows, Is.GreaterThan(followBefore));
            Assert.That(stream.Audience.Subscriptions, Is.GreaterThan(subBefore));
            Assert.That(donationCaptured, Is.True, "Readability evidence must contain an actual accepted support line");
            WritePlay(evidence, "outage", $"viewers={stream.Audience.CurrentViewers}; fallback={core.Director.Stats.ShownFromFallback - fallbackBefore}; cents={_runtime.State.Donation.TotalCents - donationBefore}; follows={stream.Audience.Follows - followBefore}; subscriptions={stream.Audience.Subscriptions - subBefore}");
            // Recover after pending outage work naturally expires; old events cannot be replayed.
            stream.Tick(20, StreamSessionTests.PrimeTime); yield return PlayModeWait.Frames(1);
            core.Director.ModelEnabled = true;
            OfferPlaySpeech(++sequence, "NightOwl, какой путь выбрать в этой игре?", "ru");
            string recoveryEvent = core.Events.LatestSpeech.Key;
            yield return PlayModeWait.Until(() => published.Any(p => p.intent.Event.Key == recoveryEvent && p.line.Source == ReactionSource.LanguageModel), "correlated local model recovery", 30);
            Assert.That(wallet.BalanceCents - walletBefore, Is.EqualTo(_runtime.State.Donation.TotalCents - donationBefore));
            _localization.SetLanguage(GameLanguage.Russian);
            yield return CaptureCommunity("viewer-community-ru-high");
            ViewerProfile english = core.Community.Profiles.Single(p => p.DisplayName == "ArcadeKid");
            SetControlledSeats(core, witness, absent, english);
            // Establish the recognized channel language with filler, which should not spend the chat budget.
            for (int i = 0; i < 5; i++) OfferPlaySpeech(++sequence, "okay uh", "en");
            var englishEvents = new HashSet<string>();
            for (int attempt = 0; attempt < 4; attempt++)
            {
                for (int second = 0; second < 15; second++) { stream.Tick(1, StreamSessionTests.PrimeTime); yield return PlayModeWait.Frames(1); }
                OfferPlaySpeech(++sequence, "ArcadeKid, which game would you choose next?", "en");
                englishEvents.Add(core.Events.LatestSpeech.Key);
                for (int second = 0; second < 10; second++) { stream.Tick(1, StreamSessionTests.PrimeTime); yield return PlayModeWait.Frames(1); }
                yield return PlayModeWait.Until(() => core.Director.QueueDepth == 0, "English requests to finish", 30);
                if (published.Any(p => p.line.ViewerId == english.Id && englishEvents.Contains(p.intent.Event.Key) && p.line.Source == ReactionSource.LanguageModel)) break;
            }
            Assert.That(published.Any(p => p.line.ViewerId == english.Id && englishEvents.Contains(p.intent.Event.Key) && p.line.Source == ReactionSource.LanguageModel), Is.True, "A correlated English model reaction must be visible");
            _localization.SetLanguage(GameLanguage.English);
            yield return CaptureCommunity("viewer-community-en-high");

            // Record the ordinary runtime markers, not a second Tick or a replacement update loop.
            // Scene frame times include the whole Unity Editor and should not be interpreted as player-build costs.
            recording = false;
            model.Recording = false;
            core.Director.CancelAll("uninstrumented performance boundary");
            model.Drain();
            modelField.SetValue(core.Director, originalModel);
            using var viewerTiming = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "GO! LIVE Viewer Tick", 600);
            using var viewerAllocated = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Scripts, "GO! LIVE Viewer Allocations", 600);
            using var frameGc = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC Allocated In Frame", 600);
            var tickMs = new List<double>(); var viewerAlloc = new List<double>(); var frameAlloc = new List<double>(); var frameMs = new List<double>();
            var performanceTimer = Stopwatch.StartNew();
            double nextSpeechAt = 0;
            for (int sample = 0; sample < 600 || performanceTimer.Elapsed.TotalSeconds < 30; sample++)
            {
                if (performanceTimer.Elapsed.TotalSeconds >= nextSpeechAt)
                { OfferPlaySpeech(++sequence, "NightOwl, какой путь выбрать в этой игре?", "ru"); nextSpeechAt += 7.5; }
                yield return PlayModeWait.Frames(1);
                if (viewerTiming.Valid) tickMs.Add(viewerTiming.LastValue / 1000000d);
                if (viewerAllocated.Valid) viewerAlloc.Add(viewerAllocated.LastValue);
                if (frameGc.Valid) frameAlloc.Add(frameGc.LastValue);
                frameMs.Add(Time.unscaledDeltaTime * 1000);
            }
            Assert.That(viewerTiming.Valid, Is.True, "ordinary runtime viewer marker must be available for the performance audit");
            Assert.That(viewerAllocated.Valid, Is.True, "ordinary runtime viewer allocation counter must be available");
            Assert.That(viewerAlloc.Max(), Is.GreaterThan(0), "The active live-chat session must expose actual native GC allocation samples, not an unsupported managed API returning zero.");
            recording = true;
            model.Recording = true;
            modelField.SetValue(core.Director, model);
            WritePlay(evidence, "performance-method", "30 real seconds (at least 600 ordinary live frames) with the original adapter and recognized question every 7.5 seconds; scene audit instrumentation disabled, so performance-period HTTP bodies are not recorded. The separate 140-cell quality audit retains every raw request/result. Whole-editor frame figures are not player-build costs.");
            WritePlay(evidence, "performance", $"samples={tickMs.Count}; viewer Tick p50={Quantile(tickMs,.5):F4} p95={Quantile(tickMs,.95):F4} max={tickMs.Max():F4} ms; viewer Tick allocation COUNT mean={viewerAlloc.Average():F1} p95={Quantile(viewerAlloc,.95):F1} max={viewerAlloc.Max():F0}; entire-editor-frame GC bytes mean={(frameAlloc.Count>0?frameAlloc.Average():-1):F1} max={(frameAlloc.Count>0?frameAlloc.Max():-1)}; editor frame p50={Quantile(frameMs,.5):F3} p95={Quantile(frameMs,.95):F3} max={frameMs.Max():F3} ms; queueMax={core.Director.Stats.MaximumQueueDepth}; stale={core.Director.Stats.DroppedStale}; permanent={core.Community.Profiles.Count}; memoryTotal={core.Community.Profiles.Sum(p=>core.Community.State(p.Id).Memories.Summary.Count)}");
            WritePlay(evidence, "final-community", JsonUtility.ToJson(core.Community.Capture()));
            double[] latencies = model.LatencySnapshot();
            WritePlay(evidence, "model-latency", $"count={latencies.Length}; p50={Quantile(latencies,.5):F4}; p90={Quantile(latencies,.9):F4}; p95={Quantile(latencies,.95):F4}; maxPromptChars={model.MaximumPromptCharacters}");
            WritePlay(evidence, "director-latency", $"Includes uninstrumented performance interval; count={core.Director.Stats.Latencies.Count}; p50={core.Director.Stats.Percentile(.5):F4}; p90={core.Director.Stats.Percentile(.9):F4}; p95={core.Director.Stats.Percentile(.95):F4}");
            yield return StopBroadcast(view);
            WritePlay(evidence, "complete", "B/C and controlled cross-stream completed. Human microphone session A remains separate.");
            }
            finally
            {
                core.Log.Added -= onTrace;
                core.Director.Shown -= onShown;
                _runtime.State.Donation.Received -= onReceipt;
                core.Director.CancelAll("acceptance finished");
                model.Drain();
                modelField.SetValue(core.Director, originalModel);
            }
        }

        private void OfferPlaySpeech(long id, string text, string language) =>
            _runtime.State.SpeechFeed.Offer(new RecognizedSpeech(id, text, SpeechClock.Now, .99f, language));
        private IEnumerator CaptureCommunity(string name)
        {
            yield return CaptureApp(name, DesktopAppId.Streamly);
            var overlay = One<StreamOverlayView>();
            foreach (TMP_Text label in Field<GameObject>(overlay, "panel").GetComponentsInChildren<TMP_Text>(false))
            {
                if (string.IsNullOrWhiteSpace(label.text)) continue;
                label.ForceMeshUpdate();
                Assert.That(label.isTextTruncated, Is.False, name + ": overlay " + label.name + " text=" + label.text);
            }
        }
        private static void SetControlledSeats(ViewerCore core, params ViewerProfile[] profiles)
        {
            // Controlled cross-stream knowledge experiment: hold only these visits for the test interval.
            // Natural schedule/leave/return probability is independently exercised by the ordinary domain suite.
            var plans = (IEnumerable)typeof(ViewerCommunity).GetField("_attendance", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(core.Community);
            foreach (object plan in plans)
            {
                Type type = plan.GetType();
                var profile = (ViewerProfile)type.GetField("Profile").GetValue(plan);
                type.GetField("Visited").SetValue(plan, true);
                type.GetField("LeavesAt").SetValue(plan, profiles.Any(p => p.Id == profile.Id) ? double.MaxValue : 0d);
            }
            foreach (var participant in core.Roster.Named.ToArray())
                if (!profiles.Any(p => p.Id == participant.ViewerId)) core.Roster.Leave(participant.ViewerId);
            foreach (var profile in profiles)
                if (!core.Roster.IsWatching(profile.Id)) Assert.That(core.Roster.Join(profile.Participant(), core.Events.Now), Is.True);
            Assert.That(core.Roster.Named.Count, Is.LessThanOrEqualTo(core.Roster.AudienceSize));
        }
        private static double Quantile(IEnumerable<double> source, double p)
        { double[] sorted = source.OrderBy(x=>x).ToArray(); return sorted.Length==0?0:sorted[(int)Math.Ceiling(p*(sorted.Length-1))]; }
        private static void WritePlay(PlaySink writer, string kind, string detail) => writer.Write(kind, detail);
        private sealed class PlaySink : IDisposable
        {
            private readonly object _gate = new();
            private StreamWriter _writer;
            public PlaySink(string path) => _writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
            public void Write(string kind, string detail)
            { lock (_gate) _writer?.WriteLine(JsonUtility.ToJson(new PlayRecord { kind = kind, detail = detail })); }
            public void Dispose() { lock (_gate) { _writer?.Dispose(); _writer = null; } }
        }
        [Serializable] private sealed class PlayRecord { public string kind, detail; }
        [Serializable] private sealed class PlayTrace
        {
            public string eventKey, eventKind, viewer, viewerId, speech, outcome, reason, text, source;
            public long intent; public double seconds, latency; public int promptChars;
            public PlayTrace(ReactionLogEntry e) {eventKey=e.EventKey;eventKind=e.EventKind.ToString();viewer=e.ViewerName;viewerId=e.ViewerId;speech=e.Speech;
                outcome=e.Outcome.ToString();reason=e.Reason;text=e.Text;source=e.Source.ToString();intent=e.IntentId;seconds=e.StreamSeconds;latency=e.LatencySeconds;promptChars=e.PromptCharacters;}
        }
        [Serializable] private sealed class PlayGeneration
        {
            public long intent;
            public string eventKey, viewerId, system, user, raw, status;
            public double latency;
            public bool completed;
        }
        private sealed class PlayModelRecorder : IViewerLanguageModel
        {
            private readonly IViewerLanguageModel _inner; private readonly PlaySink _writer;
            private readonly ChatDirector _director;
            private readonly object _gate = new();
            private readonly List<PlayGeneration> _generations = new();
            private readonly List<Task<LanguageModelResult>> _pending = new();
            private readonly List<double> _latencies = new();
            public volatile bool Recording = true;
            public int MaximumPromptCharacters {get;private set;}
            public PlayModelRecorder(IViewerLanguageModel inner, PlaySink writer, ChatDirector director) { _inner=inner;_writer=writer;_director=director; }
            public PlayGeneration[] Completed(string viewerId, HashSet<string> events)
            { lock (_gate) return _generations.Where(r => r.completed && r.viewerId == viewerId && events.Contains(r.eventKey)).ToArray(); }
            public double[] LatencySnapshot() { lock (_gate) return _latencies.ToArray(); }
            public void Drain() { Task.WaitAll(_pending.ToArray()); }
            public Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation)
            {
                // The director sets Situation/Cancellation immediately before invoking the model, then Task.
                // This read-only test hook binds the actual HTTP request to that exact ordinary generation.
                var jobs = (IEnumerable)typeof(ChatDirector).GetField("_jobs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_director);
                object starting = jobs.Cast<object>().Single(j => {
                    Type t = j.GetType();
                    return t.GetField("Task").GetValue(j) == null && t.GetField("Cancellation").GetValue(j) != null;
                });
                var intent = (ReactionIntent)starting.GetType().GetField("Intent").GetValue(starting);
                var row = new PlayGeneration { intent = intent.Id, eventKey = intent.Event.Key, viewerId = intent.Viewer.ViewerId,
                    system = request.System, user = request.User };
                lock (_gate) _generations.Add(row);
                MaximumPromptCharacters=Math.Max(MaximumPromptCharacters,request.System.Length+request.User.Length);
                if (Recording) WritePlay(_writer,"model-request",JsonUtility.ToJson(row));
                Task<LanguageModelResult> task = GenerateCoreAsync(row, request, cancellation);
                _pending.Add(task);
                return task;
            }
            private async Task<LanguageModelResult> GenerateCoreAsync(PlayGeneration row, ViewerChatRequest request, CancellationToken cancellation)
            {
                LanguageModelResult result=await _inner.GenerateAsync(request,cancellation).ConfigureAwait(false);
                lock (_gate)
                {
                    row.completed = true; row.raw = result.Text; row.status = result.Status.ToString(); row.latency = result.LatencySeconds;
                    _latencies.Add(result.LatencySeconds);
                }
                if (Recording) WritePlay(_writer,"model-result",JsonUtility.ToJson(row));
                return result;
            }
            public void Dispose() => _inner.Dispose();
        }
    }
}
