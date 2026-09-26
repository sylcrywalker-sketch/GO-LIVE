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
        // Social habits separate voices that collided in the blind review (sarcastic, gentle and technical Russian
        // viewers) by what they do and notice, not by extra signature words. Others keep ordinary defaults.
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
                Habits = new SocialHabits { Prefers = new[] { UtteranceIntent.Tease, UtteranceIntent.SilenceCheck }, Avoids = new[] { UtteranceIntent.Concern }, CallbackInterest = .55f },
                Talkativeness = .6f, Pace = 1.2f, ResponseSpeed = .8f,
                EventAffinity = new[] { 1.2f, 1.4f, 1.3f, .6f, .3f, .5f, .3f, .5f, .6f, 1f, .8f },
                DonationTendency = .03f, FollowTendency = .6f, SocialTendency = .4f, InitialSentiment = 10,
                DailyLife = new[]
                {
                    Day("owl.after-shift", "slept most of the day after last night's shift", "on a night shift, bored between rounds", DayActivityKind.Sleep, ViewerMood.Tired, ViewerEnergy.Low),
                    Day("owl.errands", "ran errands before the shift", "on a night shift, watching from the phone", DayActivityKind.Errands, ViewerMood.Chill, ViewerEnergy.Normal),
                    Day("owl.day-off", "had a night off and played games at home", "at home tonight, not on shift", DayActivityKind.Gaming, ViewerMood.Good, ViewerEnergy.Normal),
                    Day("owl.tap", "fixed a leaking tap at home before work", "on a night shift, drinking tea in the guard booth", DayActivityKind.Housework, ViewerMood.Chill, ViewerEnergy.Normal)
                }
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
                Habits = new SocialHabits { Prefers = new[] { UtteranceIntent.Question }, Avoids = new[] { UtteranceIntent.Disagree }, CallbackInterest = .35f },
                Talkativeness = .75f, Pace = 1f, ResponseSpeed = 1f,
                EventAffinity = new[] { 1.3f, .8f, .7f, 1.4f, .6f, 1f, 1f, 1.1f, 1.3f, .8f, 1f },
                DonationTendency = .15f, FollowTendency = .9f, SocialTendency = .6f, InitialSentiment = 20,
                DailyLife = new[]
                {
                    Day("fox.critique", "had classes and a long design critique", "finishing a poster for class with the stream on", DayActivityKind.Study, ViewerMood.Tired, ViewerEnergy.Normal),
                    Day("fox.portfolio", "worked on a portfolio project most of the day", "taking a break from the project", DayActivityKind.Drawing, ViewerMood.Good, ViewerEnergy.Normal),
                    Day("fox.library", "studied at the university library, then met a friend", "at home, relaxing", DayActivityKind.Study, ViewerMood.Upbeat, ViewerEnergy.High),
                    Day("fox.photos", "walked around the city taking photos", "editing photos with the stream on", DayActivityKind.Walk, ViewerMood.Upbeat, ViewerEnergy.High)
                }
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
                Habits = new SocialHabits { Prefers = new[] { UtteranceIntent.TechnicalComment }, Avoids = new[] { UtteranceIntent.Concern }, Ignores = StreamTopic.Life, CallbackInterest = .3f },
                Talkativeness = .5f, Pace = 1.3f, ResponseSpeed = 1.1f,
                EventAffinity = new[] { .8f, .4f, .5f, .5f, .2f, .4f, .3f, .4f, .5f, 2f, .7f },
                DonationTendency = .08f, FollowTendency = .5f, SocialTendency = .35f, InitialSentiment = 0,
                DailyLife = new[]
                {
                    Day("cat.migration", "fought a server migration at work that failed twice", "finally home, unwinding", DayActivityKind.Work, ViewerMood.Stressed, ViewerEnergy.Low),
                    Day("cat.psu", "replaced a colleague's broken power supply at work", "tinkering with an old laptop", DayActivityKind.Tech, ViewerMood.Good, ViewerEnergy.Normal),
                    Day("cat.quiet", "had an ordinary shift at work where nothing broke", "watching streams", DayActivityKind.Work, ViewerMood.Chill, ViewerEnergy.Normal),
                    Day("cat.homeserver", "spent the day off reinstalling the home server", "waiting for updates to finish", DayActivityKind.Tech, ViewerMood.Bored, ViewerEnergy.Normal)
                }
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
                DonationTendency = .05f, FollowTendency = .5f, SocialTendency = .5f, InitialSentiment = 15,
                DailyLife = new[]
                {
                    Day("kid.test", "went to school and had a math test", "should be asleep, watching streams instead", DayActivityKind.Study, ViewerMood.Tired, ViewerEnergy.Low),
                    Day("kid.friends", "played games with friends after school", "chilling on the phone", DayActivityKind.Gaming, ViewerMood.Upbeat, ViewerEnergy.High),
                    Day("kid.practice", "had basketball practice after school", "eating snacks and watching", DayActivityKind.Walk, ViewerMood.Good, ViewerEnergy.High),
                    Day("kid.lazy", "had a lazy weekend day", "up late watching streams", DayActivityKind.Rest, ViewerMood.Chill, ViewerEnergy.Normal)
                }
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
                Habits = new SocialHabits { Prefers = new[] { UtteranceIntent.Concern, UtteranceIntent.Question }, Avoids = new[] { UtteranceIntent.Disagree }, CallbackInterest = .45f },
                Talkativeness = .55f, Pace = 1.5f, ResponseSpeed = 1.8f,
                EventAffinity = new[] { 1f, 1.2f, 1f, 1.5f, .5f, .8f, .4f, .6f, .9f, .6f, .8f },
                DonationTendency = .25f, FollowTendency = .7f, SocialTendency = .4f, InitialSentiment = 30,
                DailyLife = new[]
                {
                    Day("zina.market", "went to the market and cooked soup", "knitting with the stream on", DayActivityKind.Errands, ViewerMood.Good, ViewerEnergy.Normal),
                    Day("zina.clinic", "sat in a long queue at the clinic", "resting with tea and knitting", DayActivityKind.Errands, ViewerMood.Tired, ViewerEnergy.Low),
                    Day("zina.flat", "cleaned the flat and watered the plants", "knitting a scarf", DayActivityKind.Housework, ViewerMood.Good, ViewerEnergy.Normal),
                    Day("zina.daughter", "talked on the phone with her daughter for an hour", "knitting and listening to the stream", DayActivityKind.Rest, ViewerMood.Upbeat, ViewerEnergy.Normal)
                }
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
                Habits = new SocialHabits { Prefers = new[] { UtteranceIntent.Disagree }, Avoids = new[] { UtteranceIntent.Concern, UtteranceIntent.Question }, CallbackInterest = .15f },
                Talkativeness = .5f, Pace = 1.2f, ResponseSpeed = .8f,
                EventAffinity = new[] { 1f, 1.3f, 1.1f, .5f, .3f, .8f, .3f, .6f, .7f, .8f, .9f },
                DonationTendency = .01f, FollowTendency = .08f, SocialTendency = .5f, InitialSentiment = -35,
                DailyLife = new[]
                {
                    Day("krit.school", "sat through boring school lessons", "lying on the bed hopping between streams", DayActivityKind.Study, ViewerMood.Bored, ViewerEnergy.Low),
                    Day("krit.ranked", "played ranked games and kept losing", "hopping between small streams", DayActivityKind.Gaming, ViewerMood.Stressed, ViewerEnergy.Normal),
                    Day("krit.nothing", "did nothing all day", "bored, looking for something to watch", DayActivityKind.Rest, ViewerMood.Bored, ViewerEnergy.Low)
                }
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
                Habits = new SocialHabits { Prefers = new[] { UtteranceIntent.Acknowledge }, Avoids = new[] { UtteranceIntent.Question, UtteranceIntent.Disagree }, Ignores = StreamTopic.Hardware | StreamTopic.Money, CallbackInterest = .2f },
                Talkativeness = .18f, Pace = 2.5f, ResponseSpeed = 1.3f,
                EventAffinity = new[] { .7f, .6f, .5f, 1.6f, .4f, .6f, .5f, .6f, .8f, .5f, .5f },
                DonationTendency = .2f, FollowTendency = .95f, SocialTendency = .2f, InitialSentiment = 25,
                DailyLife = new[]
                {
                    Day("mika.commission", "spent most of the day drawing a commission", "sketching with the stream on in the background", DayActivityKind.Drawing, ViewerMood.Tired, ViewerEnergy.Low),
                    Day("mika.studies", "practised drawing hands for hours", "still drawing, the stream in the background", DayActivityKind.Drawing, ViewerMood.Chill, ViewerEnergy.Normal),
                    Day("mika.shop", "went to an art supplies shop and took a walk", "trying out new brushes", DayActivityKind.Errands, ViewerMood.Good, ViewerEnergy.Normal),
                    Day("mika.block", "had an art block and barely drew anything", "doodling a little", DayActivityKind.Drawing, ViewerMood.Bored, ViewerEnergy.Low)
                }
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
                    Notes = "Uses simple phrasing while learning Russian; sometimes asks what a Russian word means."
                },
                Talkativeness = .45f, Pace = 1.4f, ResponseSpeed = 1.2f,
                EventAffinity = new[] { 1f, .7f, .6f, 1f, .5f, .7f, .6f, .7f, .9f, .6f, .9f },
                DonationTendency = .12f, FollowTendency = .6f, SocialTendency = .5f, InitialSentiment = 15,
                DailyLife = new[]
                {
                    Day("jonas.class", "had Russian classes at the university", "practising Russian by watching streams", DayActivityKind.Study, ViewerMood.Good, ViewerEnergy.Normal),
                    Day("jonas.metro", "got lost on the metro, then bought groceries", "cooking pelmeni for the first time", DayActivityKind.Errands, ViewerMood.Upbeat, ViewerEnergy.Normal),
                    Day("jonas.essay", "wrote an essay for the university", "taking a break from the essay", DayActivityKind.Study, ViewerMood.Tired, ViewerEnergy.Low),
                    Day("jonas.walk", "walked around the city centre with a classmate", "resting at the dormitory", DayActivityKind.Walk, ViewerMood.Good, ViewerEnergy.Normal)
                }
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
                DonationTendency = .12f, FollowTendency = .6f, SocialTendency = .6f, InitialSentiment = 15,
                DailyLife = new[]
                {
                    Day("dosh.delivery", "had lectures, then a shift as a delivery courier", "eating instant noodles with the stream on", DayActivityKind.Work, ViewerMood.Tired, ViewerEnergy.Low),
                    Day("dosh.exam", "tried to study for an exam and mostly procrastinated", "procrastinating some more", DayActivityKind.Study, ViewerMood.Stressed, ViewerEnergy.Normal),
                    Day("dosh.payday", "counted money until payday and ate noodles again", "lying in the dorm", DayActivityKind.Rest, ViewerMood.Chill, ViewerEnergy.Low),
                    Day("dosh.cafe", "worked a shift at a cafe", "home, too tired to cook", DayActivityKind.Work, ViewerMood.Tired, ViewerEnergy.Low)
                }
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
                Habits = new SocialHabits { Prefers = new[] { UtteranceIntent.Answer }, Avoids = new[] { UtteranceIntent.Question, UtteranceIntent.Concern }, CallbackInterest = .3f },
                Talkativeness = .6f, Pace = 1.2f, ResponseSpeed = 1.1f,
                EventAffinity = new[] { 1.2f, .7f, .6f, .6f, .3f, .6f, .4f, .5f, .6f, 1.6f, .8f },
                DonationTendency = .06f, FollowTendency = .4f, SocialTendency = .55f, InitialSentiment = 0,
                DailyLife = new[]
                {
                    Day("sov.laptop", "gave a relative long advice about buying a laptop", "watching streams, ready with advice", DayActivityKind.Tech, ViewerMood.Good, ViewerEnergy.Normal),
                    Day("sov.forums", "read forum threads about streaming setups", "browsing forums with the stream on", DayActivityKind.Tech, ViewerMood.Chill, ViewerEnergy.Normal),
                    Day("sov.office", "worked at the office, nothing interesting", "on the phone after work", DayActivityKind.Work, ViewerMood.Bored, ViewerEnergy.Normal),
                    Day("sov.router", "helped a neighbour set up a router", "watching the stream", DayActivityKind.Tech, ViewerMood.Upbeat, ViewerEnergy.Normal)
                }
            }
        };

        private static DailyActivity Day(string id, string today, string now, DayActivityKind kind, ViewerMood mood, ViewerEnergy energy) =>
            new() { Id = id, Today = today, Now = now, Kind = kind, Mood = mood, Energy = energy };
    }
}
