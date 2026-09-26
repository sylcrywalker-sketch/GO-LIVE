using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using GoLive.Desktop;
using GoLive.PcBuilding;

namespace GoLive.Viewers
{
    // What the selected viewer is socially doing. C# decides it from game state before any text exists; the
    // language model only phrases it. Deliberately small: this is not a dialogue ontology.
    public enum UtteranceIntent { React, Tease, Question, Answer, Concern, Disagree, Acknowledge, Callback, ThankResponse, SilenceCheck, TechnicalComment }
    public enum UtteranceTarget { Streamer, Chat, OtherViewer }
    // Behavioral reading of the hidden sentiment and familiarity. It changes decisions; it is never shown as a meter.
    public enum RelationshipTier { Wary, Neutral, Friendly, Loyal }
    public enum FactSource { Stream, StreamerSaid, OtherViewerSaid, GameFact, Memory, Promise, ViewerDay, OwnLine }

    public readonly struct GroundedFact
    {
        public FactSource Source { get; }
        public string Text { get; }
        // Optional qualifier shown with the fact ("a little earlier", the other viewer's name).
        public string Label { get; }
        public GroundedFact(FactSource source, string text, string label = null) { Source = source; Text = text ?? ""; Label = label; }
    }

    // The bounded factual and social envelope of one generation. Built by C# from real state; never by the model.
    public sealed class ViewerUtterancePlan
    {
        public long IntentId { get; }
        public string ViewerId { get; }
        public UtteranceIntent Intent { get; }
        // For a Callback, the ordinary action it is delivered as (a teasing or a caring callback); else Intent.
        public UtteranceIntent Manner { get; }
        public UtteranceTarget Target { get; }
        public string Topic { get; }
        public IReadOnlyList<GroundedFact> AllowedFacts { get; }
        public string RelevantMemoryId { get; }
        public string RelevantPromiseId { get; }
        public RelationshipTier RelationshipTone { get; }
        public string CurrentEventId { get; }
        public ViewerLanguage Language { get; }
        // The other viewer whose published line this replies to; null for ordinary reactions.
        public string ReplyTarget { get; }
        // The streamer asked which game to play: an answer names a kind of game, never an unverifiable title.
        public bool GameChoice { get; }
        // The streamer asked about the viewer themselves ("как дела", "что делал сегодня"): answered from YOUR DAY.
        public bool Personal { get; }
        // The streamer is replying to this viewer's last message: a turn in their short exchange.
        public bool FollowUp { get; }
        // Lowercase authority for specific streamer/game claims. Soft day stories and generated chat stay
        // available to phrase a reply, but cannot establish streamer hardware, quantities or history.
        internal string GroundedText { get; }

        internal ViewerUtterancePlan(long intentId, string viewerId, UtteranceIntent intent, UtteranceIntent manner, UtteranceTarget target,
            string topic, List<GroundedFact> facts, string memoryId, string promiseId, RelationshipTier tone, string eventId,
            ViewerLanguage language, string replyTarget, string groundedText, bool gameChoice, bool personal = false, bool followUp = false)
        {
            IntentId = intentId; ViewerId = viewerId; Intent = intent; Manner = manner; Target = target; Topic = topic;
            AllowedFacts = facts.AsReadOnly(); RelevantMemoryId = memoryId; RelevantPromiseId = promiseId; RelationshipTone = tone;
            CurrentEventId = eventId; Language = language; ReplyTarget = replyTarget; GroundedText = groundedText; GameChoice = gameChoice;
            Personal = personal; FollowUp = followUp;
        }
    }

    // Deterministic social planning: relationship tier, social action, callback willingness and the allowed facts.
    // All chance is a hash of the reaction id, so a plan never consumes the selector's random stream.
    public static class ViewerUtterancePlanner
    {
        private static readonly int IntentCount = Enum.GetValues(typeof(UtteranceIntent)).Length;
        private const ulong IntentSalt = 0x494E54454E54UL, CallbackSalt = 0x43414C4C4241434BUL;
        public const double CallbackCap = .75;
        // At most one published callback per viewer in this many stream seconds, on top of per-fact cooldowns.
        public const double CallbackGapSeconds = 600;
        // Input-side classification of the streamer's own words (never a check on generated text).
        private static readonly string[] FailureStems =
            { "умер", "умира", "сдох", "проиграл", "провалил", "слил", "потерял", "ошиб", "не тот ", "died ", "dead ", "lost ", "failed ", "mistake", "wrong " };
        private static readonly string[] AchievementStems =
            { "выиграл", "победил", "прошел", "затащил", "получилось", "won ", "beat ", "clutch", "did it " };

        public static RelationshipTier Tier(PermanentViewerState state) =>
            state == null ? RelationshipTier.Neutral : Tier(state.Sentiment, state.VisitCount, state.Acknowledgements);

        public static RelationshipTier Tier(int sentiment, int visits, int acknowledgements) =>
            sentiment <= -25 ? RelationshipTier.Wary
            : sentiment >= 60 && (visits >= 5 || acknowledgements >= 3) ? RelationshipTier.Loyal
            : sentiment >= 25 ? RelationshipTier.Friendly : RelationshipTier.Neutral;

        // Bounded prose for the prompt; the tier itself already changed selection, action and callbacks.
        public static string Describe(RelationshipTier tier, int visits, int acknowledgements)
        {
            string familiarity = visits >= 5 || acknowledgements >= 3 ? "You are a regular here and the streamer knows you. "
                : visits > 1 ? "You have watched this streamer before. " : "You are new to this stream. ";
            return tier + ". " + familiarity + tier switch
            {
                RelationshipTier.Wary => "You are not won over: skeptical and dry toward the streamer; no warmth, no reassurance, no affection.",
                RelationshipTier.Friendly => "You like this streamer: warm in your own way, but no gushing and no love declarations.",
                RelationshipTier.Loyal => "You are a loyal regular who backs this streamer and may show you care, in your own style. Still no romance.",
                _ => "No special bond: ordinary for your personality; no affection or loyalty claims."
            } + " Keep your authored personality.";
        }

        // Whether a relevant memory/promise becomes an explicit callback for this reaction. Bounded and sparse:
        // relationship scales the viewer's authored interest; retrieval caps and cooldowns still apply first.
        public static bool WantsCallback(ReactionIntent intent, RelationshipTier tier) =>
            Roll(intent, CallbackSalt) < CallbackChance(intent.Viewer, tier);

        // The runtime and audit decision: relevant candidates only make a callback possible; one fact at most, a
        // known promise before a memory. False leaves the line free of any historical fact.
        public static bool ChooseCallback(ReactionIntent intent, RelationshipTier tier, IReadOnlyList<ViewerMemory> relevant,
            ViewerPromiseContext known, out ViewerMemory memory, out ViewerPromiseContext promise)
        {
            memory = null; promise = null;
            if ((relevant == null || relevant.Count == 0) && known == null || !WantsCallback(intent, tier)) return false;
            if (known != null) promise = known;
            else memory = relevant[0];
            return true;
        }

        public static double CallbackChance(ChatParticipant viewer, RelationshipTier tier)
        {
            double interest = viewer.Persona.Profile?.Habits?.CallbackInterest ?? .3;
            double scale = tier switch { RelationshipTier.Wary => .5, RelationshipTier.Friendly => 1.25, RelationshipTier.Loyal => 1.6, _ => 1 };
            return Math.Min(CallbackCap, interest * scale);
        }

        // One plan per reaction, cached on its situation so the prompt and the validator see the same envelope.
        public static ViewerUtterancePlan For(ReactionIntent intent, ChatSituation situation)
        {
            if (intent == null) throw new ArgumentNullException(nameof(intent));
            if (situation == null) throw new ArgumentNullException(nameof(situation));
            ViewerUtterancePlan cached = situation.Plan;
            if (cached != null && cached.IntentId == intent.Id && cached.ViewerId == intent.Viewer.ViewerId) return cached;
            return situation.Plan = Build(intent, situation);
        }

        // The producer and planner share this gate so irrelevant speech never receives a viewer's day,
        // even before prompt assembly. Mirrors social priority: thanks, personal reply, then greeting.
        internal static bool UsesDailyContext(ReactionIntent intent)
        {
            SpeechAnalysis speech = intent.Event.Kind == StreamEventKind.StreamerSpeech ? intent.Event.Speech : null;
            if (speech == null) return false;
            bool named = speech.MentionedViewerIds.Contains(intent.Viewer.ViewerId);
            bool own = intent.Direct && intent.Event.SubjectViewerId == intent.Viewer.ViewerId;
            if ((named || own) && speech.Has(SpeechCue.Thanks)) return false;
            if (speech.Is(SpeechAct.PersonalQuestion) || intent.FollowUp && SpeechRelevance.ContinuesConversation(speech)) return true;
            return !named && !speech.Has(SpeechCue.Farewell) && speech.Has(SpeechCue.Greeting) && intent.Conversational;
        }

        private static ViewerUtterancePlan Build(ReactionIntent intent, ChatSituation situation)
        {
            StreamEvent e = intent.Event;
            RelationshipTier tier = situation.Tier;
            UtteranceIntent chosen = Choose(intent, situation, out UtteranceTarget target, out string topic, out bool personal);
            UtteranceIntent social = chosen;
            ViewerMemory memory = situation.Memories.Count > 0 ? situation.Memories[0] : null;
            ViewerPromiseContext promise = situation.Promise;
            if (promise != null) memory = null; // at most one callback fact per line
            if (memory != null || promise != null)
            {
                social = UtteranceIntent.Callback;
                target = UtteranceTarget.Streamer;
                topic = promise != null ? "the streamer's promise about " + Subject(promise.Subject) + ", in light of this moment"
                    : Subject(memory.CanonicalSubject) + ", in light of this moment";
            }

            var facts = new List<GroundedFact>(8);
            AddFacts(facts, intent, situation);
            // Soft personal present: only when the moment is about the viewer (asked about themselves, greeted in a small
            // chat, in an exchange with the streamer), never pushed into ordinary reactions.
            if (personal && situation.Day != null) facts.Add(new GroundedFact(FactSource.ViewerDay, situation.Day.Describe()));
            if (intent.FollowUp && OwnLast(intent.Viewer.ViewerId, situation.RecentChat) is string own)
                facts.Add(new GroundedFact(FactSource.OwnLine, ChatContextBuilder.Clean(own, ChatContextBuilder.QuoteLimit)));
            if (memory != null) facts.Add(new GroundedFact(FactSource.Memory, MemoryFact(memory, situation.GameMinutes)));
            if (promise != null) facts.Add(new GroundedFact(FactSource.Promise, PromiseFact(promise, situation.GameMinutes)));

            var grounded = new StringBuilder(512);
            grounded.Append(intent.Viewer.DisplayName).Append(' ').Append(e.SubjectName).Append(' ');
            foreach (GroundedFact fact in facts)
                if (fact.Source != FactSource.ViewerDay && fact.Source != FactSource.OwnLine && fact.Source != FactSource.OtherViewerSaid)
                    grounded.Append(fact.Label).Append(' ').Append(fact.Text).Append('\n');
            foreach (StreamChatMessage line in situation.RecentChat) grounded.Append(line.SenderName).Append('\n');
            return new ViewerUtterancePlan(intent.Id, intent.Viewer.ViewerId, social, chosen, target, topic, facts, memory?.MemoryId, promise?.Id,
                tier, e.Key, intent.Viewer.Persona.Language, e.Kind == StreamEventKind.ViewerReply ? e.SubjectName : null,
                grounded.ToString().ToLowerInvariant(),
                e.Speech != null && e.Speech.Has(SpeechCue.Question) && (e.Speech.Topics & StreamTopic.Games) != 0, personal, intent.FollowUp);
        }

        private static string OwnLast(string viewerId, IReadOnlyList<StreamChatMessage> chat)
        {
            for (int i = chat.Count - 1; i >= 0; i--)
                if (chat[i].ViewerId == viewerId) return chat[i].Text;
            return null;
        }

        // Candidate actions come from what actually happened; authored habits and the relationship reweight them.
        private static UtteranceIntent Choose(ReactionIntent intent, ChatSituation situation, out UtteranceTarget target, out string topic, out bool personal)
        {
            StreamEvent e = intent.Event;
            var w = new float[IntentCount];
            target = UtteranceTarget.Streamer;
            personal = UsesDailyContext(intent);
            bool own = intent.Direct && e.SubjectViewerId == intent.Viewer.ViewerId;
            string name = ChatContextBuilder.Clean(e.SubjectName ?? "someone", 32);
            switch (e.Kind)
            {
                case StreamEventKind.StreamerSpeech when e.Speech != null:
                {
                    SpeechAnalysis speech = e.Speech;
                    bool named = speech.MentionedViewerIds.Contains(intent.Viewer.ViewerId);
                    string phrase = " " + SpeechRelevance.Normalize(speech.Text) + " ";
                    bool setup = (speech.Topics & (StreamTopic.Hardware | StreamTopic.StreamSetup)) != 0;
                    bool toYou = named || intent.FollowUp || speech.SingularAddress;
                    if ((named || own) && speech.Has(SpeechCue.Thanks)) { Set(w, UtteranceIntent.ThankResponse, 1); topic = "the streamer thanking you"; }
                    // A question about the viewers themselves outranks the greeting it came with ("всем привет, как дела?").
                    else if (speech.Is(SpeechAct.PersonalQuestion))
                    {
                        Set(w, UtteranceIntent.Answer, 5); Set(w, UtteranceIntent.Question, .4f);
                        topic = toYou ? "the streamer asking you personally how you are or what you did" : "the streamer asking the chat how they are and what they did today";
                    }
                    else if (intent.FollowUp && SpeechRelevance.ContinuesConversation(speech) && speech.AsksForAnswer)
                    {
                        Set(w, UtteranceIntent.Answer, 5); Set(w, UtteranceIntent.Tease, .3f);
                        topic = "the streamer's question in reply to your message";
                    }
                    else if (intent.FollowUp && SpeechRelevance.ContinuesConversation(speech))
                    {
                        Set(w, UtteranceIntent.React, 3); Set(w, UtteranceIntent.Answer, 1); Set(w, UtteranceIntent.Question, .6f);
                        topic = "the streamer's reply to your message";
                    }
                    else if (speech.Is(SpeechAct.OpinionRequest) || speech.Is(SpeechAct.GameplayQuestion))
                    {
                        Set(w, UtteranceIntent.Answer, 4); Set(w, UtteranceIntent.Question, .5f); Set(w, UtteranceIntent.Tease, .3f);
                        Set(w, UtteranceIntent.Disagree, .3f); if (setup) Set(w, UtteranceIntent.TechnicalComment, .8f);
                        topic = speech.Is(SpeechAct.GameplayQuestion) ? "the streamer asking what to play" : "the streamer asking for your opinion";
                    }
                    else if (named && speech.Has(SpeechCue.Question))
                    {
                        Set(w, UtteranceIntent.Answer, 3); Set(w, UtteranceIntent.Question, .6f); Set(w, UtteranceIntent.Tease, .5f);
                        topic = "the streamer's question to you";
                    }
                    else if (named)
                    {
                        Set(w, UtteranceIntent.Acknowledge, 2); Set(w, UtteranceIntent.React, 1); Set(w, UtteranceIntent.Tease, .5f);
                        topic = "the streamer talking to you";
                    }
                    else if (speech.Has(SpeechCue.Farewell)) { Set(w, UtteranceIntent.Acknowledge, 3); Set(w, UtteranceIntent.React, .5f); topic = "the streamer saying goodbye"; }
                    else if (speech.Has(SpeechCue.Greeting))
                    {
                        Set(w, UtteranceIntent.Acknowledge, 3); Set(w, UtteranceIntent.React, .5f);
                        // Greeted in a small chat, a person greets back and may add a word about themselves.
                        topic = "the streamer's greeting";
                    }
                    else if (speech.Has(SpeechCue.Question))
                    {
                        Set(w, UtteranceIntent.Answer, 3); Set(w, UtteranceIntent.Question, .8f); Set(w, UtteranceIntent.Tease, .4f);
                        Set(w, UtteranceIntent.Disagree, .4f); if (setup) Set(w, UtteranceIntent.TechnicalComment, .8f);
                        topic = "the streamer's question to the chat";
                    }
                    else if (Mentions(phrase, FailureStems))
                    {
                        Set(w, UtteranceIntent.React, 2); Set(w, UtteranceIntent.Tease, 1); Set(w, UtteranceIntent.Concern, 1); Set(w, UtteranceIntent.Question, .4f);
                        Set(w, UtteranceIntent.Disagree, .2f);
                        topic = "the failure the streamer just described";
                    }
                    else if (Mentions(phrase, AchievementStems))
                    {
                        Set(w, UtteranceIntent.React, 3); Set(w, UtteranceIntent.Question, .8f); Set(w, UtteranceIntent.Tease, .3f); Set(w, UtteranceIntent.Disagree, .2f);
                        topic = "what the streamer says they just pulled off";
                    }
                    else if (speech.Has(SpeechCue.Filler)) { Set(w, UtteranceIntent.React, 1); topic = "the streamer's filler words (nothing happened)"; }
                    else if (setup)
                    {
                        Set(w, UtteranceIntent.React, 2); Set(w, UtteranceIntent.TechnicalComment, 1); Set(w, UtteranceIntent.Question, 1);
                        topic = "the streamer's talk about their setup";
                    }
                    else
                    {
                        Set(w, UtteranceIntent.React, 3); Set(w, UtteranceIntent.Question, .6f); Set(w, UtteranceIntent.Tease, .4f); Set(w, UtteranceIntent.Disagree, .2f);
                        topic = "what the streamer just said";
                    }
                    break;
                }
                case StreamEventKind.StreamerSilence:
                    Set(w, UtteranceIntent.SilenceCheck, 3); Set(w, UtteranceIntent.Concern, .5f); Set(w, UtteranceIntent.Tease, .4f);
                    topic = "the streamer's continuing silence";
                    break;
                case StreamEventKind.StreamerAway:
                    Set(w, UtteranceIntent.SilenceCheck, 2); Set(w, UtteranceIntent.React, 1); Set(w, UtteranceIntent.Tease, .3f);
                    topic = "the streamer being away from the desk";
                    break;
                case StreamEventKind.PeripheralChanged:
                {
                    string device = e.Peripheral == PcPeripheralKind.Microphone ? "microphone" : "webcam";
                    if (e.Connected) { Set(w, UtteranceIntent.React, 3); Set(w, UtteranceIntent.TechnicalComment, .6f); topic = "the streamer's " + device + " working again"; }
                    else
                    {
                        Set(w, UtteranceIntent.React, 2); Set(w, UtteranceIntent.Question, 1); Set(w, UtteranceIntent.TechnicalComment, 1.2f);
                        Set(w, UtteranceIntent.Concern, .5f); topic = "the streamer's " + device + " cutting out";
                    }
                    break;
                }
                case StreamEventKind.Donation:
                    if (own) { Set(w, UtteranceIntent.React, 1); topic = "your own donation"; }
                    else { Set(w, UtteranceIntent.React, 3); Set(w, UtteranceIntent.Acknowledge, .5f); topic = name + "'s donation"; }
                    break;
                case StreamEventKind.Follow:
                case StreamEventKind.Subscription:
                    Set(w, UtteranceIntent.React, 1);
                    topic = own ? "your own " + (e.Kind == StreamEventKind.Follow ? "follow" : "subscription") : "a new " + (e.Kind == StreamEventKind.Follow ? "follower" : "subscriber");
                    break;
                case StreamEventKind.ViewerJoined:
                    if (own) { Set(w, UtteranceIntent.Acknowledge, 1); topic = "you arriving on the stream"; }
                    else { Set(w, UtteranceIntent.React, 1); Set(w, UtteranceIntent.Acknowledge, 1); topic = name + " arriving"; }
                    break;
                case StreamEventKind.AudienceMilestone: Set(w, UtteranceIntent.React, 3); topic = "the audience growing"; break;
                case StreamEventKind.StreamStarted: Set(w, UtteranceIntent.Acknowledge, 3); Set(w, UtteranceIntent.React, 1); topic = "the stream starting"; break;
                case StreamEventKind.AudienceChatter:
                    Set(w, UtteranceIntent.React, 2); Set(w, UtteranceIntent.Question, 1);
                    topic = situation.RecentSpeech != null ? "what the streamer said a little earlier" : "the stream right now";
                    break;
                case StreamEventKind.ViewerReply:
                    Set(w, UtteranceIntent.React, 2); Set(w, UtteranceIntent.Disagree, 1); Set(w, UtteranceIntent.Tease, .5f); Set(w, UtteranceIntent.Question, .5f);
                    target = UtteranceTarget.OtherViewer;
                    topic = name + "'s message about the streamer";
                    break;
                default: Set(w, UtteranceIntent.React, 1); topic = "what just happened"; break;
            }

            ViewerProfile profile = intent.Viewer.Persona.Profile;
            SocialHabits habits = profile?.Habits;
            RelationshipTier tier = situation.Tier;
            for (int i = 0; i < IntentCount; i++)
            {
                if (w[i] <= 0) continue;
                var candidate = (UtteranceIntent)i;
                if (habits != null && habits.Avoid(candidate)) { w[i] = 0; continue; }
                if (habits != null && habits.Prefer(candidate)) w[i] *= 3;
                // Teasing is an authored disposition and technical remarks come from people who care about the
                // setup; anyone else would be forcing a persona trait or a hardware theme into the moment.
                else if (candidate == UtteranceIntent.Tease) w[i] *= profile == null ? .5f : 0;
                else if (candidate == UtteranceIntent.TechnicalComment)
                    w[i] *= profile == null ? .3f : (profile.Interests & (StreamTopic.Hardware | StreamTopic.StreamSetup)) != 0 ? 1 : 0;
                w[i] *= TierWeight(tier, candidate);
            }
            // Someone who likes the streamer pushes back on a skeptic's dig instead of joining it.
            if (e.Kind == StreamEventKind.ViewerReply && situation.ReplyTargetTier == RelationshipTier.Wary && tier >= RelationshipTier.Friendly)
                w[(int)UtteranceIntent.Disagree] *= 4;

            double total = 0;
            foreach (float weight in w) total += weight;
            if (total <= 0) return UtteranceIntent.React;
            double roll = Roll(intent, IntentSalt) * total;
            for (int i = 0; i < IntentCount; i++)
            {
                if (w[i] <= 0) continue;
                if (roll < w[i]) return (UtteranceIntent)i;
                roll -= w[i];
            }
            return UtteranceIntent.React;
        }

        private static float TierWeight(RelationshipTier tier, UtteranceIntent intent) => tier switch
        {
            RelationshipTier.Wary => intent switch
            {
                UtteranceIntent.Concern => 0, UtteranceIntent.Acknowledge => .6f, UtteranceIntent.Answer => .7f, UtteranceIntent.Question => .5f,
                UtteranceIntent.Tease => 1.5f, UtteranceIntent.Disagree => 2f, _ => 1
            },
            // Warmer viewers answer and keep the exchange going (a question back) more often.
            RelationshipTier.Friendly => intent switch
            {
                UtteranceIntent.Answer => 1.2f, UtteranceIntent.Question => 1.5f, UtteranceIntent.Concern => 1.2f, UtteranceIntent.Disagree => .5f, _ => 1
            },
            RelationshipTier.Loyal => intent switch
            {
                UtteranceIntent.Answer => 1.3f, UtteranceIntent.Question => 1.6f, UtteranceIntent.Concern => 1.5f, UtteranceIntent.Disagree => .3f,
                UtteranceIntent.Tease => .8f, _ => 1
            },
            _ => 1
        };

        private static void AddFacts(List<GroundedFact> facts, ReactionIntent intent, ChatSituation situation)
        {
            StreamEvent e = intent.Event;
            bool own = intent.Direct && e.SubjectViewerId == intent.Viewer.ViewerId;
            string name = ChatContextBuilder.Clean(e.SubjectName ?? "someone", 32);
            var stream = new StringBuilder("Stream: live for ").Append(Minutes(situation.StreamSeconds)).Append(", ")
                .Append(situation.Viewers == 1 ? "1 viewer" : situation.Viewers.ToString(CultureInfo.InvariantCulture) + " viewers").Append(" watching");
            if ((situation.Content & StreamTopic.Games) != 0) stream.Append("; the streamer is playing a game (its title is unknown unless they say it)");
            else if (situation.Content != StreamTopic.None) stream.Append("; just chatting");
            facts.Add(new GroundedFact(FactSource.Stream, stream.Append(". Nobody knows when it will end.").ToString()));
            switch (e.Kind)
            {
                case StreamEventKind.StreamerSpeech when e.Speech != null:
                {
                    SpeechAnalysis speech = e.Speech;
                    string phrase = " " + SpeechRelevance.Normalize(speech.Text) + " ";
                    facts.Add(new GroundedFact(FactSource.StreamerSaid, ChatContextBuilder.Clean(speech.Text, ChatContextBuilder.QuoteLimit)));
                    bool named = speech.MentionedViewerIds.Contains(intent.Viewer.ViewerId);
                    if (named && speech.Has(SpeechCue.Thanks)) facts.Add(Fact("They are thanking you by name; what for is not said."));
                    else if (named) facts.Add(Fact("They said your name: they are talking to you."));
                    else if (own) facts.Add(Fact("They are thanking you for your donation a moment ago."));
                    else if (speech.Has(SpeechCue.AddressesChat)) facts.Add(Fact("They are asking the chat."));
                    if (speech.Has(SpeechCue.Filler)) facts.Add(Fact("Those were filler words; nothing notable happened."));
                    if (phrase.Contains(" я ") || phrase.Contains(" i ") || phrase.Contains(" у меня "))
                        facts.Add(Fact("The streamer is talking about themselves: it happened to them, not to you."));
                    if ((speech.Topics & (StreamTopic.Hardware | StreamTopic.StreamSetup)) == 0) facts.Add(SetupUnremarkable);
                    else if ((speech.Topics & StreamTopic.Hardware) != 0 && situation.Promise == null && situation.CallbackCandidates == null)
                        facts.Add(Fact("You know of no purchase, upgrade or hardware change."));
                    break;
                }
                case StreamEventKind.StreamerSilence:
                    facts.Add(SetupUnremarkable);
                    facts.Add(Fact(e.Silence == SilenceLevel.VeryLong
                        ? $"The streamer has not said a word for about {Math.Max(3, (int)(e.Seconds / 60))} minutes and is still silent right now; why is unknown."
                        : "The streamer has been quiet for over a minute and is still quiet right now; why is unknown."));
                    break;
                case StreamEventKind.StreamerAway:
                    facts.Add(Fact("The streamer left the desk; the stream shows a frozen picture and nobody is talking; where they went is unknown."));
                    break;
                case StreamEventKind.PeripheralChanged:
                    // The cause is the fact itself (unplugged/switched off); personas must not diagnose another one.
                    facts.Add(Fact(e.Peripheral == PcPeripheralKind.Microphone
                        ? e.Connected ? "The streamer plugged their microphone back in; their voice sounds clear again."
                            : "The streamer's microphone (theirs, not yours) was just unplugged, so their voice now sounds much worse."
                        : e.Connected ? "The streamer's webcam just turned on." : "The streamer's webcam was just switched off."));
                    break;
                case StreamEventKind.ViewerJoined:
                    facts.Add(Fact(own ? "You just opened the stream." : $"The viewer {name} just came into the chat."));
                    break;
                case StreamEventKind.Donation:
                    facts.Add(Fact(own
                        ? $"You just donated {ChatContextBuilder.Money(e.AmountCents)} to the streamer. Your line goes with the donation: a short comment, joke, question or request about the stream (do not state the amount, do not thank yourself)."
                        : $"{name} just donated {ChatContextBuilder.Money(e.AmountCents)} to the streamer."));
                    break;
                case StreamEventKind.Follow:
                    facts.Add(Fact(own ? "You just followed the channel." : "Someone in the chat just followed the channel."));
                    break;
                case StreamEventKind.Subscription:
                    facts.Add(Fact(own ? "You just subscribed to the channel." : $"{name} just subscribed to the channel."));
                    break;
                case StreamEventKind.AudienceMilestone:
                    facts.Add(Fact($"There are now {e.AudienceSize} people watching" + (e.Significance >= .7f ? ", more than this channel ever had." : ".")));
                    break;
                case StreamEventKind.StreamStarted:
                    facts.Add(Fact("The stream just went live."));
                    break;
                case StreamEventKind.AudienceChatter:
                    facts.Add(Fact("Nothing special is happening right now."));
                    if (situation.RecentSpeech != null)
                        facts.Add(new GroundedFact(FactSource.StreamerSaid, ChatContextBuilder.Clean(situation.RecentSpeech, ChatContextBuilder.QuoteLimit), "a little earlier"));
                    break;
                case StreamEventKind.ViewerReply:
                    facts.Add(new GroundedFact(FactSource.OtherViewerSaid, ChatContextBuilder.Clean(e.TriggeringLine, ChatContextBuilder.QuoteLimit), name));
                    break;
            }
        }

        private static GroundedFact Fact(string text) => new(FactSource.GameFact, text);

        // Technical personas otherwise diagnose problems nobody reported; a peripheral event states its own fact.
        private static readonly GroundedFact SetupUnremarkable =
            Fact("No picture, sound, PC or settings problem has been reported; the hardware is unknown.");

        // Canonical fact, labelled for knowledge source, with the only time phrase the game can back.
        internal static string MemoryFact(ViewerMemory memory, double now)
        {
            string when = When(memory.CreatedGameMinutes, now);
            string subject = Subject(memory.CanonicalSubject);
            string meaning = memory.Kind switch
            {
                ViewerMemoryKind.PersonalAcknowledgement => "the streamer thanked you by name",
                ViewerMemoryKind.TechnicalIncident => $"you saw the streamer's {memory.CanonicalSubject} cut out during a stream",
                ViewerMemoryKind.ReportedFailure => "the streamer told the chat " + memory.CanonicalSubject switch
                {
                    "final" => "they lost a tournament final", "boss" => "they lost to a boss", "game-session" => "they failed in a game",
                    _ => "about a failure with " + subject
                },
                _ => "the streamer told the chat " + memory.CanonicalSubject switch
                {
                    "final" => "they won a tournament final", "boss" => "they beat a boss", "game-session" => "they won in a game",
                    _ => "about a success with " + subject
                }
            };
            string source = memory.KnowledgeSource == MemoryKnowledgeSource.HeardStreamer
                ? " You only heard them say it; you did not see it. If you bring it up, it is something they said, not something you saw."
                : "";
            return $"{memory.Kind}; subject={memory.CanonicalSubject}; knowledge={memory.KnowledgeSource}. Meaning: {when}, {meaning}.{source}";
        }

        internal static string PromiseFact(ViewerPromiseContext promise, double now)
        {
            string action = promise.Action switch
            {
                PromiseAction.Purchase => "to buy " + Subject(promise.Subject),
                PromiseAction.Install => "to install " + Subject(promise.Subject),
                _ => "to start the next stream earlier"
            };
            string outcome = promise.Status switch
            {
                PromiseStatus.Fulfilled => "you saw that they did it",
                PromiseStatus.Broken => "you saw the deadline pass without it",
                PromiseStatus.Expired => "it was never settled",
                _ => "you do not know yet whether they did it"
            };
            string unknown = promise.Action == PromiseAction.StartBroadcast ? "" : " Which model, brand or price is not known.";
            string when = When(promise.CreatedGameMinutes, now);
            return $"action={promise.Action}; subject={promise.Subject}; known state={promise.Status}. Meaning: {when}, the streamer promised {action}, and {outcome}.{unknown}";
        }

        internal static string Subject(string canonical) => canonical switch
        {
            "gpu" => "a graphics card", "microphone" => "the microphone", "webcam" => "the webcam", "final" => "a tournament final",
            "boss" => "a boss fight", "game-session" => "a game", "personal-acknowledgement" => "their thanks to you",
            "stream-start" => "the stream start time", _ => "it"
        };

        private static string When(double created, double now)
        {
            if (double.IsNaN(now) || now < created) return "earlier";
            int days = (int)(Math.Floor(now / 1440) - Math.Floor(created / 1440));
            return days <= 0 ? "earlier today" : days == 1 ? "yesterday" : days < 7 ? "a few days ago" : days < 14 ? "about a week ago" : "a while ago";
        }

        private static string Minutes(double seconds)
        {
            int minutes = (int)(seconds / 60);
            return minutes < 1 ? "less than a minute" : minutes == 1 ? "1 minute" : minutes.ToString(CultureInfo.InvariantCulture) + " minutes";
        }

        private static void Set(float[] weights, UtteranceIntent intent, float weight) => weights[(int)intent] = weight;

        private static bool Mentions(string phrase, string[] stems)
        {
            foreach (string stem in stems)
                if (phrase.Contains(" " + stem)) return true;
            return false;
        }

        private static double Roll(ReactionIntent intent, ulong salt)
        {
            ulong seed = AudienceRandom.Hash((ulong)intent.Id, ChatContextBuilder.StableHash(intent.Viewer.ViewerId + "|" + intent.Event.Key));
            return (AudienceRandom.Hash(seed, salt) >> 11) * (1.0 / 9007199254740992.0);
        }
    }
}
