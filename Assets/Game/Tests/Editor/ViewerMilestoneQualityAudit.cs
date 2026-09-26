using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using GoLive.Desktop;
using GoLive.PcBuilding;
using GoLive.Viewers;
using GoLive.Voice;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GoLive.Tests
{
    // Conditional style audit, not natural selection/presence evidence. Every cell invokes the real adapter once;
    // the ordinary director decides model/fallback/silence. The separate Play Mode journey checks the full game.
    public sealed class ViewerMilestoneQualityAudit
    {
        [Test, Explicit("Requires the configured local model; retains every raw generation"), Timeout(1200000)]
        public void AuditAllProfilesAcrossIntegratedContexts()
        {
            var config = AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>("Assets/Game/Config/Viewers/ViewerCore.asset");
            Assert.That(config.ValidationError, Is.Null);
            string directory = Environment.GetEnvironmentVariable("GO_LIVE_VISUAL_OUTPUT");
            Assert.That(directory, Is.Not.Null.And.Not.Empty);
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "viewer-milestone-raw.jsonl");
            using var writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
            using var model = new Recorder(config.Model, writer);
            int attempts = 0, generated = 0;
            foreach (string context in Contexts)
            foreach (ViewerProfile profile in config.Community.Profiles)
            {
                var row = new Row { record = "attempt", profileId = profile.Id, displayName = profile.DisplayName,
                    context = context, endpoint = config.Model.Endpoint, model = config.Model.Model,
                    modelSettings = JsonUtility.ToJson(config.Model), identity = JsonUtility.ToJson(profile) };
                model.Row = row;
                using Fixture fixture = BuildFixture(profile, context, ++attempts);
                var log = new ReactionLog();
                using var director = new ChatDirector(new BorrowedModel(model), config.Model, fixture.Chat, log);
                director.BeginBroadcast(new AudienceRandom((ulong)attempts));
                ViewerChatRequest request = ChatContextBuilder.Build(fixture.Intent, fixture.Situation, config.Model.MaximumTokens);
                row.systemPrompt = request.System; row.userPrompt = request.User;
                row.maximumTokens = request.MaximumTokens;
                writer.WriteLine(JsonUtility.ToJson(row));
                director.Submit(fixture.Intent);
                var timer = Stopwatch.StartNew();
                do
                {
                    director.Update(600 + timer.Elapsed.TotalSeconds, fixture.Roster, () => fixture.Situation, "audit");
                    if (director.QueueDepth > 0) Thread.Sleep(2);
                } while (director.QueueDepth > 0 && timer.Elapsed.TotalSeconds < 30);
                if (director.QueueDepth > 0) director.CancelAll("audit wall timeout");
                // Cancellation removes a director job before its HTTP continuation necessarily completes.
                // Finish that exact attempt before inspecting its row or advancing the fixed audit matrix.
                model.Drain();
                StreamChatMessage published = fixture.Chat.Messages.LastOrDefault(m => m.IntentId == fixture.Intent.Id);
                row.publishedSource = published?.Source.ToString() ?? "None";
                row.publishedText = published?.Text;
                row.publicationReason = log.Entries.LastOrDefault()?.Reason;
                row.maximumQueueDepth = director.Stats.MaximumQueueDepth;
                // Validation is recorded independently of runtime fallback publication.
                ChatValidation validation = ValidateRaw(row.rawText, fixture);
                row.accepted = validation.Accepted; row.validation = validation.Reason;
                if (row.httpStatus == 200 && !string.IsNullOrWhiteSpace(row.rawCompletion)) generated++;
                row.record = "generation";
                writer.WriteLine(JsonUtility.ToJson(row));
                Assert.That(model.CallsForRow, Is.EqualTo(1), "Each fixed cell must retain one actual attempt, with no regeneration");
            }
            Assert.That(attempts, Is.EqualTo(Contexts.Length * 10));
            Assert.That(generated, Is.GreaterThanOrEqualTo(100), "At least 100 actual raw model generations, not transport failures or fallback attempts");
            TestContext.WriteLine("All raw attempts: " + path + "; semantic review remains required.");
        }

        private static readonly string[] Contexts = {
            "dumb mistake RU", "question RU", "impressive win RU", "ordinary filler RU", "microphone quality", "long silence",
            "question EN", "direct thanks", "cold relationship", "warm relationship", "witness memory callback",
            "absent memory contrast", "known promise fulfilled", "known promise broken"
        };

        // Fixed conditional fixtures create witnessed facts through the actual community/ledger, not diary text.
        private static Fixture BuildFixture(ViewerProfile profile, string context, long serial)
        {
            var roster = new AudienceRoster((random, index) => null);
            var community = new ViewerCommunity(new[] { profile }, roster);
            roster.SetAudienceSize(10);
            community.BeginBroadcast("audit-old-" + serial, (ulong)serial, 100, StreamTopic.Games);
            if (context != "absent memory contrast") roster.Join(profile.Participant(), 0);
            StreamEvent Speech(long id, string broadcast, double at, string text, string language, double gameMinutes) =>
                StreamEvent.StreamerSpeech(id, broadcast, at, SpeechRelevance.Analyze(
                    new RecognizedSpeech(id, text, at, 1f, language), community.KnownNames)).WithWitnesses(roster, gameMinutes);
            if (context == "witness memory callback" || context == "absent memory contrast")
                community.Process(Speech(serial * 10, "audit-old-" + serial, 30, "Я впервые проиграл финал турнира", "ru", 100));
            if (context == "known promise fulfilled" || context == "known promise broken")
            {
                community.Process(Speech(serial * 10, "audit-old-" + serial, 30, "я завтра куплю видеокарту", "ru", 100));
                Assert.That(community.Promises.Summary, Has.Count.EqualTo(1));
                if (context == "known promise fulfilled")
                    community.ObserveGameplay(new PromiseGameplayFact(PromiseAction.Purchase, "gpu", 1500, "audit-order-" + serial, EventWitnesses.Capture(roster)));
                else community.UpdateContext(2881, StreamTopic.Hardware);
                Assert.That(community.Promises.Summary.Single().Status, Is.EqualTo(context == "known promise fulfilled" ? PromiseStatus.Fulfilled : PromiseStatus.Broken));
            }
            community.EndBroadcast();
            var snapshot = community.Capture();
            if (context == "cold relationship") { snapshot.Viewers[0].Sentiment = -50; snapshot.Viewers[0].Acknowledgements = 4; }
            if (context == "warm relationship") { snapshot.Viewers[0].Sentiment = 50; snapshot.Viewers[0].Acknowledgements = 4; }
            community.Restore(snapshot);
            roster.SetAudienceSize(10);
            community.BeginBroadcast("audit-now-" + serial, (ulong)serial + 1, 4000, StreamTopic.Games);
            roster.Join(profile.Participant(), 0);
            string text = context switch {
                "dumb mistake RU" => "Блин, я продал не тот предмет и опять умер в игре самым тупым способом!",
                "question RU" => "Чат, как думаете, какую игру лучше запустить дальше?",
                "impressive win RU" => "Я выиграл этот раунд один против пятерых, вот это жесть!",
                "ordinary filler RU" => "Так, ну, секунду, ага",
                "question EN" => "Chat, which game should I play next?",
                "direct thanks" => "спасибо " + profile.DisplayName,
                "cold relationship" => profile.DisplayName + ", ты тут? Как тебе сегодняшний стрим?",
                "warm relationship" => profile.DisplayName + ", ты тут? Как тебе сегодняшний стрим?",
                "witness memory callback" => "Чат, как лучше подготовиться к финалу турнира?",
                "absent memory contrast" => "Чат, как лучше подготовиться к финалу турнира?",
                "known promise fulfilled" => "Чат, что думаете о видеокарте?",
                "known promise broken" => "Чат, что думаете о видеокарте?",
                _ => null
            };
            StreamEvent current = context == "microphone quality"
                ? StreamEvent.PeripheralChanged(serial, "audit.mic." + serial, 600, PcPeripheralKind.Microphone, false).WithWitnesses(roster, 4000)
                : context == "long silence"
                ? StreamEvent.StreamerSilence(serial, "audit.silence." + serial, 600, 180, SilenceLevel.VeryLong).WithWitnesses(roster, 4000)
                : text != null ? Speech(serial, "audit-now-" + serial, 600, text, context == "question EN" ? "en" : "ru", 4000)
                : throw new ArgumentOutOfRangeException(nameof(context), context, "Unknown fixed audit context");
            bool direct = context == "direct thanks" || context.EndsWith("relationship", StringComparison.Ordinal);
            var intent = (ReactionIntent)typeof(ReactionIntent).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 8).Invoke(new object[] { serial, current, roster.Find(profile.Id), direct, 0, 600d, 620d, roster.Epoch(profile.Id) });
            var memories = community.State(profile.Id).Memories.Retrieve(current, 4000);
            if (context == "witness memory callback") Assert.That(memories, Has.Count.EqualTo(1));
            if (context == "absent memory contrast") Assert.That(memories, Is.Empty);
            ViewerPromiseContext promise = context.StartsWith("known promise", StringComparison.Ordinal)
                ? community.Promises.Retrieve(profile.Id, "gpu", 4000) : null;
            if (context.StartsWith("known promise", StringComparison.Ordinal)) Assert.That(promise, Is.Not.Null);
            var situation = new ChatSituation("audit", 600, 10, context == "question EN" ? ViewerLanguage.English : ViewerLanguage.Russian,
                Array.Empty<StreamChatMessage>(), null, community.RelationshipContext(profile.Id), memories, promise);
            return new Fixture { Roster = roster, Chat = new StreamChat(), Intent = intent, Situation = situation };
        }
        private static ChatValidation ValidateRaw(string text, Fixture fixture) =>
            ChatOutputValidator.Validate(text, fixture.Intent, fixture.Situation.RecentChat, fixture.Situation);

        private sealed class Fixture : IDisposable
        {
            public AudienceRoster Roster;
            public StreamChat Chat;
            public ReactionIntent Intent;
            public ChatSituation Situation;
            public void Dispose() { }
        }

        [Serializable]
        private sealed class Row
        {
            public string record, profileId, displayName, context, identity, endpoint, model, modelSettings;
            public string systemPrompt, userPrompt, requestBody, rawResponse, rawCompletion, rawText, status, detail, validation;
            public string publishedSource, publishedText, publicationReason;
            public int maximumTokens, httpStatus, promptTokens, completionTokens, maximumQueueDepth;
            public double latencySeconds;
            public bool accepted;
        }

        private sealed class Recorder : IViewerLanguageModel
        {
            private readonly OpenAiCompatibleChatModel _inner;
            private readonly StreamWriter _writer;
            private readonly WireCapture _capture;
            private Row _row;
            private Task<LanguageModelResult> _pending;
            public int CallsForRow { get; private set; }
            public Row Row { get => _row; set { _row = value; CallsForRow = 0; _capture.Row = value; } }
            public Recorder(ChatModelSettings settings, StreamWriter writer)
            {
                _writer = writer; _inner = new OpenAiCompatibleChatModel(settings);
                _capture = new WireCapture(writer);
                var field = typeof(OpenAiCompatibleChatModel).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                var previous = (HttpClient)field.GetValue(_inner);
                field.SetValue(_inner, new HttpClient(_capture) { Timeout = System.Threading.Timeout.InfiniteTimeSpan });
                previous.Dispose();
            }
            public Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation)
            {
                CallsForRow++;
                return _pending = GenerateCoreAsync(_row, request, cancellation);
            }
            public void Drain() => _pending?.GetAwaiter().GetResult();
            private async Task<LanguageModelResult> GenerateCoreAsync(Row row, ViewerChatRequest request, CancellationToken cancellation)
            {
                var timer = Stopwatch.StartNew();
                try
                {
                    LanguageModelResult result = await _inner.GenerateAsync(request, cancellation).ConfigureAwait(false);
                    row.status = result.Status.ToString(); row.rawText = result.Text; row.detail = result.Detail;
                    row.latencySeconds = result.LatencySeconds; row.promptTokens = result.PromptTokens; row.completionTokens = result.CompletionTokens;
                    return result;
                }
                catch (Exception error)
                {
                    row.status = "Exception"; row.detail = error.ToString(); row.latencySeconds = timer.Elapsed.TotalSeconds;
                    return new LanguageModelResult(LanguageModelStatus.Unavailable, null, timer.Elapsed.TotalSeconds, detail: error.Message);
                }
                finally { row.record = "raw-result"; lock (_writer) _writer.WriteLine(JsonUtility.ToJson(row)); }
            }
            public void Dispose() { _inner.Dispose(); Drain(); }
        }

        // Each fresh director owns this test-local facade; the enclosing audit owns the shared recorder/HTTP client.
        private sealed class BorrowedModel : IViewerLanguageModel
        {
            private readonly Recorder _recorder;
            public BorrowedModel(Recorder recorder) => _recorder = recorder;
            public Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation) => _recorder.GenerateAsync(request, cancellation);
            public void Dispose() { }
        }

        private sealed class WireCapture : DelegatingHandler
        {
            private readonly StreamWriter _writer;
            public Row Row;
            public WireCapture(StreamWriter writer) : base(new HttpClientHandler { UseProxy = false }) => _writer = writer;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                Row row = Row;
                row.requestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                try
                {
                    HttpResponseMessage response = await base.SendAsync(request, cancellation).ConfigureAwait(false);
                    row.httpStatus = (int)response.StatusCode;
                    row.rawResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    try { row.rawCompletion = JsonUtility.FromJson<WireCompletion>(row.rawResponse)?.choices?.FirstOrDefault()?.message?.content; }
                    catch (ArgumentException) { /* Retain the malformed wire body for review. */ }
                    row.record = "wire-response"; lock (_writer) _writer.WriteLine(JsonUtility.ToJson(row)); return response;
                }
                catch (Exception error) { row.record = "wire-failure"; row.detail = error.ToString(); lock (_writer) _writer.WriteLine(JsonUtility.ToJson(row)); throw; }
            }
        }
#pragma warning disable 0649
        [Serializable] private sealed class WireCompletion { public WireChoice[] choices; }
        [Serializable] private sealed class WireChoice { public WireMessage message; }
        [Serializable] private sealed class WireMessage { public string content; }
#pragma warning restore 0649
    }
}
