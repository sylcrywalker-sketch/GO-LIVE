using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Desktop;
using GoLive.Viewers;
using GoLive.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GoLive.Tests
{
    // Explicit evidence collection, not a replacement for the microphone/human quality gate. Fixed selector seeds;
    // no retry until a preferred viewer or answer appears. The actual configured model and settings stay unchanged.
    [Category("LocalModel")]
    public sealed class ViewerRealPlay2ReplayTests
    {
        private const string Broadcast = "1ae995fef77540aa8acef520fd3498dc";
        private const string SourceName = "session-20260926-212336.jsonl";
        private const string Kritik = "viewer.kritik228", Zina = "viewer.zinaivanovna";
        private static readonly int[] MainSequence = { 6, 7, 8, 9, 10, 11, 12 };

        [Serializable]
        private sealed class SourceRow
        {
            public string type, text, viewerId, viewerName, language;
            public double realSeconds, streamSeconds, confidence, recognitionSeconds, endToTextSeconds, audioSeconds;
            public long sequence, intentId;
            public int sourceLine;
        }

        [Serializable]
        private sealed class Row
        {
            public string record, mode, episode, sourcePath, sourceSha256, notes, modelSettings, reactionTuning;
            public ulong seed;
            public SourceRow source;
            public string targetKind, targetViewer, reason, viewerId, viewerName, profile, candidates;
            public long targetEpoch, presenceEpoch, intentId;
            public string[] selected, directAnswerFacts;
            public string purpose, plannedIntent, previousViewerLine, dayId, dayDescription, dayProvenance;
            public string systemPrompt, userPrompt, status, rawModelText, detail, validationReason;
            public string outcome, publishedText, publishedSource;
            public bool accepted;
            public int maximumTokens, promptTokens, completionTokens;
            public double streamSeconds, sourceToPublicationSeconds, latencySeconds, queueSeconds, dueSeconds, expiresSeconds;
        }

        [Test, Explicit("Requires the unchanged configured local model; writes every attempt outside the repository")]
        public void ReplayRecordedSpeechAndCollectEveryModelOutcome()
        {
            var config = AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>("Assets/Game/Config/Viewers/ViewerCore.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.ValidationError, Is.Null);
            Assert.That(config.Model.Enabled, Is.True, "Start the configured model without changing its settings.");
            string output = Environment.GetEnvironmentVariable("GO_LIVE_REALPLAY2_OUTPUT");
            Assert.That(output, Is.Not.Null.And.Not.Empty, "Set GO_LIVE_REALPLAY2_OUTPUT to an external evidence directory.");
            output = Path.GetFullPath(output);
            string repo = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That((output.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).StartsWith(repo, StringComparison.OrdinalIgnoreCase),
                Is.False, "Replay evidence must stay outside the repository.");
            Directory.CreateDirectory(output);
            string path = Path.Combine(output, "realplay2-replay-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".jsonl");
            string sourcePath = Path.Combine(repo, "Logs", "ViewerSessions", SourceName);
            SourceRow[] source = File.ReadAllLines(sourcePath).Select((line, index) =>
            {
                var row = JsonUtility.FromJson<SourceRow>(line); row.sourceLine = index + 1; return row;
            }).ToArray();
            var speech = source.Where(r => r.type == "speech").ToDictionary(r => (int)r.sequence);
            Assert.That(speech[7].text, Is.EqualTo("Почему без настроения?"));
            Assert.That(speech[11].text, Is.EqualTo("Что? Девочка с девятого этажа? О чем ты критика?"));
            string hash;
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(sourcePath)) hash = BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
            using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            var writeGate = new object();
            void Write(Row row) { lock (writeGate) writer.WriteLine(JsonUtility.ToJson(row)); }
            var violations = new List<string>();
            int modelOk = 0;
            Write(new Row
            {
                record = "manifest", sourcePath = sourcePath, sourceSha256 = hash,
                modelSettings = JsonUtility.ToJson(config.Model), reactionTuning = JsonUtility.ToJson(config.Reactions),
                notes = "Partial-state controlled replay. Seeds 1,2,3 are fixed chosen selector/fallback seeds; original audience seed, PRNG state, game day/minutes and rhythm are absent. " +
                    "Audience is controlled at 3: authored Zina and kritik plus a reconstructed anonymous seat generated by production code. Named join epoch 1 is recorded in source. " +
                    "Events use controlled gameMinutes=0 only for witness stamping; ChatSituation game time stays unknown. krit.ranked is logged at source intent2; zina.market is logged later at intent13 and carried into earlier fixtures as an explicit assumption. " +
                    "Recorded fixtures restore prior visible source lines and conversation owner, using publication time as the unknown rhythm due-time approximation. Fresh episodes inject no recorded answers. " +
                    "Sequence35 is a separate fresh episode; the omitted long gap and changed original audience are not reconstructed. Idle stream gaps are fast-forwarded, model/typing periods use wall time. " +
                    "All parsed adapter responses including rejected text are recorded; the production adapter does not expose the raw HTTP envelope. No semantic success is inferred from validator acceptance."
            });
            for (ulong seed = 1; seed <= 3; seed++)
            {
                // Independent snapshots prevent a new generated answer from being mislabeled as the original context.
                foreach (int sequence in MainSequence)
                    modelOk += Run(config, seed, "recorded-line-fixture", "source-" + sequence,
                        new[] { speech[sequence] }, source.Where(r => r.type == "chat" && r.streamSeconds < speech[sequence].streamSeconds).ToArray(), Write, violations);
                modelOk += Run(config, seed, "fresh-generation", "main-source-order-6-through-12",
                    MainSequence.Select(s => speech[s]).ToArray(), Array.Empty<SourceRow>(), Write, violations);
                modelOk += Run(config, seed, "fresh-generation", "later-35-separate-episode",
                    new[] { speech[35] }, Array.Empty<SourceRow>(), Write, violations);
            }
            Write(new Row { record = "summary", notes = "Fixed three-seed matrix complete; model OK count=" + modelOk + "; invariant violations=" + violations.Count,
                selected = violations.ToArray() });
            TestContext.WriteLine("REALPLAY2_REPLAY " + path);
            Assert.That(violations, Is.Empty, "Review every recorded invariant failure; no attempt was removed.");
            Assert.That(modelOk, Is.GreaterThan(0), "No successful configured-model response; inspect the saved failure records.");
        }

        private static int Run(ViewerCoreConfig config, ulong seed, string mode, string episode, SourceRow[] speech,
            SourceRow[] history, Action<Row> write, List<string> violations)
        {
            var settings = JsonUtility.FromJson<ChatModelSettings>(JsonUtility.ToJson(config.Model));
            var tuning = JsonUtility.FromJson<ReactionTuning>(JsonUtility.ToJson(config.Reactions));
            var profiles = config.Community.Profiles.Where(p => p.Id == Kritik || p.Id == Zina).ToDictionary(p => p.Id);
            var roster = new AudienceRoster(EphemeralViewers.Create);
            roster.SetAudienceSize(3);
            roster.Join(profiles[Zina].Participant(), 89.0004287599586);
            roster.Join(profiles[Kritik].Participant(), 91.00058015587274);
            var selector = new ReactionSelector(tuning, roster, new AudienceRandom(seed), Tier);
            var chat = new StreamChat();
            var log = new ReactionLog();
            var intents = new Dictionary<long, ReactionIntent>();
            var sources = speech.ToDictionary(s => s.sequence);
            var clock = Stopwatch.StartNew();
            double now = speech[0].streamSeconds;
            Row New(string record, ReactionIntent intent = null)
            {
                var row = new Row { record = record, mode = mode, episode = episode, seed = seed, streamSeconds = now };
                if (intent == null) return row;
                row.source = sources[intent.Event.Speech.Sequence];
                row.intentId = intent.Id; row.viewerId = intent.Viewer.ViewerId; row.viewerName = intent.Viewer.DisplayName;
                row.presenceEpoch = intent.PresenceEpoch; row.targetKind = intent.ConversationTarget.Kind.ToString();
                row.targetViewer = intent.ConversationTarget.ViewerId; row.targetEpoch = intent.ConversationTarget.PresenceEpoch;
                row.previousViewerLine = intent.PreviousViewerLine; row.dueSeconds = intent.DueSeconds; row.expiresSeconds = intent.ExpiresSeconds;
                return row;
            }
            write(New("episode-start"));
            foreach (SourceRow prior in history)
            {
                ChatParticipant viewer = roster.Find(prior.viewerId);
                if (viewer == null) continue; // No invented original anonymous profile or visit.
                var line = chat.Add(Broadcast, viewer.ViewerId, viewer.DisplayName, prior.text, prior.streamSeconds, -prior.intentId, ReactionSource.LanguageModel);
                var origin = (ReactionIntent)typeof(ReactionIntent).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                    .Single(c => c.GetParameters().Length == 8).Invoke(new object[] { -prior.intentId, Said(roster, speech[0]), viewer,
                        true, 0, prior.streamSeconds, prior.streamSeconds + tuning.StaleSeconds, roster.Epoch(viewer.ViewerId) });
                selector.ObservePublished(line, origin);
                selector.Rhythm.Refill(prior.streamSeconds, 3);
                selector.Rhythm.RecordConversation(viewer.ViewerId, prior.streamSeconds);
                var recorded = New("recorded-context-only"); recorded.source = prior;
                recorded.viewerId = viewer.ViewerId; recorded.publishedText = prior.text;
                recorded.notes = "Original source line injected only as snapshot precondition; NOT generated by this run. Thread turns reset to one; original count was unlogged.";
                write(recorded);
            }

            var recorder = new RecordingModel(settings, write);
            using var director = new ChatDirector(recorder, settings, chat, log, () => clock.Elapsed.TotalSeconds);
            director.BeginBroadcast(new AudienceRandom(seed));
            director.Shown += (message, intent) =>
            {
                selector.ObservePublished(message, intent);
                var row = New("publication", intent); row.publishedText = message.Text; row.publishedSource = message.Source.ToString();
                row.streamSeconds = message.StreamSeconds; row.sourceToPublicationSeconds = message.StreamSeconds - intent.Event.StreamSeconds;
                write(row);
            };
            director.Finished += (intent, situation, text) => typeof(ReactionSelector)
                .GetMethod("FinishConversation", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(selector, new object[] { intent });
            log.Added += entry =>
            {
                intents.TryGetValue(entry.IntentId, out ReactionIntent intent);
                var row = New("director-outcome", intent); row.outcome = entry.Outcome.ToString(); row.reason = entry.Reason;
                row.latencySeconds = entry.LatencySeconds; row.queueSeconds = entry.QueueSeconds; row.publishedSource = entry.Source.ToString();
                row.publishedText = entry.Text; write(row);
            };
            ChatSituation Context(ReactionIntent intent)
            {
                RelationshipTier tier = Tier(intent.Viewer.ViewerId);
                var situation = new ChatSituation("stream", now, 3, ViewerLanguage.Russian, chat.Messages.ToArray(), intent.Event.Speech.Text,
                    ViewerUtterancePlanner.Describe(tier, 1, 0), tier: tier, gameMinutes: double.NaN);
                bool usesDay = (bool)typeof(ViewerUtterancePlanner).GetMethod("UsesDailyContext", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { intent });
                ViewerDailyState day = usesDay ? RecoveredDay(intent.Viewer, tier) : null;
                if (day != null) typeof(ChatSituation).GetProperty("Day").SetValue(situation, day);
                ViewerUtterancePlan plan = ViewerUtterancePlanner.For(intent, situation);
                var row = New("plan", intent);
                row.profile = intent.Viewer.Persona.Profile == null ? "Reconstructed production anonymous persona; see exact prompt" : JsonUtility.ToJson(intent.Viewer.Persona.Profile);
                row.purpose = plan.QuestionPurpose.ToString(); row.plannedIntent = plan.Intent.ToString();
                row.previousViewerLine = plan.PreviousViewerLine;
                row.directAnswerFacts = plan.DirectAnswerFacts.Select(f => f.Source + ": " + f.Text).ToArray();
                row.dayId = day?.Activity.Id; row.dayDescription = day?.Describe();
                row.dayProvenance = intent.Viewer.ViewerId == Kritik ? "Recorded activity krit.ranked, source intent2" :
                    intent.Viewer.ViewerId == Zina ? "Recorded later activity zina.market, source intent13; earlier reuse is controlled assumption" :
                    "Reconstructed anonymous identity/activity, not original session state";
                recorder.Next = row; recorder.Intent = intent; recorder.Situation = situation;
                write(row);
                return situation;
            }

            int index = 0;
            double lastWall = clock.Elapsed.TotalSeconds;
            while ((index < speech.Length || director.QueueDepth > 0) && clock.Elapsed.TotalSeconds < 120)
            {
                double wall = clock.Elapsed.TotalSeconds;
                now += wall - lastWall; lastWall = wall;
                if (director.QueueDepth == 0 && index < speech.Length && now < speech[index].streamSeconds) now = speech[index].streamSeconds;
                while (index < speech.Length && speech[index].streamSeconds <= now)
                {
                    SourceRow source = speech[index++];
                    List<ReactionIntent> selected = selector.Select(Said(roster, source), now, true, out string reason);
                    ConversationTarget target = selector.LastTarget;
                    var row = New("selection"); row.source = source; row.reason = reason;
                    row.targetKind = target.Kind.ToString(); row.targetViewer = target.ViewerId; row.targetEpoch = target.PresenceEpoch;
                    row.selected = selected.Select(i => i.Viewer.ViewerId).ToArray(); row.candidates = selector.LastCandidateIds;
                    write(row);
                    if (target.IsSpecific && selected.Any(i => i.Viewer.ViewerId != target.ViewerId))
                        violations.Add($"{mode}/{episode}/seed{seed}/speech{source.sequence}: primary target stolen");
                    if (selected.Select(i => i.Viewer.ViewerId).Distinct().Count() != selected.Count)
                        violations.Add($"{mode}/{episode}/seed{seed}/speech{source.sequence}: duplicate responder");
                    if (target.Kind == ConversationTargetKind.Group && selected.Zip(selected.Skip(1), (a, b) => b.DueSeconds <= a.DueSeconds).Any(x => x))
                        violations.Add($"{mode}/{episode}/seed{seed}/speech{source.sequence}: non-staggered group");
                    if (mode == "recorded-line-fixture" && source.sequence == 7 && (target.ViewerId != Kritik || target.Kind != ConversationTargetKind.ActiveThreadViewer))
                        violations.Add($"seed{seed}: recorded mood follow-up did not resolve to kritik");
                    if (source.sequence == 11 && (target.ViewerId != Kritik || target.Kind != ConversationTargetKind.SpecificViewer))
                        violations.Add($"{mode}/seed{seed}: recorded критика name did not resolve to kritik");
                    if (mode == "recorded-line-fixture" && source.sequence == 12 && target.ViewerId != Zina)
                        violations.Add($"seed{seed}: recorded explanation follow-up did not retain Zina, who wrote the original prior line");
                    foreach (ReactionIntent intent in selected)
                    {
                        intents[intent.Id] = intent;
                        write(New("scheduled", intent));
                        director.Submit(intent);
                    }
                }
                director.Update(now, roster, Context, Broadcast);
                if (director.QueueDepth > 0) Thread.Sleep(2);
            }
            if (index < speech.Length || director.QueueDepth > 0)
            {
                violations.Add($"{mode}/{episode}/seed{seed}: replay wall timeout");
                director.CancelAll("replay wall timeout");
            }
            recorder.Drain();
            write(New("episode-end"));
            return recorder.Ok;
        }

        private static RelationshipTier Tier(string id) => id == Kritik ? RelationshipTier.Wary : id == Zina ? RelationshipTier.Friendly : RelationshipTier.Neutral;

        private static StreamEvent Said(AudienceRoster roster, SourceRow source) => StreamEvent.StreamerSpeech(source.sequence, Broadcast, source.streamSeconds,
            SpeechRelevance.Analyze(new RecognizedSpeech(source.sequence, source.text, 0, (float)source.confidence, source.language), roster.NamesForMentions()))
            .WithWitnesses(roster, 0); // Deliberately controlled stamp; original absolute game time was not captured.

        private static ViewerDailyState RecoveredDay(ChatParticipant viewer, RelationshipTier tier)
        {
            string id = viewer.ViewerId == Kritik ? "krit.ranked" : viewer.ViewerId == Zina ? "zina.market" : null;
            if (id == null) return ViewerDailyLife.For(viewer, double.NaN, Broadcast, tier);
            DailyActivity activity = viewer.Persona.Profile.DailyLife.Single(a => a.Id == id);
            return (ViewerDailyState)Activator.CreateInstance(typeof(ViewerDailyState), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { activity, activity.Mood, activity.Energy, tier == RelationshipTier.Wary ? ConversationOpenness.Reserved : ConversationOpenness.Chatty }, null);
        }

        private sealed class RecordingModel : IViewerLanguageModel
        {
            private readonly OpenAiCompatibleChatModel _inner;
            private readonly Action<Row> _write;
            private readonly List<Task<LanguageModelResult>> _tasks = new();
            public Row Next;
            public ReactionIntent Intent;
            public ChatSituation Situation;
            public int Ok;
            public RecordingModel(ChatModelSettings settings, Action<Row> write) { _inner = new OpenAiCompatibleChatModel(settings); _write = write; }
            public Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation)
            {
                Row row = Next;
                ReactionIntent intent = Intent;
                ChatSituation situation = Situation;
                row.record = "request"; row.systemPrompt = request.System; row.userPrompt = request.User; row.maximumTokens = request.MaximumTokens;
                _write(row);
                Task<LanguageModelResult> task = Record(); _tasks.Add(task); return task;
                async Task<LanguageModelResult> Record()
                {
                    LanguageModelResult result = await _inner.GenerateAsync(request, cancellation).ConfigureAwait(false);
                    if (result.Status == LanguageModelStatus.Ok) Interlocked.Increment(ref Ok);
                    ChatValidation validation = result.Status == LanguageModelStatus.Ok
                        ? ChatOutputValidator.Validate(result.Text, intent, situation.RecentChat, situation) : ChatValidation.Reject(result.Status.ToString());
                    row.record = "model-result"; row.status = result.Status.ToString(); row.rawModelText = result.Text; row.detail = result.Detail;
                    row.latencySeconds = result.LatencySeconds; row.promptTokens = result.PromptTokens; row.completionTokens = result.CompletionTokens;
                    row.accepted = validation.Accepted; row.validationReason = validation.Reason;
                    _write(row); return result;
                }
            }
            public void Drain() => Task.WhenAll(_tasks).GetAwaiter().GetResult();
            public void Dispose() { Drain(); _inner.Dispose(); }
        }
    }
}
