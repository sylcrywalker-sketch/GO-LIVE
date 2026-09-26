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
    // Social quality pass (V2): the same fixed 14-context matrix, but each cell goes through the production
    // utterance planner and callback decision exactly as ViewerCore does. Relationship and memory A/B sets hold
    // the viewer and event fixed and vary only the controlled state.
    public sealed class ViewerMilestoneQualityAudit
    {
        [Test, Explicit("Requires the configured local model; retains every raw generation"), Timeout(1200000)]
        public void AuditAllProfilesAcrossIntegratedContexts()
        {
            using var audit = new Audit("viewer-social-quality-raw.jsonl");
            int attempts = 0;
            foreach (string context in Contexts)
            foreach (ViewerProfile profile in audit.Config.Community.Profiles)
            {
                var row = audit.NewRow(profile, context);
                audit.Run(row, BuildFixture(profile, context, ++attempts));
            }
            Assert.That(attempts, Is.EqualTo(Contexts.Length * 10));
            Assert.That(audit.Generated, Is.GreaterThanOrEqualTo(100), "At least 100 actual raw model generations, not transport failures or fallback attempts");
            TestContext.WriteLine("All raw attempts: " + audit.Path + "; semantic review remains required.");
        }

        // Same viewer, same event; only the durable relationship differs. Paired reaction ids across states.
        [Test, Explicit("Requires the configured local model; retains every raw generation"), Timeout(1200000)]
        public void RelationshipStatesChangeBehaviorForTheSameViewerAndEvent()
        {
            using var audit = new Audit("relationship-ab-raw.jsonl");
            var selection = new StringBuilder("{\n  \"note\": \"Deterministic C# selection/planning over fixed seeds; no model involved.\",\n  \"rows\": [\n");
            foreach (string id in new[] { "viewer.pixelfox", "viewer.nightowl" })
            {
                ViewerProfile profile = audit.Config.Community.Find(id);
                foreach (string moment in new[] { "direct question", "failure report" })
                foreach (var (tier, sentiment, visits, acknowledgements) in Tiers)
                {
                    selection.Append(SelectionStats(profile, tier, sentiment, visits, acknowledgements, moment)).Append(",\n");
                    for (int sample = 0; sample < 5; sample++)
                    {
                        var row = audit.NewRow(profile, moment + " / " + tier);
                        audit.Run(row, RelationshipFixture(profile, moment, sentiment, visits, acknowledgements, 1000 + sample));
                    }
                }
            }
            selection.Length -= 2;
            audit.WriteSide("relationship-ab-selection.json", selection.Append("\n  ]\n}\n").ToString());
            Assert.That(audit.Generated, Is.GreaterThanOrEqualTo(60));
        }

        // A: witnessed and relevant; B: same event, never witnessed; C: witnessed, unrelated event.
        [Test, Explicit("Requires the configured local model; retains every raw generation"), Timeout(1200000)]
        public void MemoryCallbacksAreRelevantSparseAndIsolated()
        {
            using var audit = new Audit("memory-ab-raw.jsonl");
            foreach (string id in new[] { "viewer.nightowl", "viewer.zinaivanovna" })
            {
                ViewerProfile profile = audit.Config.Community.Find(id);
                foreach (string condition in new[] { "A witnessed relevant", "B no memory", "C witnessed unrelated" })
                for (int sample = 0; sample < 8; sample++)
                {
                    var row = audit.NewRow(profile, condition);
                    Fixture fixture = MemoryFixture(profile, condition, 2000 + sample);
                    if (condition.StartsWith("B")) Assert.That(fixture.Candidates, Is.Zero, "no witnessed fact, no candidate");
                    if (condition.StartsWith("C")) Assert.That(fixture.Candidates, Is.Zero, "an unrelated moment retrieves nothing");
                    if (condition.StartsWith("A")) Assert.That(fixture.Candidates, Is.EqualTo(1));
                    audit.Run(row, fixture);
                }
            }
            Assert.That(audit.Generated, Is.GreaterThanOrEqualTo(40));
        }

        private static readonly string[] Contexts = {
            "dumb mistake RU", "question RU", "impressive win RU", "ordinary filler RU", "microphone quality", "long silence",
            "question EN", "direct thanks", "cold relationship", "warm relationship", "witness memory callback",
            "absent memory contrast", "known promise fulfilled", "known promise broken"
        };

        private static readonly (RelationshipTier tier, int sentiment, int visits, int acknowledgements)[] Tiers =
        {
            (RelationshipTier.Wary, -50, 3, 1), (RelationshipTier.Neutral, 0, 3, 1), (RelationshipTier.Friendly, 40, 3, 1), (RelationshipTier.Loyal, 80, 8, 5)
        };

        // Fixed conditional fixtures create witnessed facts through the actual community/ledger, not diary text.
        private static Fixture BuildFixture(ViewerProfile profile, string context, long serial)
        {
            var (roster, community) = World(profile, serial, context != "absent memory contrast", old =>
            {
                if (context == "witness memory callback" || context == "absent memory contrast")
                    old.Say("Я впервые проиграл финал турнира");
                if (context == "known promise fulfilled" || context == "known promise broken")
                {
                    old.Say("я завтра куплю видеокарту");
                    Assert.That(old.Community.Promises.Summary, Has.Count.EqualTo(1));
                    if (context == "known promise fulfilled")
                        old.Community.ObserveGameplay(new PromiseGameplayFact(PromiseAction.Purchase, "gpu", 1500, "audit-order-" + serial, EventWitnesses.Capture(old.Roster)));
                    else old.Community.UpdateContext(2881, StreamTopic.Hardware);
                    Assert.That(old.Community.Promises.Summary.Single().Status, Is.EqualTo(context == "known promise fulfilled" ? PromiseStatus.Fulfilled : PromiseStatus.Broken));
                }
            }, snapshot =>
            {
                if (context == "cold relationship") { snapshot.Viewers[0].Sentiment = -50; snapshot.Viewers[0].Acknowledgements = 4; }
                if (context == "warm relationship") { snapshot.Viewers[0].Sentiment = 50; snapshot.Viewers[0].Acknowledgements = 4; }
            });
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
                : text != null ? Speech(community, roster, serial, "audit-now-" + serial, text, context == "question EN" ? "en" : "ru", 4000)
                : throw new ArgumentOutOfRangeException(nameof(context), context, "Unknown fixed audit context");
            bool direct = context == "direct thanks" || context.EndsWith("relationship", StringComparison.Ordinal);
            Fixture fixture = Plan(profile, community, roster, current, direct, serial, context == "question EN" ? ViewerLanguage.English : ViewerLanguage.Russian);
            if (context == "witness memory callback") Assert.That(fixture.Candidates, Is.EqualTo(1));
            if (context == "absent memory contrast") Assert.That(fixture.Candidates, Is.Zero);
            if (context.StartsWith("known promise", StringComparison.Ordinal)) Assert.That(fixture.Candidates, Is.EqualTo(1));
            return fixture;
        }

        private static Fixture RelationshipFixture(ViewerProfile profile, string moment, int sentiment, int visits, int acknowledgements, long serial)
        {
            var (roster, community) = World(profile, serial, true, _ => { }, snapshot =>
            {
                snapshot.Viewers[0].Sentiment = sentiment; snapshot.Viewers[0].VisitCount = visits; snapshot.Viewers[0].Acknowledgements = acknowledgements;
            });
            bool direct = moment == "direct question";
            string text = direct ? profile.DisplayName + ", ты тут? Как тебе сегодняшний стрим?"
                : "Блин, я продал не тот предмет и опять умер в игре самым тупым способом!";
            return Plan(profile, community, roster, Speech(community, roster, serial, "ab-now-" + serial, text, "ru", 4000), direct, serial, ViewerLanguage.Russian);
        }

        private static Fixture MemoryFixture(ViewerProfile profile, string condition, long serial)
        {
            var (roster, community) = World(profile, serial, !condition.StartsWith("B"), old => old.Say("Я впервые проиграл финал турнира"), _ => { }, 1600);
            string text = condition.StartsWith("C") ? "Чат, какую игру лучше запустить дальше?"
                : "Чат, я снова в финале турнира. Как думаете, в этот раз получится?";
            return Plan(profile, community, roster, Speech(community, roster, serial, "memory-now-" + serial, text, "ru", 1600), false, serial,
                ViewerLanguage.Russian, 1600);
        }

        private sealed class OldBroadcast
        {
            public ViewerCommunity Community; public AudienceRoster Roster; public long Serial; private int _phrase;
            public void Say(string text) => Community.Process(Speech(Community, Roster, Serial * 10 + ++_phrase, "audit-old-" + Serial, text, "ru", 100));
        }

        // One earlier broadcast (optionally witnessed), a save/restore with controlled relationship rows, then the
        // current broadcast with the viewer present. All state is the real community/ledger.
        private static (AudienceRoster, ViewerCommunity) World(ViewerProfile profile, long serial, bool witnessOld, Action<OldBroadcast> old,
            Action<ViewerCommunitySnapshot> relationship, double now = 4000)
        {
            var roster = new AudienceRoster((random, index) => null);
            var community = new ViewerCommunity(new[] { profile }, roster);
            roster.SetAudienceSize(10);
            community.BeginBroadcast("audit-old-" + serial, (ulong)serial, 100, StreamTopic.Games);
            if (witnessOld) roster.Join(profile.Participant(), 0);
            old(new OldBroadcast { Community = community, Roster = roster, Serial = serial });
            community.EndBroadcast();
            var snapshot = community.Capture();
            relationship(snapshot);
            community.Restore(snapshot);
            roster.SetAudienceSize(10);
            community.BeginBroadcast("audit-now-" + serial, (ulong)serial + 1, now, StreamTopic.Games);
            roster.Join(profile.Participant(), 0);
            return (roster, community);
        }

        private static StreamEvent Speech(ViewerCommunity community, AudienceRoster roster, long id, string broadcast, string text, string language, double gameMinutes) =>
            StreamEvent.StreamerSpeech(id, broadcast, 600, SpeechRelevance.Analyze(
                new RecognizedSpeech(id, text, 600, 1f, language), community.KnownNames)).WithWitnesses(roster, gameMinutes);

        // The production decision: relevant facts are only candidates; the planner's bounded roll decides.
        private static Fixture Plan(ViewerProfile profile, ViewerCommunity community, AudienceRoster roster, StreamEvent current, bool direct,
            long serial, ViewerLanguage channel, double gameMinutes = 4000)
        {
            var intent = (ReactionIntent)typeof(ReactionIntent).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single(c => c.GetParameters().Length == 8).Invoke(new object[] { serial, current, roster.Find(profile.Id), direct, 0, 600d, 620d, roster.Epoch(profile.Id) });
            IReadOnlyList<ViewerMemory> relevant = community.State(profile.Id).Memories.Retrieve(current, gameMinutes);
            string subject = ViewerPromiseVocabulary.Subject(current);
            ViewerPromiseContext known = subject == null ? null : community.Promises.Retrieve(profile.Id, subject, gameMinutes);
            RelationshipTier tier = community.Tier(profile.Id);
            ViewerUtterancePlanner.ChooseCallback(intent, tier, relevant, known, out ViewerMemory memory, out ViewerPromiseContext promise);
            var situation = new ChatSituation("audit", 600, 10, channel, Array.Empty<StreamChatMessage>(), null, community.RelationshipContext(profile.Id),
                memory == null ? null : new[] { memory }, promise, tier, StreamTopic.Games, gameMinutes);
            return new Fixture { Roster = roster, Chat = new StreamChat(), Intent = intent, Situation = situation,
                Candidates = relevant.Count + (known == null ? 0 : 1) };
        }

        private static string SelectionStats(ViewerProfile profile, RelationshipTier tier, int sentiment, int visits, int acknowledgements, string moment)
        {
            ChatParticipant viewer = profile.Participant();
            int reactions = 0; double delays = 0; var intents = new Dictionary<UtteranceIntent, int>();
            string text = moment == "direct question" ? profile.DisplayName + ", ты тут? Как тебе сегодняшний стрим?"
                : "Блин, я продал не тот предмет и опять умер в игре самым тупым способом!";
            for (ulong seed = 1; seed <= 400; seed++)
            {
                var roster = new AudienceRoster(EphemeralViewers.Create);
                roster.SetAudienceSize(6);
                roster.Join(viewer);
                var selector = new ReactionSelector(new ReactionTuning(), roster, new AudienceRandom(seed), id => id == viewer.ViewerId ? tier : RelationshipTier.Neutral);
                StreamEvent e = StreamEvent.StreamerSpeech((long)seed, "ab", 10, SpeechRelevance.Analyze(new RecognizedSpeech((long)seed, text, 10, 1f, "ru"),
                    new[] { new ViewerNameForms(viewer.ViewerId, viewer.NameForms) }));
                ReactionIntent intent = selector.Select(e, 10, true, out _).FirstOrDefault(i => i.Viewer == viewer);
                if (intent == null) continue;
                reactions++; delays += intent.DueSeconds - 10;
                var plan = ViewerUtterancePlanner.For(intent, new ChatSituation("ab", 600, 6, ViewerLanguage.Russian, null, null, tier: tier, content: StreamTopic.Games));
                intents[plan.Intent] = intents.TryGetValue(plan.Intent, out int n) ? n + 1 : 1;
            }
            string mix = string.Join(", ", intents.OrderByDescending(p => p.Value).Select(p => $"\"{p.Key}\": {p.Value}"));
            return $"    {{\"viewer\": \"{profile.DisplayName}\", \"moment\": \"{moment}\", \"tier\": \"{tier}\", \"sentiment\": {sentiment}, \"visits\": {visits}, " +
                   $"\"acknowledgements\": {acknowledgements}, \"seeds\": 400, \"reactionRate\": {reactions / 400.0:0.000}, " +
                   $"\"meanDelaySeconds\": {delays / Math.Max(1, reactions):0.00}, \"callbackChance\": {ViewerUtterancePlanner.CallbackChance(viewer, tier):0.000}, " +
                   $"\"plannedIntents\": {{{mix}}}}}";
        }

        private sealed class Fixture
        {
            public AudienceRoster Roster;
            public StreamChat Chat;
            public ReactionIntent Intent;
            public ChatSituation Situation;
            public int Candidates;
        }

        // Shared execution: one real generation per cell through the ordinary director, every record retained.
        private sealed class Audit : IDisposable
        {
            private readonly StreamWriter _writer;
            private readonly Recorder _model;
            private readonly string _directory;
            private int _attempts;
            public ViewerCoreConfig Config { get; }
            public string Path { get; }
            public int Generated { get; private set; }

            public Audit(string file)
            {
                Config = AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>("Assets/Game/Config/Viewers/ViewerCore.asset");
                Assert.That(Config.ValidationError, Is.Null);
                _directory = Environment.GetEnvironmentVariable("GO_LIVE_VISUAL_OUTPUT");
                Assert.That(_directory, Is.Not.Null.And.Not.Empty);
                Directory.CreateDirectory(_directory);
                Path = System.IO.Path.Combine(_directory, file);
                _writer = new StreamWriter(Path, false, new UTF8Encoding(false)) { AutoFlush = true };
                _model = new Recorder(Config.Model, _writer);
            }

            public void WriteSide(string file, string content) => File.WriteAllText(System.IO.Path.Combine(_directory, file), content, new UTF8Encoding(false));

            public Row NewRow(ViewerProfile profile, string context) => new()
            {
                record = "attempt", profileId = profile.Id, displayName = profile.DisplayName, context = context, endpoint = Config.Model.Endpoint,
                model = Config.Model.Model, modelSettings = JsonUtility.ToJson(Config.Model), identity = JsonUtility.ToJson(profile)
            };

            public void Run(Row row, Fixture fixture)
            {
                _attempts++;
                _model.Row = row;
                var log = new ReactionLog();
                using var director = new ChatDirector(new BorrowedModel(_model), Config.Model, fixture.Chat, log);
                director.BeginBroadcast(new AudienceRandom((ulong)_attempts));
                ViewerChatRequest request = ChatContextBuilder.Build(fixture.Intent, fixture.Situation, Config.Model.MaximumTokens);
                ViewerUtterancePlan plan = ViewerUtterancePlanner.For(fixture.Intent, fixture.Situation);
                row.systemPrompt = request.System; row.userPrompt = request.User; row.maximumTokens = request.MaximumTokens;
                row.plannedIntent = plan.Intent.ToString(); row.plannedManner = plan.Manner.ToString(); row.tier = plan.RelationshipTone.ToString();
                row.topic = plan.Topic; row.target = plan.Target.ToString(); row.callbackCandidates = fixture.Candidates;
                row.memoryId = plan.RelevantMemoryId; row.promiseId = plan.RelevantPromiseId;
                _writer.WriteLine(JsonUtility.ToJson(row));
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
                _model.Drain();
                StreamChatMessage published = fixture.Chat.Messages.LastOrDefault(m => m.IntentId == fixture.Intent.Id);
                row.publishedSource = published?.Source.ToString() ?? "None";
                row.publishedText = published?.Text;
                row.publicationReason = log.Entries.LastOrDefault()?.Reason;
                row.maximumQueueDepth = director.Stats.MaximumQueueDepth;
                // Validation is recorded independently of runtime fallback publication.
                ChatValidation validation = ChatOutputValidator.Validate(row.rawText, fixture.Intent, fixture.Situation.RecentChat, fixture.Situation);
                row.accepted = validation.Accepted; row.validation = validation.Reason;
                if (row.httpStatus == 200 && !string.IsNullOrWhiteSpace(row.rawCompletion)) Generated++;
                row.record = "generation";
                _writer.WriteLine(JsonUtility.ToJson(row));
                Assert.That(_model.CallsForRow, Is.EqualTo(1), "Each fixed cell must retain one actual attempt, with no regeneration");
            }

            public void Dispose() { _model.Dispose(); _writer.Dispose(); }
        }

        [Serializable]
        private sealed class Row
        {
            public string record, profileId, displayName, context, identity, endpoint, model, modelSettings;
            public string systemPrompt, userPrompt, requestBody, rawResponse, rawCompletion, rawText, status, detail, validation;
            public string publishedSource, publishedText, publicationReason;
            public string plannedIntent, plannedManner, tier, topic, target, memoryId, promiseId;
            public int maximumTokens, httpStatus, promptTokens, completionTokens, maximumQueueDepth, callbackCandidates;
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
