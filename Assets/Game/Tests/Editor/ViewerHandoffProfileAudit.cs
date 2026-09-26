using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    // Developer-only CONDITIONAL STYLE AUDITION: one forced output per cell, never runtime selection evidence.
    public sealed class ViewerHandoffProfileAudit
    {
        [Test, Explicit("Requires the configured local model; writes every unfiltered attempt"), Timeout(900000)]
        public void CompareTenProfilesOnFiveRequiredContexts()
        {
            var core = AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>("Assets/Game/Config/Viewers/ViewerCore.asset");
            if (core == null || core.ValidationError != null || core.Community.Profiles.Count != 10)
                throw new InvalidOperationException("The authored Viewer Core with ten valid community profiles is required.");
            string folder = Environment.GetEnvironmentVariable("GO_LIVE_VISUAL_OUTPUT");
            if (string.IsNullOrWhiteSpace(folder)) throw new InvalidOperationException("Set GO_LIVE_VISUAL_OUTPUT.");
            Directory.CreateDirectory(folder);
            string stem = Path.Combine(folder, "viewer-profile-audition-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            using var log = new StreamWriter(stem + ".jsonl", false, new UTF8Encoding(false)) { AutoFlush = true };
            var contexts = Contexts();
            var table = new StringBuilder("# CONDITIONAL STYLE AUDITION\n\n50 forced outputs; no retries, filtering or seed search. Quality requires manual review.\n" +
                "Selector measurements use seeds 0–199 independently, all ten named viewers watching, audience 10 and normal tuning. " +
                "This isolates selection competition and delay, not eventual presence or runtime integration. Each trial starts with fresh rhythm.\n");
            using var model = new OpenAiCompatibleChatModel(core.Model);
            var recorder = new RecordingHandler(log);
            var httpField = typeof(OpenAiCompatibleChatModel).GetField("_http", BindingFlags.Instance | BindingFlags.NonPublic);
            if (httpField == null) throw new MissingFieldException("OpenAiCompatibleChatModel._http");
            var originalHttp = (HttpClient)httpField.GetValue(model);
            httpField.SetValue(model, new HttpClient(recorder) { Timeout = System.Threading.Timeout.InfiniteTimeSpan });
            originalHttp.Dispose(); // Test-local transport capture; production request building/parsing remain in use.
            int attempts = 0, responses = 0;
            foreach (var context in contexts)
            {
                Dictionary<string, SelectionRow> selections = Measure(core.Reactions, context, core.Community.Profiles);
                table.Append("\n## ").Append(context.Name).Append("\n\n").Append(context.Description).Append("\n\n")
                    .Append("| Viewer | Raw model text | Status | Accepted / reason | ms | tokens in/out | fallback | selected / 200 | mean delay s |\n")
                    .Append("|---|---|---|---|---:|---:|---|---:|---:|\n");
                foreach (ViewerProfile profile in core.Community.Profiles)
                {
                    ChatParticipant viewer = profile.Participant();
                    SelectionRow selection = selections[profile.Id];
                    log.WriteLine(JsonUtility.ToJson(selection));
                    var constructor = typeof(ReactionIntent).GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new[] { typeof(long), typeof(StreamEvent), typeof(ChatParticipant), typeof(bool), typeof(int), typeof(double), typeof(double) }, null);
                    if (constructor == null) throw new MissingMethodException("ReactionIntent audit constructor");
                    var intent = (ReactionIntent)constructor.Invoke(new object[] { context.Event.Serial, context.Event, viewer, false, 0, 600d, 620d });
                    var situation = new ChatSituation("audit", 600, 10, ViewerLanguage.Russian, Array.Empty<StreamChatMessage>(), null);
                    ViewerChatRequest request = ChatContextBuilder.Build(intent, situation, core.Model.MaximumTokens);
                    var row = new GenerationRow { profileId = profile.Id, displayName = profile.DisplayName, identity = JsonUtility.ToJson(profile),
                        context = context.Name, eventKind = context.Event.Kind.ToString(), eventKey = context.Event.Key,
                        contextDescription = context.Description, significance = context.Event.Significance,
                        speech = context.Event.Speech?.Text, cues = context.Event.Speech?.Cues.ToString(), topics = context.Event.Speech?.Topics.ToString(),
                        systemPrompt = request.System, userPrompt = request.User, maximumTokens = request.MaximumTokens,
                        endpoint = core.Model.Endpoint, model = core.Model.Model, modelSettings = JsonUtility.ToJson(core.Model),
                        fallback = FallbackChat.Pick(intent, new AudienceRandom((ulong)context.Event.Serial), situation.RecentChat) };
                    recorder.Row = row; row.record = "attempt"; log.WriteLine(JsonUtility.ToJson(row)); attempts++;
                    var clock = System.Diagnostics.Stopwatch.StartNew();
                    try
                    {
                        LanguageModelResult result = model.GenerateAsync(request, CancellationToken.None).GetAwaiter().GetResult();
                        row.status = result.Status.ToString(); row.rawText = result.Text; row.detail = result.Detail;
                        row.latencySeconds = result.LatencySeconds; row.promptTokens = result.PromptTokens; row.completionTokens = result.CompletionTokens;
                        ChatValidation validation = ChatOutputValidator.Validate(result.Text, intent, situation.RecentChat);
                        row.accepted = validation.Accepted; row.validation = validation.Reason; row.validatedText = validation.Text;
                    }
                    catch (Exception error) { row.status = "Exception"; row.detail = error.ToString(); row.latencySeconds = clock.Elapsed.TotalSeconds; }
                    finally { row.record = "generation"; log.WriteLine(JsonUtility.ToJson(row)); }
                    if (row.httpStatus > 0) responses++;
                    table.Append("| ").Append(Cell(profile.DisplayName)).Append(" | ").Append(Cell(row.rawText ?? row.rawResponse)).Append(" | ")
                        .Append(Cell(row.status)).Append(" | ").Append(Cell(row.accepted ? "accepted" : row.validation ?? row.detail)).Append(" | ")
                        .Append((row.latencySeconds * 1000).ToString("0", CultureInfo.InvariantCulture)).Append(" | ")
                        .Append(row.promptTokens).Append('/').Append(row.completionTokens).Append(" | ").Append(Cell(row.fallback)).Append(" | ")
                        .Append(selection.selected).Append(" | ")
                        .Append(selection.meanDelaySeconds.ToString("0.00", CultureInfo.InvariantCulture)).Append(" |\n");
                    File.WriteAllText(stem + ".md", table.ToString(), new UTF8Encoding(false));
                }
            }
            TestContext.WriteLine("Audit artifacts: " + stem + ".jsonl and .md; manual quality review required.");
            Assert.That(attempts, Is.EqualTo(50), "coverage only, not a quality verdict");
            Assert.That(responses, Is.GreaterThan(0), "at least one backend HTTP response, not a quality verdict");
        }

        private static List<Context> Contexts()
        {
            Context Speech(long id, string name, string text) => new Context { Name = name, Description = "Truthful streamer self-report: «" + text + "»",
                Event = StreamEvent.StreamerSpeech(id, "style-audit", 600,
                    SpeechRelevance.Analyze(new RecognizedSpeech(id, text, 600, 1f, "ru"), Array.Empty<ViewerNameForms>())) };
            return new List<Context> {
                Speech(1, "Dumb mistake", "Блин, я продал не тот предмет и опять умер в игре самым тупым способом!"),
                Speech(2, "Question to chat", "Чат, как думаете, какую игру лучше запустить дальше?"),
                Speech(3, "Impressive win", "Я выиграл этот раунд один против пятерых, вот это жесть!"),
                Speech(4, "Ordinary low-value speech", "Так, ну, секунду, ага"),
                new Context { Name = "In-game microphone audio worsens", Description = "In-game microphone disconnected; stream voice quality worsened.",
                    Event = StreamEvent.PeripheralChanged(5, "style-audit.microphone-off", 600, PcPeripheralKind.Microphone, false) }
            };
        }

        private static Dictionary<string, SelectionRow> Measure(ReactionTuning tuning, Context context, IReadOnlyList<ViewerProfile> profiles)
        {
            var rows = new Dictionary<string, SelectionRow>();
            var delays = new Dictionary<string, List<double>>();
            foreach (ViewerProfile profile in profiles)
            {
                rows.Add(profile.Id, new SelectionRow { record = "selection", profileId = profile.Id, context = context.Name,
                    seedFirst = 0, seedLast = 199, trials = 200, affinity = profile.Traits().Affinity(context.Event.Kind),
                    pace = profile.Pace, speed = profile.ResponseSpeed, talkativeness = profile.Talkativeness });
                delays.Add(profile.Id, new List<double>());
            }
            for (ulong seed = 0; seed < 200; seed++)
            {
                var roster = new AudienceRoster((random, index) => null); roster.SetAudienceSize(10);
                foreach (ViewerProfile profile in profiles) roster.Join(profile.Participant());
                var selector = new ReactionSelector(tuning, roster, new AudienceRandom(seed));
                List<ReactionIntent> selected = selector.Select(context.Event, 600, true, out string reason);
                foreach (SelectionRow row in rows.Values)
                    if (reason == "speech below threshold") row.belowThreshold++;
                foreach (ReactionIntent intent in selected)
                {
                    rows[intent.Viewer.ViewerId].selected++;
                    delays[intent.Viewer.ViewerId].Add(intent.DueSeconds - 600);
                }
            }
            foreach (SelectionRow row in rows.Values)
            {
                row.silent = row.trials - row.selected; row.delaySeconds = delays[row.profileId].ToArray();
                foreach (double delay in row.delaySeconds) row.meanDelaySeconds += delay;
                if (row.selected > 0) row.meanDelaySeconds /= row.selected;
            }
            return rows;
        }

        private static string Cell(string value) => (value ?? "—").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
            .Replace("|", "&#124;").Replace("\r", "").Replace("\n", "<br>");
        private sealed class Context { public string Name, Description; public StreamEvent Event; }
        [Serializable] private sealed class GenerationRow
        {
            public string record, profileId, displayName, identity, context, contextDescription, eventKind, eventKey, speech, cues, topics;
            public string systemPrompt, userPrompt, endpoint, model, modelSettings, requestBody, rawResponse, rawText, status, detail, validation, validatedText, fallback;
            public float significance; public int maximumTokens, httpStatus, promptTokens, completionTokens; public double latencySeconds; public bool accepted;
        }
        [Serializable] private sealed class SelectionRow
        {
            public string record, profileId, context; public int seedFirst, seedLast, trials, selected, silent, belowThreshold;
            public float affinity, pace, speed, talkativeness; public double meanDelaySeconds; public double[] delaySeconds;
        }
        private sealed class RecordingHandler : DelegatingHandler
        {
            private readonly StreamWriter _log; public GenerationRow Row;
            public RecordingHandler(StreamWriter log) : base(new HttpClientHandler { UseProxy = false }) => _log = log;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
            {
                Row.requestBody = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
                try
                {
                    HttpResponseMessage response = await base.SendAsync(request, cancellation).ConfigureAwait(false);
                    Row.httpStatus = (int)response.StatusCode;
                    Row.rawResponse = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Row.record = "wire-response"; _log.WriteLine(JsonUtility.ToJson(Row)); return response;
                }
                catch (Exception error) { Row.record = "wire-failure"; Row.detail = error.ToString(); _log.WriteLine(JsonUtility.ToJson(Row)); throw; }
            }
        }
    }
}
