using System;
using System.Collections.Generic;
using GoLive.Desktop;

namespace GoLive.Viewers
{
    public enum ViewerMood { Good, Tired, Chill, Bored, Upbeat, Stressed }
    public enum ViewerEnergy { Low, Normal, High }
    public enum ConversationOpenness { Reserved, Normal, Chatty }
    // Broad kinds of everyday doings; the validator uses them to keep a viewer's story about today consistent.
    public enum DayActivityKind { Work, Study, Drawing, Gaming, Rest, Errands, Housework, Sleep, Tech, Watching, Walk }

    // One authored, harmless way a viewer's day may have gone. Written in English for the prompt; the model phrases it
    // in the viewer's own language and style. Never a stream, money, hardware or history fact.
    [Serializable]
    public sealed class DailyActivity
    {
        public string Id = "";
        // "spent most of the day drawing commissions"
        public string Today = "";
        // "has the stream on in the background while sketching"
        public string Now = "";
        public DayActivityKind Kind;
        public ViewerMood Mood;
        public ViewerEnergy Energy = ViewerEnergy.Normal;
    }

    // Soft personal present of one viewer for one game day (permanent viewers) or one broadcast (anonymous chatters):
    // enough to answer "как дела?", "что делал сегодня?", "какое настроение?". Derived deterministically from the
    // viewer id and the day, so it stays the same all day (across broadcasts and loads) without being saved.
    public sealed class ViewerDailyState
    {
        public DailyActivity Activity { get; }
        public ViewerMood Mood { get; }
        public ViewerEnergy Energy { get; }
        public ConversationOpenness Openness { get; }

        private readonly HashSet<DayActivityKind> _allowed;

        internal ViewerDailyState(DailyActivity activity, ViewerMood mood, ViewerEnergy energy, ConversationOpenness openness)
        {
            Activity = activity;
            Mood = mood;
            Energy = energy;
            Openness = openness;
            _allowed = ViewerDailyLife.Kinds(activity);
        }

        // What the viewer may say about themselves: the kinds of doings their story today mentions (today and now).
        // Resting and watching (this stream) are always true.
        public bool Allows(DayActivityKind kind) => _allowed.Contains(kind);

        public string Describe() =>
            $"mood {Mood.ToString().ToLowerInvariant()}, energy {Energy.ToString().ToLowerInvariant()}; today you {Activity.Today}; right now you {Activity.Now}. " +
            Openness switch
            {
                ConversationOpenness.Chatty => "You are in the mood to chat and may ask the streamer something back.",
                ConversationOpenness.Reserved => "You keep answers short and plain.",
                _ => "You answer normally."
            };
    }

    public static class ViewerDailyLife
    {
        private const ulong DaySalt = 0x4441594C494645UL;

        // Everyday options for anyone without an authored life (anonymous chatters, promoted viewers), grouped by what they
        // are interested in. Ordinary and gender-neutral in meaning; the model picks the grammatical form.
        private static readonly DailyActivity[] Generic =
        {
            A("gen.work", "worked a long day and only just got home", "resting with the stream on", DayActivityKind.Work, ViewerMood.Tired, ViewerEnergy.Low),
            A("gen.study", "had classes most of the day", "procrastinating on homework with the stream on", DayActivityKind.Study, ViewerMood.Chill, ViewerEnergy.Normal),
            A("gen.games", "played some games with friends in the evening", "taking a break and watching streams", DayActivityKind.Gaming, ViewerMood.Upbeat, ViewerEnergy.High),
            A("gen.errands", "ran errands around the city all day", "finally sitting down", DayActivityKind.Errands, ViewerMood.Tired, ViewerEnergy.Low),
            A("gen.rest", "had a lazy day off and did almost nothing", "lying around with the phone", DayActivityKind.Rest, ViewerMood.Chill, ViewerEnergy.Normal),
            A("gen.house", "cleaned the apartment and did laundry", "eating something and watching the stream", DayActivityKind.Housework, ViewerMood.Good, ViewerEnergy.Normal),
            A("gen.walk", "went for a long walk outside", "warming up at home", DayActivityKind.Walk, ViewerMood.Good, ViewerEnergy.Normal),
            A("gen.tech", "spent the day fixing a friend's computer", "watching streams to unwind", DayActivityKind.Tech, ViewerMood.Good, ViewerEnergy.Normal),
            A("gen.bored", "had a boring, uneventful day", "looking for something to watch", DayActivityKind.Rest, ViewerMood.Bored, ViewerEnergy.Normal),
            A("gen.stress", "had a stressful day at work", "trying to switch off", DayActivityKind.Work, ViewerMood.Stressed, ViewerEnergy.Low)
        };

        private static readonly Dictionary<StreamTopic, string[]> ByInterest = new()
        {
            [StreamTopic.Games] = new[] { "gen.games", "gen.rest", "gen.study", "gen.work" },
            [StreamTopic.Hardware] = new[] { "gen.tech", "gen.work", "gen.games" },
            [StreamTopic.StreamSetup] = new[] { "gen.tech", "gen.rest", "gen.work" },
            [StreamTopic.Money] = new[] { "gen.work", "gen.stress", "gen.errands" },
            [StreamTopic.Life] = new[] { "gen.house", "gen.errands", "gen.walk", "gen.work" },
            [StreamTopic.Community] = new[] { "gen.rest", "gen.bored", "gen.study", "gen.walk" }
        };

        // Permanent viewers keep one story per game day; anonymous chatters one per broadcast.
        public static ViewerDailyState For(ChatParticipant viewer, double gameMinutes, string broadcastId, RelationshipTier tier)
        {
            if (viewer == null) throw new ArgumentNullException(nameof(viewer));
            ViewerProfile profile = viewer.Persona.Profile;
            long day = double.IsNaN(gameMinutes) ? 0 : (long)Math.Floor(gameMinutes / 1440);
            ulong seed = ChatContextBuilder.StableHash(viewer.ViewerId);
            seed = viewer.IsPermanent ? AudienceRandom.Hash(seed, (ulong)day) : AudienceRandom.Hash(seed, ChatContextBuilder.StableHash(broadcastId ?? ""));
            seed = AudienceRandom.Hash(seed, DaySalt);
            IReadOnlyList<DailyActivity> options = Options(profile, viewer.Traits.Interests);
            DailyActivity activity = options[(int)(seed % (ulong)options.Count)];
            float talkativeness = viewer.Traits.Talkativeness;
            ConversationOpenness openness = tier == RelationshipTier.Wary ? ConversationOpenness.Reserved
                : talkativeness >= .6f || tier >= RelationshipTier.Friendly ? ConversationOpenness.Chatty
                : talkativeness < .3f ? ConversationOpenness.Reserved : ConversationOpenness.Normal;
            return new ViewerDailyState(activity, activity.Mood, activity.Energy, openness);
        }

        private static IReadOnlyList<DailyActivity> Options(ViewerProfile profile, StreamTopic interests)
        {
            DailyActivity[] authored = profile?.DailyLife;
            if (authored != null && authored.Length > 0) return authored;
            var options = new List<DailyActivity>();
            foreach (var pair in ByInterest)
                if ((interests & pair.Key) != 0)
                    foreach (string id in pair.Value)
                    {
                        DailyActivity activity = Array.Find(Generic, a => a.Id == id);
                        if (!options.Contains(activity)) options.Add(activity);
                    }
            return options.Count > 0 ? options : Generic;
        }

        // Kinds of doings an authored story mentions, read from its own words, so "slept after the night shift; now on
        // shift" allows talk of both sleep and work while "drew all day" rules out a day at the office.
        private static readonly (DayActivityKind kind, string[] words)[] KindWords =
        {
            (DayActivityKind.Work, new[] { "work", "shift", "office", "job", "courier", "cafe", "guard" }),
            (DayActivityKind.Study, new[] { "class", "school", "universit", "lesson", "lecture", "essay", "exam", "homework", "library", "critique" }),
            (DayActivityKind.Drawing, new[] { "draw", "sketch", "poster", "portfolio", "brush", "doodl", "commission", "art " }),
            (DayActivityKind.Gaming, new[] { "game", "played", "ranked" }),
            (DayActivityKind.Sleep, new[] { "slept", "asleep", "sleep", "nap" }),
            (DayActivityKind.Errands, new[] { "errand", "market", "shop", "clinic", "groceries", "metro", "queue" }),
            (DayActivityKind.Housework, new[] { "clean", "laundry", "tap", "plants", "cook", "soup", "pelmeni" }),
            (DayActivityKind.Walk, new[] { "walk", "photos", "basketball", "outside" }),
            (DayActivityKind.Tech, new[] { "computer", "server", "laptop", "router", "power supply", "forum", "setup" })
        };

        internal static HashSet<DayActivityKind> Kinds(DailyActivity activity)
        {
            var kinds = new HashSet<DayActivityKind> { activity.Kind, DayActivityKind.Rest, DayActivityKind.Watching };
            string story = (" " + activity.Today + " " + activity.Now + " ").ToLowerInvariant();
            foreach (var (kind, words) in KindWords)
                foreach (string word in words)
                    if (story.Contains(word)) kinds.Add(kind);
            return kinds;
        }

        private static DailyActivity A(string id, string today, string now, DayActivityKind kind, ViewerMood mood, ViewerEnergy energy) =>
            new() { Id = id, Today = today, Now = now, Kind = kind, Mood = mood, Energy = energy };
    }
}
