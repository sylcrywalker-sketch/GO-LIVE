using System;
using System.Collections.Generic;
using GoLive.PcBuilding;

namespace GoLive.Viewers
{
    // What actually happened on the current broadcast, normalized for the reaction layer. C# decides every fact
    // before any chat text exists; nothing downstream (selection, language model, chat) can create or alter it.
    public enum StreamEventKind
    {
        StreamStarted,      // the broadcast went live
        StreamerSpeech,     // the player's recognized speech
        StreamerSilence,    // the player has been quiet (only measurable while voice recognition is listening)
        StreamerAway,       // the player left the desk while live; the broadcast holds a frozen frame
        ViewerJoined,       // a named viewer started watching
        Donation,           // a donation receipt the donation account accepted
        Follow,             // the audience simulation produced a follow
        Subscription,       // the audience simulation produced a paid subscription
        AudienceMilestone,  // the audience reached a size worth noticing
        PeripheralChanged,  // webcam or in-game microphone connected/disconnected on air
        AudienceChatter,    // the audience simulation's ambient chat impulse: somebody may just talk
        ViewerReply        // one reply to a line actually published, never another reply
    }

    public enum SilenceLevel { None, Long, VeryLong }

    public readonly struct ViewerWitness
    {
        public string ViewerId { get; }
        public long Epoch { get; }
        public ViewerWitness(string viewerId, long epoch) { ViewerId = viewerId; Epoch = epoch; }
    }

    // Immutable observation boundary. Unnamed audience seats stay a count, never permanent identities.
    public sealed class EventWitnesses
    {
        public IReadOnlyList<ViewerWitness> Viewers { get; }
        public int AnonymousSeats { get; }
        private EventWitnesses(List<ViewerWitness> viewers, int anonymousSeats)
        { Viewers = viewers.AsReadOnly(); AnonymousSeats = anonymousSeats; }
        public static EventWitnesses Capture(AudienceRoster roster)
        {
            var viewers = new List<ViewerWitness>(roster.Named.Count + roster.Ephemeral.Count);
            foreach (var viewer in roster.Named) viewers.Add(new ViewerWitness(viewer.ViewerId, roster.Epoch(viewer.ViewerId)));
            foreach (var viewer in roster.Ephemeral) viewers.Add(new ViewerWitness(viewer.ViewerId, roster.Epoch(viewer.ViewerId)));
            return new EventWitnesses(viewers, roster.AnonymousCount);
        }
        public static EventWitnesses Empty() => new(new List<ViewerWitness>(), 0);
        public bool Contains(string id, long epoch = 0)
        {
            foreach (var viewer in Viewers) if (viewer.ViewerId == id && (epoch == 0 || viewer.Epoch == epoch)) return true;
            return false;
        }
    }

    public sealed class StreamEvent
    {
        // Per-broadcast order of normalization.
        public long Serial { get; }
        // Idempotency key: the same fact always has the same key (receipt id, speech sequence, ...).
        public string Key { get; }
        public StreamEventKind Kind { get; }
        public double StreamSeconds { get; }
        // 0..1: how much this deserves the chat's attention, decided by C# rules.
        public float Significance { get; }
        public SpeechAnalysis Speech { get; }
        // The viewer the fact is about (joined, donated, followed); null for the anonymous audience.
        public string SubjectViewerId { get; }
        public string SubjectName { get; }
        public long AmountCents { get; }
        public double Seconds { get; }
        public SilenceLevel Silence { get; }
        public int AudienceSize { get; }
        public PcPeripheralKind Peripheral { get; }
        public bool Connected { get; }
        public EventWitnesses Witnesses { get; }
        public bool HasWitnesses => Witnesses != null;
        public double GameMinutes { get; }
        public string TriggeringLine { get; }
        public int ReplyDepth => Kind == StreamEventKind.ViewerReply ? 1 : 0;
        public bool WitnessedBy(string viewerId, long epoch = 0) => Witnesses?.Contains(viewerId, epoch) == true;

        private StreamEvent(long serial, string key, StreamEventKind kind, double streamSeconds, float significance,
            SpeechAnalysis speech = null, string subjectViewerId = null, string subjectName = null, long amountCents = 0,
            double seconds = 0, SilenceLevel silence = SilenceLevel.None, int audienceSize = 0,
            PcPeripheralKind peripheral = default, bool connected = false, EventWitnesses witnesses = null, double gameMinutes = 0, string triggeringLine = null)
        {
            if (string.IsNullOrEmpty(key)) throw new ArgumentException("A stream event needs an idempotency key.", nameof(key));
            Serial = serial;
            Key = key;
            Kind = kind;
            StreamSeconds = streamSeconds;
            Significance = Math.Clamp(significance, 0f, 1f);
            Speech = speech;
            SubjectViewerId = subjectViewerId;
            SubjectName = subjectName;
            AmountCents = amountCents;
            Seconds = seconds;
            Silence = silence;
            AudienceSize = audienceSize;
            Peripheral = peripheral;
            Connected = connected;
            Witnesses = witnesses;
            GameMinutes = gameMinutes;
            TriggeringLine = triggeringLine;
        }

        // The same fact addressed to a viewer (e.g. the streamer thanks the donor who just gave).
        public StreamEvent WithSubject(string viewerId, string name) =>
            new(Serial, Key, Kind, StreamSeconds, Significance, Speech, viewerId, name, AmountCents, Seconds, Silence, AudienceSize, Peripheral, Connected, Witnesses, GameMinutes, TriggeringLine);

        // Factories remain explicitly unstamped for synthetic tests; production Source.Add always stamps once.
        public StreamEvent WithWitnesses(AudienceRoster roster, double gameMinutes)
        {
            if (HasWitnesses) return this;
            if (double.IsNaN(gameMinutes) || double.IsInfinity(gameMinutes) || gameMinutes < 0) throw new ArgumentOutOfRangeException(nameof(gameMinutes));
            return new StreamEvent(Serial, Key, Kind, StreamSeconds, Significance, Speech, SubjectViewerId, SubjectName, AmountCents,
                Seconds, Silence, AudienceSize, Peripheral, Connected, EventWitnesses.Capture(roster), gameMinutes, TriggeringLine);
        }

        public static StreamEvent Started(long serial, string streamId, double at) =>
            new(serial, streamId + ".started", StreamEventKind.StreamStarted, at, .45f);

        public static StreamEvent StreamerSpeech(long serial, string streamId, double at, SpeechAnalysis speech) =>
            new(serial, streamId + ".speech." + speech.Sequence, StreamEventKind.StreamerSpeech, at, speech.Relevance, speech);

        public static StreamEvent StreamerSilence(long serial, string key, double at, double seconds, SilenceLevel level) =>
            new(serial, key, StreamEventKind.StreamerSilence, at, level == SilenceLevel.VeryLong ? .6f : .3f, seconds: seconds, silence: level);

        public static StreamEvent StreamerAway(long serial, string key, double at, double seconds) =>
            new(serial, key, StreamEventKind.StreamerAway, at, .4f, seconds: seconds);

        public static StreamEvent ViewerJoined(long serial, string streamId, double at, string viewerId, string name, float significance) =>
            new(serial, streamId + ".joined." + viewerId, StreamEventKind.ViewerJoined, at, significance, subjectViewerId: viewerId, subjectName: name);

        public static StreamEvent Donation(long serial, string receiptId, double at, string viewerId, string name, long amountCents) =>
            new(serial, receiptId, StreamEventKind.Donation, at, amountCents >= 1000 ? .95f : amountCents >= 300 ? .85f : .7f,
                subjectViewerId: viewerId, subjectName: name, amountCents: amountCents);

        public static StreamEvent Follow(long serial, string key, double at, string viewerId, string name) =>
            new(serial, key, StreamEventKind.Follow, at, .5f, subjectViewerId: viewerId, subjectName: name);

        public static StreamEvent Subscription(long serial, string key, double at, string viewerId, string name) =>
            new(serial, key, StreamEventKind.Subscription, at, .8f, subjectViewerId: viewerId, subjectName: name);

        public static StreamEvent AudienceMilestone(long serial, string streamId, double at, int audienceSize, bool channelRecord) =>
            new(serial, streamId + ".milestone." + audienceSize, StreamEventKind.AudienceMilestone, at, channelRecord ? .75f : .5f,
                audienceSize: audienceSize);

        public static StreamEvent PeripheralChanged(long serial, string key, double at, PcPeripheralKind kind, bool connected) =>
            new(serial, key, StreamEventKind.PeripheralChanged, at, kind == PcPeripheralKind.Microphone ? .8f : .55f,
                peripheral: kind, connected: connected);

        public static StreamEvent AudienceChatter(long serial, string key, double at, int audienceSize) =>
            new(serial, key, StreamEventKind.AudienceChatter, at, .2f, audienceSize: audienceSize);

        internal static StreamEvent Reply(StreamChatMessage message, double now, AudienceRoster roster, double gameMinutes) =>
            new(0, "reply." + message.Id, StreamEventKind.ViewerReply, now, .25f, subjectViewerId: message.ViewerId,
                subjectName: message.SenderName, witnesses: EventWitnesses.Capture(roster), gameMinutes: gameMinutes,
                triggeringLine: ChatContextBuilder.Clean(message.Text, ChatContextBuilder.QuoteLimit));
    }
}
