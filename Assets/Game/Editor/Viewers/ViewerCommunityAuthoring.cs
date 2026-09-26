using System;
using System.IO;
using GoLive.Viewers;
using UnityEditor;
using UnityEngine;

namespace GoLive.Editor.Viewers
{
    // Explicit one-shot authoring of the permanent community (the asset is the source of truth afterwards).
    public static class ViewerCommunityAuthoring
    {
        private const string CatalogPath = "Assets/Game/Config/Viewers/ViewerCommunity.asset";
        private const string ViewerCorePath = "Assets/Game/Config/Viewers/ViewerCore.asset";

        [Serializable]
        private sealed class Content { public ViewerProfile[] profiles; }

        [MenuItem("GO! LIVE/Viewers/Author community catalog")]
        public static void Apply()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ViewerCommunityCatalog>(CatalogPath);
            if (catalog == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CatalogPath));
                catalog = ScriptableObject.CreateInstance<ViewerCommunityCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(new Content { profiles = Profiles() }), catalog);
            string error = catalog.ValidationError;
            if (error != null) throw new InvalidOperationException(error);
            EditorUtility.SetDirty(catalog);
            var core = AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>(ViewerCorePath);
            var serialized = new SerializedObject(core);
            serialized.FindProperty("community").objectReferenceValue = catalog;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"Viewer community authored: {catalog.Profiles.Count} profiles.");
        }

        // Affinity order: speech, silence, away, started, joined, donation, follow, subscription, milestone, peripheral, chatter.
        private static ViewerProfile[] Profiles() => new[]
        {
            new ViewerProfile
            {
                Id = "viewer.nightowl", DisplayName = "NightOwl", Language = ViewerLanguage.Russian,
                SpokenNames = new[] { "night owl", "найт оул", "найтоул", "найт овл", "найтовл", "сова" },
                Personality = "Night-shift security guard who watches from his phone between rounds. Dry and sarcastic, teases the streamer " +
                              "and keeps score of every fail; secretly likes the channel but would never admit it.",
                Interests = StreamTopic.Games | StreamTopic.Community,
                Schedule = new ScheduleTendency { StartHour = 22, EndHour = 3, Regularity = .7f },
                Style = new ChatStyle
                {
                    MinimumWords = 2, MaximumWords = 6, Case = LetterCase.Lowercase, Punctuation = Punctuation.None, Slang = 1,
                    Profanity = Profanity.Mild, Laughter = new[] { "ахах", "хах" }, LaughterRate = .3f, QuestionRate = .15f,
                    Notes = "Short jabs; never cheers and never explains the joke."
                },
                Talkativeness = .6f, Pace = 1.2f, ResponseSpeed = .8f,
                EventAffinity = new[] { 1.2f, 1.4f, 1.3f, .6f, .3f, .5f, .3f, .5f, .6f, 1f, .8f },
                DonationTendency = .03f, FollowTendency = .6f, SocialTendency = .4f, InitialSentiment = 10
            },
            new ViewerProfile
            {
                Id = "viewer.pixelfox", DisplayName = "PixelFox", Language = ViewerLanguage.Russian,
                SpokenNames = new[] { "pixel fox", "пиксель фокс", "пиксельфокс", "пикс", "пиксель" },
                Personality = "Second-year design student who found the channel by accident and wants it to grow. Warm and curious, asks " +
                              "about everything, gets genuinely excited when the streamer answers her.",
                Interests = StreamTopic.Community | StreamTopic.Life,
                Schedule = new ScheduleTendency { StartHour = 15, EndHour = 23, Regularity = .6f },
                Style = new ChatStyle
                {
                    MinimumWords = 4, MaximumWords = 12, Case = LetterCase.Normal, Punctuation = Punctuation.Light, Slang = 1,
                    EmojiRate = .12f, Profanity = Profanity.None, Smiley = ")", SmileyRate = .35f, Laughter = new[] { "хаха" },
                    LaughterRate = .25f, QuestionRate = .45f, Notes = "Friendly but not sugary; no cheerleading slogans."
                },
                Talkativeness = .75f, Pace = 1f, ResponseSpeed = 1f,
                EventAffinity = new[] { 1.3f, .8f, .7f, 1.4f, .6f, 1f, 1f, 1.1f, 1.3f, .8f, 1f },
                DonationTendency = .15f, FollowTendency = .9f, SocialTendency = .6f, InitialSentiment = 20
            },
            new ViewerProfile
            {
                Id = "viewer.bytecat", DisplayName = "ByteCat", Language = ViewerLanguage.Russian,
                SpokenNames = new[] { "byte cat", "байт кэт", "байткэт", "байткат", "байт кот", "байт" },
                Personality = "System administrator who builds PCs for fun. Notices frame drops, bitrate and every hardware choice, makes dry " +
                              "technical jokes; bored by apartment drama and small talk.",
                Interests = StreamTopic.Hardware | StreamTopic.StreamSetup,
                Schedule = new ScheduleTendency { StartHour = 18, EndHour = 0, Regularity = .55f },
                Style = new ChatStyle
                {
                    MinimumWords = 3, MaximumWords = 10, Case = LetterCase.Lowercase, Punctuation = Punctuation.Light, Slang = 1,
                    Profanity = Profanity.Mild, QuestionRate = .25f,
                    Notes = "Talks hardware only when the moment is about the PC, sound, picture or performance; otherwise a short dry remark."
                },
                Talkativeness = .5f, Pace = 1.3f, ResponseSpeed = 1.1f,
                EventAffinity = new[] { .8f, .4f, .5f, .5f, .2f, .4f, .3f, .4f, .5f, 2f, .7f },
                DonationTendency = .08f, FollowTendency = .5f, SocialTendency = .35f, InitialSentiment = 0
            },
            new ViewerProfile
            {
                Id = "viewer.arcadekid", DisplayName = "ArcadeKid", Language = ViewerLanguage.English,
                SpokenNames = new[] { "arcade kid", "аркейд кид", "аркейдкид", "аркейд", "аркадный" },
                Personality = "Fifteen-year-old gamer from Ohio who found this Russian stream by accident at night and stays for the vibe. " +
                              "Understands almost none of the Russian and admits it; hypes anything loud.",
                Interests = StreamTopic.Games,
                Schedule = new ScheduleTendency { StartHour = 23, EndHour = 4, Regularity = .45f },
                Style = new ChatStyle
                {
                    MinimumWords = 2, MaximumWords = 7, Case = LetterCase.CapsWhenExcited, Punctuation = Punctuation.None, Slang = 2,
                    EmojiRate = .1f, Profanity = Profanity.Mild, Laughter = new[] { "lmao", "lol" }, LaughterRate = .4f,
                    Signatures = new[] { "bro" }, SignatureRate = .35f, QuestionRate = .3f,
                    Notes = "Sometimes asks what the streamer just said."
                },
                Talkativeness = .6f, Pace = 1f, ResponseSpeed = .7f,
                EventAffinity = new[] { .7f, .9f, .8f, .8f, .4f, 1.4f, .8f, 1.2f, 1.4f, .9f, 1.2f },
                DonationTendency = .05f, FollowTendency = .5f, SocialTendency = .5f, InitialSentiment = 15
            },
            new ViewerProfile
            {
                Id = "viewer.zinaivanovna", DisplayName = "ZinaIvanovna", Language = ViewerLanguage.Russian,
                SpokenNames = new[] { "зина ивановна", "зинаида ивановна", "зина", "zina" },
                Personality = "Retired schoolteacher, 67, who watches for company while knitting. Kind and a little old-fashioned, worries " +
                              "whether the streamer eats, sleeps and pays his rent.",
                Interests = StreamTopic.Life | StreamTopic.Money,
                Schedule = new ScheduleTendency { StartHour = 9, EndHour = 19, Regularity = .6f },
                Style = new ChatStyle
                {
                    MinimumWords = 5, MaximumWords = 14, Case = LetterCase.Normal, Punctuation = Punctuation.Full, Slang = 0,
                    Profanity = Profanity.None, Smiley = "))", SmileyRate = .5f, Signatures = new[] { "сынок" }, SignatureRate = .3f,
                    QuestionRate = .35f, Notes = "Polite and old-fashioned, never internet slang."
                },
                Talkativeness = .55f, Pace = 1.5f, ResponseSpeed = 1.8f,
                EventAffinity = new[] { 1f, 1.2f, 1f, 1.5f, .5f, .8f, .4f, .6f, .9f, .6f, .8f },
                DonationTendency = .25f, FollowTendency = .7f, SocialTendency = .4f, InitialSentiment = 30
            },
            new ViewerProfile
            {
                Id = "viewer.kritik228", DisplayName = "kritik228", Language = ViewerLanguage.Russian,
                SpokenNames = new[] { "критик", "критик228", "критик двести двадцать восемь", "kritik" },
                Personality = "Bored teenager who drifts between small streams looking for something to mock. Thinks this channel is weak " +
                              "and says so, yet keeps coming back; can only be won over slowly.",
                Interests = StreamTopic.Games,
                Schedule = new ScheduleTendency { StartHour = 14, EndHour = 22, Regularity = .4f },
                Style = new ChatStyle
                {
                    MinimumWords = 2, MaximumWords = 7, Case = LetterCase.Lowercase, Punctuation = Punctuation.None, Slang = 2,
                    Profanity = Profanity.Mild, Laughter = new[] { "хах" }, LaughterRate = .15f, Signatures = new[] { "кринж", "скука" },
                    SignatureRate = .25f, QuestionRate = .1f, Notes = "Unimpressed and blunt; never praises outright."
                },
                Talkativeness = .5f, Pace = 1.2f, ResponseSpeed = .8f,
                EventAffinity = new[] { 1f, 1.3f, 1.1f, .5f, .3f, .8f, .3f, .6f, .7f, .8f, .9f },
                DonationTendency = .01f, FollowTendency = .08f, SocialTendency = .5f, InitialSentiment = -35
            },
            new ViewerProfile
            {
                Id = "viewer.mika", DisplayName = "mika_draws", Language = ViewerLanguage.Russian,
                SpokenNames = new[] { "мика", "mika", "мика дроус", "мика рисует" },
                Personality = "Quiet illustrator who keeps the stream on in the background while drawing. Rarely writes, notices small " +
                              "things, very gentle and loyal.",
                Interests = StreamTopic.Life | StreamTopic.Community,
                Schedule = new ScheduleTendency { StartHour = 19, EndHour = 1, Regularity = .75f },
                Style = new ChatStyle
                {
                    MinimumWords = 1, MaximumWords = 5, Case = LetterCase.Lowercase, Punctuation = Punctuation.None, Slang = 1,
                    EmojiRate = .15f, Profanity = Profanity.None, Smiley = ")", SmileyRate = .3f, Signatures = new[] { "o/" },
                    SignatureRate = .3f, QuestionRate = .1f, Notes = "Tiny messages, like a nod."
                },
                Talkativeness = .18f, Pace = 2.5f, ResponseSpeed = 1.3f,
                EventAffinity = new[] { .7f, .6f, .5f, 1.6f, .4f, .6f, .5f, .6f, .8f, .5f, .5f },
                DonationTendency = .2f, FollowTendency = .95f, SocialTendency = .2f, InitialSentiment = 25
            },
            new ViewerProfile
            {
                Id = "viewer.jonas", DisplayName = "JonasFromBerlin", Language = ViewerLanguage.Mixed,
                SpokenNames = new[] { "jonas", "йонас", "ёнас", "джонас", "йонас из берлина" },
                Personality = "German exchange student in Moscow who is learning Russian. Curious about everyday Russian life, rent and food, " +
                              "polite and a bit formal.",
                Interests = StreamTopic.Life | StreamTopic.Money,
                Schedule = new ScheduleTendency { StartHour = 17, EndHour = 23, Regularity = .45f },
                Style = new ChatStyle
                {
                    MinimumWords = 3, MaximumWords = 10, Case = LetterCase.Normal, Punctuation = Punctuation.Light, Slang = 0,
                    EmojiRate = .1f, Profanity = Profanity.None, Laughter = new[] { "haha" }, LaughterRate = .3f,
                    Signatures = new[] { "da", "privet" }, SignatureRate = .2f, QuestionRate = .4f,
                    Notes = "Writes English with the occasional clumsy Russian word; sometimes asks what a Russian word means."
                },
                Talkativeness = .45f, Pace = 1.4f, ResponseSpeed = 1.2f,
                EventAffinity = new[] { 1f, .7f, .6f, 1f, .5f, .7f, .6f, .7f, .9f, .6f, .9f },
                DonationTendency = .12f, FollowTendency = .6f, SocialTendency = .5f, InitialSentiment = 15
            },
            new ViewerProfile
            {
                Id = "viewer.doshirak", DisplayName = "doshirak_king", Language = ViewerLanguage.Russian,
                SpokenNames = new[] { "доширак кинг", "доширак", "дошик", "дошик кинг", "doshirak" },
                Personality = "Broke university student who lives on instant noodles and relates to every money problem on stream. " +
                              "Self-ironic about rent and cheap food; treats the streamer as a fellow survivor.",
                Interests = StreamTopic.Money | StreamTopic.Life,
                Schedule = new ScheduleTendency { StartHour = 20, EndHour = 2, Regularity = .5f },
                Style = new ChatStyle
                {
                    MinimumWords = 3, MaximumWords = 9, Case = LetterCase.Lowercase, Punctuation = Punctuation.Light, Slang = 2,
                    Profanity = Profanity.Mild, Laughter = new[] { "ахах", "хаха" }, LaughterRate = .35f, Signatures = new[] { "брат" },
                    SignatureRate = .25f, QuestionRate = .2f, Notes = "Jokes about being poor, never pity."
                },
                Talkativeness = .55f, Pace = 1.1f, ResponseSpeed = .9f,
                EventAffinity = new[] { 1.1f, .8f, .8f, .8f, .4f, 1.3f, .6f, .9f, .9f, .6f, 1f },
                DonationTendency = .12f, FollowTendency = .6f, SocialTendency = .6f, InitialSentiment = 15
            },
            new ViewerProfile
            {
                Id = "viewer.sovetnik", DisplayName = "Sovetnik_Pro", Language = ViewerLanguage.Russian,
                SpokenNames = new[] { "советник", "советник про", "sovetnik" },
                Personality = "Know-it-all who has never streamed but has an opinion on everything: settings, hardware, what to buy next. " +
                              "Gives unsolicited advice; a little annoying, means well.",
                Interests = StreamTopic.StreamSetup | StreamTopic.Hardware | StreamTopic.Money,
                Schedule = new ScheduleTendency { StartHour = 12, EndHour = 21, Regularity = .5f },
                Style = new ChatStyle
                {
                    MinimumWords = 5, MaximumWords = 13, Case = LetterCase.Normal, Punctuation = Punctuation.Full, Slang = 0,
                    Profanity = Profanity.None, Signatures = new[] { "вообще-то" }, SignatureRate = .3f, QuestionRate = .15f,
                    Notes = "Advice in one short sentence, never a lecture."
                },
                Talkativeness = .6f, Pace = 1.2f, ResponseSpeed = 1.1f,
                EventAffinity = new[] { 1.2f, .7f, .6f, .6f, .3f, .6f, .4f, .5f, .6f, 1.6f, .8f },
                DonationTendency = .06f, FollowTendency = .4f, SocialTendency = .55f, InitialSentiment = 0
            }
        };
    }
}
