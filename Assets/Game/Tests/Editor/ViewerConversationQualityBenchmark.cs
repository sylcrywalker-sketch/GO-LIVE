using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
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
    // Explicit evidence collection for the conversation quality pass. One fixed corpus of GO! LIVE conversation
    // contexts (authored profiles, recorded day entries, real session phrases) runs through the production planner,
    // prompt, adapter, validator, fallback and director. Every attempt is written; there are no retries and no
    // selection of better samples. The model id, repeats and output directory come from the environment so the
    // same corpus can compare prompts and models. It asserts nothing about wording.
    [Category("LocalModel")]
    public sealed class ViewerConversationQualityBenchmark
    {
        private const string Broadcast = "quality-bench";
        private const double GameMinutes = 3 * 1440 + 20 * 60; // day 3, 20:00: memories from day 2 are "yesterday"

        private sealed class Seat
        {
            public string Profile;          // viewer.* id, or anon:<persona index>:<name>
            public string Day;              // DailyLife id, gen.* for anonymous seats, null = no day
            public RelationshipTier Tier = RelationshipTier.Neutral;
            public int Visits = 1, Acknowledgements;
        }

        private sealed class Case
        {
            public string Id, Group, Evidence;
            public Seat[] Seats;
            public (int seat, string text)[] Prior = Array.Empty<(int, string)>();
            public string[] Turns;          // streamer lines in order; turn 2+ continue the thread of seat 0
            public ConversationTargetKind Target = ConversationTargetKind.SpecificViewer;
            public bool FollowUp;           // turn 1 continues seat 0's prior line
            public ViewerMemorySnapshot Memory;
            public ViewerPromiseSnapshot Promise;
            public PromiseStatus PromiseStatus;
        }

        [Serializable]
        private sealed class Row
        {
            public string record, label, model, caseId, group, evidence, targetKind, viewerId, viewerName, profileId, dayId, tier;
            public int sample, turn, order;
            public long intentId;
            public string speech, purpose, plannedIntent, previousViewerLine, memory, promise;
            public string[] answerFacts, recentChat;
            public string systemPrompt, userPrompt, status, rawModelText, detail, validationReason;
            public string outcome, outcomeReason, publishedText, publishedSource, notes;
            public bool accepted;
            public int promptCharacters, maximumTokens, promptTokens, completionTokens;
            public double latencySeconds;
        }

        [Test, Explicit("Requires a running local model; writes every attempt outside the repository"), Timeout(3600000)]
        public void RunConversationCorpusAndKeepEveryOutput()
        {
            var config = AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>("Assets/Game/Config/Viewers/ViewerCore.asset");
            Assert.That(config, Is.Not.Null);
            Assert.That(config.ValidationError, Is.Null);
            string output = Environment.GetEnvironmentVariable("GO_LIVE_BENCH_OUTPUT");
            Assert.That(output, Is.Not.Null.And.Not.Empty, "Set GO_LIVE_BENCH_OUTPUT to an external evidence directory.");
            output = Path.GetFullPath(output);
            string repo = Path.GetFullPath(Directory.GetCurrentDirectory()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.That((output.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).StartsWith(repo, StringComparison.OrdinalIgnoreCase),
                Is.False, "Benchmark evidence must stay outside the repository.");
            string label = Environment.GetEnvironmentVariable("GO_LIVE_BENCH_LABEL") ?? "run";
            int repeats = int.TryParse(Environment.GetEnvironmentVariable("GO_LIVE_BENCH_REPEATS"), out int r) ? r : 3;
            string only = Environment.GetEnvironmentVariable("GO_LIVE_BENCH_CASES");

            var settings = JsonUtility.FromJson<ChatModelSettings>(JsonUtility.ToJson(config.Model));
            settings.Enabled = true;
            string model = Environment.GetEnvironmentVariable("GO_LIVE_BENCH_MODEL");
            if (!string.IsNullOrWhiteSpace(model)) settings.Model = model.Trim();
            if (float.TryParse(Environment.GetEnvironmentVariable("GO_LIVE_BENCH_TIMEOUT"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float timeout)) settings.TimeoutSeconds = timeout;
            int.TryParse(Environment.GetEnvironmentVariable("GO_LIVE_BENCH_EXTRA_TOKENS"), out int extraTokens);
            if (extraTokens > 0) settings.MaximumTokens = Math.Min(256, settings.MaximumTokens + extraTokens);
            string suffix = Environment.GetEnvironmentVariable("GO_LIVE_BENCH_SYSTEM_SUFFIX");

            Directory.CreateDirectory(output);
            string path = Path.Combine(output, label + "-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".jsonl");
            using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
            var gate = new object();
            void Write(Row row) { lock (gate) writer.WriteLine(JsonUtility.ToJson(row)); }
            Write(new Row
            {
                record = "manifest", label = label, model = settings.Model,
                notes = "settings=" + JsonUtility.ToJson(settings) + "; repeats=" + repeats + "; extraTokens=" + extraTokens + "; systemSuffix=" + (suffix ?? "") +
                        "; one generation per scheduled reply, no retries, all attempts kept"
            });

            using var inner = new OpenAiCompatibleChatModel(settings);
            var recorder = new RecordingModel(inner, Write, suffix, extraTokens);
            // One unrecorded warm-up so the first case does not pay the model load / prompt-prefix cost.
            inner.GenerateAsync(new ViewerChatRequest(ChatContextBuilder.SystemText, "Warm-up. Reply {\"text\": \"ok\"}.", 8), CancellationToken.None)
                .GetAwaiter().GetResult();

            foreach (Case item in Corpus())
            {
                if (!string.IsNullOrEmpty(only) && !only.Split(',').Contains(item.Id)) continue;
                for (int sample = 1; sample <= repeats; sample++)
                    RunCase(item, sample, label, settings, recorder, Write);
            }
            recorder.Drain();
            Write(new Row { record = "summary", label = label, model = settings.Model, notes = "model OK=" + recorder.Ok + "; non-OK=" + recorder.NotOk });
            TestContext.WriteLine("QUALITY_BENCH " + path);
            Assert.That(recorder.Ok, Is.GreaterThan(0), "No successful model response; inspect the saved records.");
        }

        private static void RunCase(Case item, int sample, string label, ChatModelSettings settings, RecordingModel recorder, Action<Row> write)
        {
            ChatParticipant pendingAnonymous = null;
            var roster = new AudienceRoster((random, index) => pendingAnonymous);
            roster.SetAudienceSize(Math.Max(3, item.Seats.Length));
            var seats = new List<(ChatParticipant viewer, Seat seat, ViewerDailyState day)>();
            for (int i = 0; i < item.Seats.Length; i++)
            {
                ChatParticipant viewer = Participant(item.Seats[i].Profile);
                if (viewer.IsPermanent) Assert.That(roster.Join(viewer, 1), Is.True);
                else
                {
                    pendingAnonymous = viewer;
                    Assert.That(roster.AnonymousChatter(new AudienceRandom(1), _ => false, 1), Is.SameAs(viewer));
                }
                seats.Add((viewer, item.Seats[i], Day(viewer, item.Seats[i])));
            }
            var chat = new StreamChat();
            double stream = 600;
            foreach (var (seat, text) in item.Prior)
                chat.Add(Broadcast, seats[seat].viewer.ViewerId, seats[seat].viewer.DisplayName, text, stream - 8, -1, ReactionSource.LanguageModel);
            var log = new ReactionLog();
            var clock = Stopwatch.StartNew();
            using var director = new ChatDirector(new Borrowed(recorder), settings, chat, log, () => clock.Elapsed.TotalSeconds);
            director.BeginBroadcast(new AudienceRandom((ulong)(StableHash(item.Id) + (ulong)sample)));
            var intents = new Dictionary<long, (ReactionIntent intent, Row row)>();
            // The director logs the outcome (with the published text) before raising Shown.
            log.Added += entry =>
            {
                if (!intents.TryGetValue(entry.IntentId, out var known)) return;
                Row row = known.row;
                row.record = "outcome"; row.outcome = entry.Outcome.ToString(); row.outcomeReason = entry.Reason;
                row.publishedText = entry.Text; row.publishedSource = entry.Source.ToString();
                write(row);
            };
            string lastLine = item.Prior.Where(p => p.seat == 0).Select(p => p.text).LastOrDefault();
            long serial = 0;
            for (int turn = 0; turn < item.Turns.Length; turn++)
            {
                string text = item.Turns[turn];
                StreamEvent e = StreamEvent.StreamerSpeech(turn + 1, Broadcast, stream,
                    SpeechRelevance.Analyze(new RecognizedSpeech(turn + 1, text, 0, .95f, "ru"), roster.NamesForMentions())).WithWitnesses(roster, GameMinutes);
                bool thread = turn > 0 || item.FollowUp;
                var scheduled = new List<ReactionIntent>();
                int count = item.Target == ConversationTargetKind.Group && turn == 0 ? seats.Count : 1;
                for (int order = 0; order < count; order++)
                {
                    var (viewer, seat, _) = seats[order];
                    ConversationTargetKind kind = item.Target == ConversationTargetKind.Group && turn == 0 ? ConversationTargetKind.Group
                        : thread ? (item.Target == ConversationTargetKind.SpecificViewer && e.Speech.MentionedViewerIds.Contains(viewer.ViewerId)
                            ? ConversationTargetKind.SpecificViewer : ConversationTargetKind.ActiveThreadViewer)
                        : item.Target;
                    bool direct = kind == ConversationTargetKind.SpecificViewer || kind == ConversationTargetKind.ActiveThreadViewer;
                    double due = clock.Elapsed.TotalSeconds + .05 + order * 1.4;
                    var intent = (ReactionIntent)typeof(ReactionIntent).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                        .Single(c => c.GetParameters().Length == 8)
                        .Invoke(new object[] { ++serial + turn * 10, e, viewer, direct, order, due, due + 30, roster.Epoch(viewer.ViewerId) });
                    Set(intent, "Tier", seat.Tier);
                    Set(intent, "Conversational", kind != ConversationTargetKind.None);
                    Set(intent, "FollowUp", thread && kind != ConversationTargetKind.Group);
                    Set(intent, "ConversationTarget", Activator.CreateInstance(typeof(ConversationTarget), BindingFlags.Instance | BindingFlags.NonPublic, null,
                        new object[] { kind, kind == ConversationTargetKind.Group || kind == ConversationTargetKind.None ? null : viewer.ViewerId,
                            kind == ConversationTargetKind.Group || kind == ConversationTargetKind.None ? 0L : roster.Epoch(viewer.ViewerId) }, null));
                    Set(intent, "PreviousViewerLine", thread && kind != ConversationTargetKind.Group ? lastLine : null);
                    var row = new Row
                    {
                        label = label, model = settings.Model, caseId = item.Id, group = item.Group, evidence = item.Evidence, sample = sample, turn = turn + 1,
                        order = order, intentId = intent.Id, targetKind = kind.ToString(), viewerId = viewer.ViewerId, viewerName = viewer.DisplayName,
                        profileId = seat.Profile, dayId = seats[order].day?.Activity.Id, tier = seat.Tier.ToString(), speech = text
                    };
                    intents[intent.Id] = (intent, row);
                    scheduled.Add(intent);
                    director.Submit(intent);
                }

                ChatSituation Context(ReactionIntent intent)
                {
                    int index = seats.FindIndex(s => s.viewer.ViewerId == intent.Viewer.ViewerId);
                    var (viewer, seat, day) = seats[index];
                    var memories = item.Memory != null && index == 0
                        ? new[] { (ViewerMemory)Activator.CreateInstance(typeof(ViewerMemory), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { item.Memory }, null) }
                        : Array.Empty<ViewerMemory>();
                    ViewerPromiseContext promise = item.Promise != null && index == 0
                        ? (ViewerPromiseContext)Activator.CreateInstance(typeof(ViewerPromiseContext), BindingFlags.Instance | BindingFlags.NonPublic, null,
                            new object[] { item.Promise, item.PromiseStatus, item.PromiseStatus == PromiseStatus.Open ? double.NaN : GameMinutes - 600 }, null)
                        : null;
                    var situation = new ChatSituation("stream", stream, roster.AudienceSize, ViewerLanguage.Russian, chat.Messages.ToArray(), text,
                        ViewerUtterancePlanner.Describe(seat.Tier, seat.Visits, seat.Acknowledgements), memories, promise, seat.Tier, StreamTopic.None, GameMinutes,
                        callbackCandidates: memories.Length > 0 ? memories[0].MemoryId : promise?.Id);
                    bool usesDay = (bool)typeof(ViewerUtterancePlanner).GetMethod("UsesDailyContext", BindingFlags.Static | BindingFlags.NonPublic)
                        .Invoke(null, new object[] { intent });
                    if (usesDay && day != null) typeof(ChatSituation).GetProperty("Day").SetValue(situation, day);
                    ViewerUtterancePlan plan = ViewerUtterancePlanner.For(intent, situation);
                    Row row = intents[intent.Id].row;
                    row.purpose = plan.QuestionPurpose.ToString(); row.plannedIntent = plan.Intent.ToString();
                    row.previousViewerLine = plan.PreviousViewerLine;
                    row.answerFacts = plan.DirectAnswerFacts.Select(f => f.Text).ToArray();
                    row.recentChat = situation.RecentChat.Select(m => m.SenderName + ": " + m.Text).ToArray();
                    row.memory = memories.Length > 0 ? memories[0].CanonicalSubject + "/" + memories[0].Kind : null;
                    row.promise = promise == null ? null : promise.Action + "/" + promise.Subject + "/" + promise.Status;
                    if (!usesDay) row.dayId = null;
                    recorder.Expect(intent, situation, row);
                    return situation;
                }

                double started = clock.Elapsed.TotalSeconds;
                while (director.QueueDepth > 0 && clock.Elapsed.TotalSeconds - started < 60)
                {
                    director.Update(clock.Elapsed.TotalSeconds, roster, Context, Broadcast);
                    Thread.Sleep(2);
                }
                if (director.QueueDepth > 0) director.CancelAll("bench wall timeout");
                string published = chat.Messages.Where(m => m.ViewerId == seats[0].viewer.ViewerId).Select(m => m.Text).LastOrDefault();
                if (published != null) lastLine = published;
                stream += 12;
            }
        }

        private static void Set(object target, string property, object value) => target.GetType().GetProperty(property).SetValue(target, value);

        private static ulong StableHash(string text)
        {
            ulong hash = 1469598103934665603UL;
            foreach (char c in text) { hash ^= c; hash *= 1099511628211UL; }
            return hash % 100000;
        }

        private static ChatParticipant Participant(string id)
        {
            if (!id.StartsWith("anon:", StringComparison.Ordinal)) return ViewerProfileTests.Profile(id).Participant();
            string[] parts = id.Split(':');
            int persona = int.Parse(parts[1]);
            var traits = new ReactionTraits(.5f, 1.2f, 1f, StreamTopic.Games);
            return new ChatParticipant("anon.1." + parts[2], parts[2], false, traits, null, ViewerPersona.AnonymousPersona(persona, ViewerLanguage.Russian));
        }

        private static ViewerDailyState Day(ChatParticipant viewer, Seat seat)
        {
            if (seat.Day == null) return null;
            DailyActivity activity = seat.Day.StartsWith("gen.", StringComparison.Ordinal)
                ? ((DailyActivity[])typeof(ViewerDailyLife).GetField("Generic", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null)).Single(a => a.Id == seat.Day)
                : viewer.Persona.Profile.DailyLife.Single(a => a.Id == seat.Day);
            ConversationOpenness openness = seat.Tier == RelationshipTier.Wary ? ConversationOpenness.Reserved
                : viewer.Traits.Talkativeness >= .6f || seat.Tier >= RelationshipTier.Friendly ? ConversationOpenness.Chatty
                : viewer.Traits.Talkativeness < .3f ? ConversationOpenness.Reserved : ConversationOpenness.Normal;
            return (ViewerDailyState)Activator.CreateInstance(typeof(ViewerDailyState), BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { activity, activity.Mood, activity.Energy, openness }, null);
        }

        private const string Kritik = "viewer.kritik228", Zina = "viewer.zinaivanovna", Fox = "viewer.pixelfox", Owl = "viewer.nightowl",
            Mika = "viewer.mika", Dosh = "viewer.doshirak", Byte = "viewer.bytecat", Jonas = "viewer.jonas", Sovetnik = "viewer.sovetnik",
            Kid = "viewer.arcadekid";

        private static Seat S(string profile, string day, RelationshipTier tier = RelationshipTier.Neutral, int visits = 1, int acks = 0) =>
            new() { Profile = profile, Day = day, Tier = tier, Visits = visits, Acknowledgements = acks };

        private static Case One(string id, string group, string evidence, Seat seat, string speech, string prior = null, bool followUp = false) => new()
        {
            Id = id, Group = group, Evidence = evidence, Seats = new[] { seat }, Turns = new[] { speech },
            Prior = prior == null ? Array.Empty<(int, string)>() : new[] { (0, prior) }, FollowUp = followUp
        };

        private static Case Group(string id, string evidence, string speech, params Seat[] seats) => new()
        {
            Id = id, Group = "GroupPersonal", Evidence = evidence, Seats = seats, Turns = new[] { speech }, Target = ConversationTargetKind.Group
        };

        private static Case Chain(string id, string evidence, Seat seat, string prior, bool followUp, params string[] turns) => new()
        {
            Id = id, Group = "Chain", Evidence = evidence, Seats = new[] { seat }, Turns = turns, FollowUp = followUp,
            Prior = prior == null ? Array.Empty<(int, string)>() : new[] { (0, prior) }
        };

        private static ViewerMemorySnapshot Memory(string subject, ViewerMemoryKind kind, MemoryKnowledgeSource source) => new()
        {
            MemoryId = "mem." + subject, EventKey = "old." + subject, CanonicalSubject = subject, Kind = kind,
            Topics = subject == "microphone" ? StreamTopic.StreamSetup : StreamTopic.Games, Importance = 3, Emotion = 1,
            CreatedGameMinutes = 2 * 1440 + 21 * 60, LastReferencedGameMinutes = double.NaN, KnowledgeSource = source
        };

        private static ViewerPromiseSnapshot Promise(string id, PromiseAction action, string subject) => new()
        {
            Id = id, Action = action, Subject = subject, Created = 2 * 1440 + 20 * 60, Deadline = 3 * 1440 + 18 * 60, Status = PromiseStatus.Open
        };

        private const string RealSession = "session-20260926-212336";

        // Fixed corpus. Real session phrases are marked with their source; the rest reproduce the same situations
        // for other authored profiles, days and relationships. Changing it invalidates before/after comparison.
        private static IEnumerable<Case> Corpus()
        {
            const RelationshipTier W = RelationshipTier.Wary, N = RelationshipTier.Neutral, F = RelationshipTier.Friendly, L = RelationshipTier.Loyal;
            // ReasonForMood
            yield return One("R01", "ReasonForMood", RealSession + " #7 (real failed case)", S(Kritik, "krit.ranked", W), "Почему без настроения?", "без настроения но здесь сижу", true);
            yield return One("R02", "ReasonForMood", "named elliptical form of #7", S(Kritik, "krit.ranked", W), "критик, почему?", "без настроения но здесь сижу", true);
            yield return One("R03", "ReasonForMood", "same situation, other profile", S(Fox, "fox.critique", F), "а почему так устала?", "сегодня чет совсем без сил", true);
            yield return One("R04", "ReasonForMood", "same situation, other profile", S(Byte, "cat.migration", N), "почему настроение так себе?", "настроение так себе", true);
            yield return One("R05", "ReasonForMood", "same situation, other profile", S(Zina, "zina.clinic", F), "Почему устали, Зина Ивановна?", "Устала я сегодня немного))", true);
            yield return One("R06", "ReasonForMood", "same situation, other profile", S(Dosh, "dosh.exam", N), "почему настроение на нуле?", "настроение на нуле", true);
            // TodayActivity
            yield return One("T01", "TodayActivity", RealSession + " zina.market day", S(Zina, "zina.market", F), "Зина Ивановна, а что вы сегодня делали?");
            yield return One("T02", "TodayActivity", "named day question", S(Kritik, "krit.school", W), "критик, а ты что сегодня делал?");
            yield return One("T03", "TodayActivity", "named day question", S(Fox, "fox.photos", F), "Пиксель, как день прошёл?");
            yield return One("T04", "TodayActivity", "named day question", S(Mika, "mika.commission", L, 6, 3), "мика, чем сегодня занималась?");
            yield return One("T05", "TodayActivity", "named day question", S(Owl, "owl.after-shift", N), "сова, что делал сегодня?");
            // Mood
            yield return One("M01", "Mood", "named mood question", S(Kritik, "krit.nothing", W), "критик, как настроение?");
            yield return One("M02", "Mood", "named mood question", S(Zina, "zina.daughter", F), "Зина Ивановна, как поживаете?");
            yield return One("M03", "Mood", "named mood question", S(Owl, "owl.errands", N), "сова, как дела?");
            yield return One("M04", "Mood", "named mood question", S(Byte, "cat.quiet", N), "байт, как сам?");
            // CurrentActivity
            yield return One("C01", "CurrentActivity", "named now question", S(Mika, "mika.studies", L, 6, 3), "мика, чем сейчас занята?");
            yield return One("C02", "CurrentActivity", "named now question", S(Dosh, "dosh.delivery", N), "дошик, что делаешь?");
            // GamePreference
            yield return One("G01", "GamePreference", "named game question", S(Kritik, "krit.ranked", W), "критик, во что мне поиграть?");
            yield return One("G02", "GamePreference", "named game question", S(Owl, "owl.day-off", N), "сова, какую игру запустить?");
            yield return One("G03", "GamePreference", "named game question", S(Fox, "fox.portfolio", F), "Пиксель, тебе какие игры нравятся?");
            // Opinion
            yield return One("O01", "Opinion", "named opinion", S(Fox, "fox.library", F), "Пиксель, как тебе оформление канала?");
            yield return One("O02", "Opinion", "named opinion", S(Kritik, "krit.nothing", W), "критик, ну и как тебе мой стрим?");
            yield return One("O03", "Opinion", "named opinion, hardware subject", S(Byte, "cat.psu", N), "байт, что думаешь про мой микрофон?");
            yield return One("O04", "Opinion", "relationship difference vs O02", S(Kritik, "krit.nothing", F, 4, 1), "критик, ну и как тебе мой стрим?");
            // Explanation
            yield return One("E01", "Explanation", RealSession + " #11", S(Kritik, "krit.ranked", W), "Что? Девочка с девятого этажа? О чем ты критика?", "девочка с девятого этажа", true);
            yield return One("E02", "Explanation", RealSession + " line 199", S(Kritik, "krit.ranked", W), "критик, что значит ну да ладно?", "ну да ладно", true);
            yield return One("E03", "Explanation", RealSession + " #12", S(Zina, "zina.market", F), "Зина Ивановна, о чём речь? Какое плохое?", "Ну а почему это должно быть плохое?", true);
            yield return One("E04", "Explanation", "explanation of own daily line", S(Kritik, "krit.ranked", W), "критик, о чём ты?", "проиграл сегодня всё что можно", true);
            // Direct named, not a question
            yield return One("N01", "DirectNamed", RealSession + " #8", S(Zina, "zina.market", F), "Зина Ивановна, я думаю, мои стримы.", "Всё же чего-то радует меня в этой жизни больше всего?)", true);
            yield return One("N02", "DirectNamed", "named greeting", S(Kritik, "krit.school", W), "о, критик, привет");
            yield return One("N03", "DirectNamed", "named thanks, loyal", S(Mika, "mika.commission", L, 8, 4), "мика, спасибо что заходишь");
            // Relationship difference: same question and day, different tier
            yield return One("RD1", "Relationship", "tier contrast with RD2", S(Owl, "owl.tap", W), "сова, как дела?");
            yield return One("RD2", "Relationship", "tier contrast with RD1", S(Owl, "owl.tap", L, 9, 4), "сова, как дела?");
            // Memory / promise callbacks (C# selects the callback; not a question)
            yield return new Case { Id = "MC1", Group = "MemoryCallback", Evidence = "ReportedFailure boss, heard yesterday", Seats = new[] { S(Owl, "owl.errands", N, 3) },
                Turns = new[] { "так, сегодня иду на того же босса" }, Target = ConversationTargetKind.None,
                Memory = Memory("boss", ViewerMemoryKind.ReportedFailure, MemoryKnowledgeSource.HeardStreamer) };
            yield return new Case { Id = "MC2", Group = "MemoryCallback", Evidence = "TechnicalIncident microphone, witnessed yesterday", Seats = new[] { S(Byte, "cat.quiet", F, 4) },
                Turns = new[] { "поменял настройки микрофона" }, Target = ConversationTargetKind.None,
                Memory = Memory("microphone", ViewerMemoryKind.TechnicalIncident, MemoryKnowledgeSource.Witnessed) };
            yield return new Case { Id = "PC1", Group = "PromiseCallback", Evidence = "open GPU purchase promise", Seats = new[] { S(Zina, "zina.market", F, 5) },
                Turns = new[] { "думаю вот, какую видеокарту взять" }, Target = ConversationTargetKind.None,
                Promise = Promise("promise.gpu", PromiseAction.Purchase, "gpu"), PromiseStatus = PromiseStatus.Open };
            yield return new Case { Id = "PC2", Group = "PromiseCallback", Evidence = "broken earlier-start promise", Seats = new[] { S(Owl, "owl.errands", N, 5) },
                Turns = new[] { "всем привет, начинаем стрим" }, Target = ConversationTargetKind.None,
                Promise = Promise("promise.start", PromiseAction.StartBroadcast, "stream-start"), PromiseStatus = PromiseStatus.Broken };
            // Group personal questions
            yield return Group("GP1", RealSession + " #6", "Всем привет парни, как дела, как настроение?",
                S(Kritik, "krit.ranked", W), S(Zina, "zina.market", F), S("anon:0:sleepyx97", "gen.games", N));
            yield return Group("GP2", RealSession + " #10", "да как у вас у всех дела расскажите мне пожалуйста",
                S(Kritik, "krit.school", W), S(Zina, "zina.flat", F), S(Fox, "fox.critique", F));
            yield return Group("GP3", "combined group question", "как у вас дела, что сегодня делали?",
                S(Owl, "owl.tap", N), S(Dosh, "dosh.cafe", N), S(Mika, "mika.studies", L, 6, 3));
            yield return Group("GP4", RealSession + " #35", "Как у вас дела?", S(Zina, "zina.market", F), S(Kritik, "krit.ranked", W));
            // Multi-turn chains (turn 2+ follow the actual published answer)
            yield return Chain("CH1", "spec chain from " + RealSession + " #7", S(Kritik, "krit.ranked", W), "без настроения но здесь сижу", true,
                "почему без настроения?", "понятно. часто играешь в ранкед?", "ну ты хоть иногда выигрываешь?", "ладно, не расстраивайся");
            yield return Chain("CH2", RealSession + " #35-#39", S(Zina, "zina.market", F), null, false,
                "Зина Ивановна, как у вас дела?", "а что сегодня делали?", "Зина Ивановна, какой суп сварила?", "ммм, люблю такой");
            yield return Chain("CH3", "day chain", S(Fox, "fox.portfolio", F), null, false,
                "Пиксель, как дела?", "а чем занималась сегодня?", "и как, получилось?", "покажешь потом?");
            yield return Chain("CH4", "now chain", S(Owl, "owl.after-shift", N), null, false,
                "сова, чем занят?", "и долго ещё?", "не скучно там?", "ладно, держись там");
            yield return Chain("CH5", "opinion chain", S(Kritik, "krit.nothing", W), null, false,
                "критик, как тебе стрим?", "а что не так?", "ну а что бы ты поменял?", "ладно, учту");
            // Holdout: added before the first full post-change run and never used while tuning the prompt.
            const string Holdout = "holdout (not used during prompt development)";
            yield return One("H01", "TodayActivity", Holdout, S(Jonas, "jonas.metro", N), "Йонас, как день прошёл?");
            yield return One("H02", "ReasonForMood", Holdout, S(Sovetnik, "sov.office", N), "советник, а почему скучно?", "скучно сегодня что-то", true);
            yield return One("H03", "Mood", Holdout, S(Dosh, "dosh.payday", F, 3), "дошик, как настроение?");
            yield return One("H04", "Mood", Holdout, S(Kid, "kid.test", F, 3), "аркейд, how are you?");
            yield return One("H05", "Confirmation", Holdout, S(Owl, "owl.day-off", N), "сова, а ты сегодня на смене?");
            yield return One("H06", "ReasonForMood", Holdout, S(Mika, "mika.block", L, 6, 3), "мика, почему грустишь?", "что-то совсем не рисуется", true);
            yield return Group("H07", Holdout, "ребята, кто что сегодня делал?",
                S(Jonas, "jonas.class", N), S(Sovetnik, "sov.router", N), S("anon:2:pelmen_ru", "gen.walk", N));
            yield return One("H08", "CurrentActivity", Holdout, S(Byte, "cat.homeserver", N), "байт, чем занят?");
            yield return Chain("H09", Holdout, S(Zina, "zina.clinic", F), null, false,
                "Зина Ивановна, как настроение?", "а что сегодня делали?", "устали, наверное?", "ну отдыхайте тогда");
            yield return One("H10", "Opinion", Holdout, S(Fox, "fox.library", F), "Пиксель, как тебе идея стримить по утрам?");
        }

        private sealed class RecordingModel
        {
            private readonly OpenAiCompatibleChatModel _inner;
            private readonly Action<Row> _write;
            private readonly string _suffix;
            private readonly int _extraTokens;
            private readonly List<Task<LanguageModelResult>> _tasks = new();
            private (ReactionIntent intent, ChatSituation situation, Row row) _expected;
            public int Ok, NotOk;

            public RecordingModel(OpenAiCompatibleChatModel inner, Action<Row> write, string suffix, int extraTokens)
            { _inner = inner; _write = write; _suffix = suffix; _extraTokens = extraTokens; }

            public void Expect(ReactionIntent intent, ChatSituation situation, Row row) => _expected = (intent, situation, row);

            public Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation)
            {
                // The director builds the situation immediately before its request, so the latest expectation is this one.
                var (intent, situation, row) = _expected;
                row.systemPrompt = request.System; row.userPrompt = request.User; row.maximumTokens = request.MaximumTokens + _extraTokens;
                row.promptCharacters = request.Characters;
                var sent = string.IsNullOrEmpty(_suffix) && _extraTokens == 0 ? request
                    : new ViewerChatRequest(request.System + (_suffix ?? ""), request.User, request.MaximumTokens + _extraTokens);
                Task<LanguageModelResult> task = Record();
                _tasks.Add(task);
                return task;

                async Task<LanguageModelResult> Record()
                {
                    LanguageModelResult result = await _inner.GenerateAsync(sent, cancellation).ConfigureAwait(false);
                    if (result.Status == LanguageModelStatus.Ok) Interlocked.Increment(ref Ok); else Interlocked.Increment(ref NotOk);
                    ChatValidation validation = result.Status == LanguageModelStatus.Ok
                        ? ChatOutputValidator.Validate(result.Text, intent, situation.RecentChat, situation) : ChatValidation.Reject(result.Status.ToString());
                    row.status = result.Status.ToString(); row.rawModelText = result.Text; row.detail = result.Detail;
                    row.latencySeconds = result.LatencySeconds; row.promptTokens = result.PromptTokens; row.completionTokens = result.CompletionTokens;
                    row.accepted = validation.Accepted; row.validationReason = validation.Reason;
                    return result;
                }
            }

            public void Drain() => Task.WhenAll(_tasks).GetAwaiter().GetResult();
        }

        private sealed class Borrowed : IViewerLanguageModel
        {
            private readonly RecordingModel _recorder;
            public Borrowed(RecordingModel recorder) => _recorder = recorder;
            public Task<LanguageModelResult> GenerateAsync(ViewerChatRequest request, CancellationToken cancellation) => _recorder.GenerateAsync(request, cancellation);
            public void Dispose() { }
        }
    }
}
