using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace GoLive.Viewers
{
    public enum LetterCase { Lowercase, Normal, CapsWhenExcited }
    public enum Punctuation { None, Light, Full }
    public enum Profanity { None, Mild, Strong }

    // How a permanent viewer types: structured, so C# can build the prompt style per message and enforce limits.
    [Serializable]
    public sealed class ChatStyle
    {
        public int MinimumWords = 2;
        public int MaximumWords = 8;
        public LetterCase Case;
        public Punctuation Punctuation = Punctuation.Light;
        // 0 none, 1 casual, 2 heavy internet slang.
        [Range(0, 2)] public int Slang = 1;
        [Range(0, 1)] public float EmojiRate;
        public Profanity Profanity = Profanity.Mild;
        // Share of messages that end with this viewer's smiley (e.g. ")" or "))"); empty for none.
        public string Smiley = "";
        [Range(0, 1)] public float SmileyRate;
        // How this viewer laughs ("ахах", "лол", "lmao") and how often a message may contain it.
        public string[] Laughter = Array.Empty<string>();
        [Range(0, 1)] public float LaughterRate = .3f;
        // Personal words (e.g. "сынок", "bro"); C# allows them only in this share of messages, so they stay a
        // recognizable habit instead of a tic in every line.
        public string[] Signatures = Array.Empty<string>();
        [Range(0, 1)] public float SignatureRate = .3f;
        [Range(0, 1)] public float QuestionRate = .2f;
        // One short extra note ("never uses exclamation marks", "types full words, no abbreviations").
        public string Notes = "";
    }

    // When a viewer tends to watch (game time) and how regular they are.
    [Serializable]
    public sealed class ScheduleTendency
    {
        // Preferred window in game hours; End may be past midnight (e.g. 22 -> 3).
        [Range(0, 23)] public int StartHour = 18;
        [Range(0, 23)] public int EndHour = 23;
        // Chance to show up to a stream inside the window (outside it is much lower).
        [Range(0, 1)] public float Regularity = .5f;

        public bool Contains(int hour) => StartHour <= EndHour ? hour >= StartHour && hour <= EndHour : hour >= StartHour || hour <= EndHour;
    }

    // A permanent community member's authored identity. Structured data, not a prompt: C# derives reaction traits,
    // presence and the per-message style from it; the persisted relationship/memory refer to Id only.
    [Serializable]
    public sealed class ViewerProfile
    {
        public string Id = "";
        public string DisplayName = "";
        public string[] SpokenNames = Array.Empty<string>();
        public ViewerLanguage Language;
        [TextArea(2, 4)] public string Personality = "";
        public StreamTopic Interests;
        public ScheduleTendency Schedule = new();
        public ChatStyle Style = new();
        [Range(.05f, 1)] public float Talkativeness = .5f;
        // Gap multiplier between two of their messages, and typing/reading delay multiplier.
        [Range(.5f, 4)] public float Pace = 1f;
        [Range(.4f, 2.5f)] public float ResponseSpeed = 1f;
        // Per moment interest: speech, silence, away, started, joined, donation, follow, subscription, milestone, peripheral, chatter.
        public float[] EventAffinity = Array.Empty<float>();
        [Range(0, 1)] public float DonationTendency = .1f;
        [Range(0, 1)] public float FollowTendency = .3f;
        // How readily they answer other viewers (viewer-to-viewer replies are rare regardless).
        [Range(0, 1)] public float SocialTendency = .3f;
        // Where the relationship starts, -100..100 (a grumpy viewer may start below zero).
        [Range(-100, 100)] public int InitialSentiment;

        private static readonly StreamEventKind[] AffinityOrder =
        {
            StreamEventKind.StreamerSpeech, StreamEventKind.StreamerSilence, StreamEventKind.StreamerAway, StreamEventKind.StreamStarted,
            StreamEventKind.ViewerJoined, StreamEventKind.Donation, StreamEventKind.Follow, StreamEventKind.Subscription,
            StreamEventKind.AudienceMilestone, StreamEventKind.PeripheralChanged, StreamEventKind.AudienceChatter
        };
        public static IReadOnlyList<StreamEventKind> AffinityKinds => AffinityOrder;

        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(Id) || !Id.StartsWith("viewer.", StringComparison.Ordinal)) return "Viewer ids start with 'viewer.'.";
            if (string.IsNullOrWhiteSpace(DisplayName) || DisplayName.Length > 32) return $"{Id}: display name must be 1-32 characters.";
            if (string.IsNullOrWhiteSpace(Personality)) return $"{Id}: personality is missing.";
            if (Schedule == null || Style == null) return $"{Id}: schedule and style are required.";
            if (Style.MinimumWords < 1 || Style.MaximumWords < Style.MinimumWords || Style.MaximumWords > 25) return $"{Id}: word range is invalid.";
            if (!(Talkativeness > 0 && Talkativeness <= 1) || !(Pace > 0) || !(ResponseSpeed > 0)) return $"{Id}: reaction tendencies are invalid.";
            if (EventAffinity == null || EventAffinity.Length != AffinityOrder.Length) return $"{Id}: needs {AffinityOrder.Length} event affinities.";
            foreach (float affinity in EventAffinity)
                if (!(affinity >= 0 && affinity <= 3)) return $"{Id}: event affinities must be 0-3.";
            if (InitialSentiment < -100 || InitialSentiment > 100) return $"{Id}: initial sentiment must be -100..100.";
            return null;
        }

        public ReactionTraits Traits()
        {
            var affinity = new Dictionary<StreamEventKind, float>();
            for (int i = 0; i < AffinityOrder.Length && i < EventAffinity.Length; i++) affinity[AffinityOrder[i]] = EventAffinity[i];
            return new ReactionTraits(Talkativeness, Pace, ResponseSpeed, Interests, affinity);
        }

        public ViewerPersona Persona() =>
            new(Language, Personality, BaseStyle(), Style.MinimumWords, Style.MaximumWords, Style.Profanity, this);

        public ChatParticipant Participant() => new(Id, DisplayName, true, Traits(), SpokenNames, Persona());

        // The stable part of the style; per-message habits (signature, smiley, laughter) are added by the prompt builder.
        private string BaseStyle()
        {
            var style = new StringBuilder();
            style.Append(Style.Case switch
            {
                LetterCase.Lowercase => "All lowercase. ",
                LetterCase.CapsWhenExcited => "Mostly lowercase, CAPS when excited. ",
                _ => "Normal capitalization. "
            });
            style.Append(Style.Punctuation switch
            {
                Punctuation.None => "No punctuation at all. ",
                Punctuation.Full => "Full sentences with commas and proper punctuation. ",
                _ => "Light punctuation, no final period. "
            });
            style.Append(Style.Slang switch { 0 => "No slang. ", 2 => "Heavy internet slang. ", _ => "Some casual slang. " });
            style.Append(Style.EmojiRate <= 0 ? "No emoji. " : Style.EmojiRate < .2f ? "An emoji very rarely. " : "Sometimes one emoji. ");
            style.Append(Style.Profanity switch
            {
                Profanity.None => "Never swears. ",
                Profanity.Strong => "Swears casually when it fits. ",
                _ => "Mild words at most, no obscenities. "
            });
            if (Style.QuestionRate >= .35f) style.Append("Often asks the streamer something. ");
            if (!string.IsNullOrWhiteSpace(Style.Notes)) style.Append(Style.Notes.Trim()).Append(' ');
            return style.ToString().Trim();
        }
    }
}
