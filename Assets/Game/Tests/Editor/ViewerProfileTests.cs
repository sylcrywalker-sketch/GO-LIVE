using System;
using System.Collections.Generic;
using System.Linq;
using GoLive.Desktop;
using GoLive.Viewers;
using NUnit.Framework;
using UnityEditor;

namespace GoLive.Tests
{
    // Stage C: the authored permanent community — valid, distinct, and turned into bounded prompt data whose
    // personal habits C# rations per message.
    public sealed class ViewerProfileTests
    {
        internal const string CommunityPath = "Assets/Game/Config/Viewers/ViewerCommunity.asset";

        internal static ViewerCommunityCatalog Catalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ViewerCommunityCatalog>(CommunityPath);
            Assert.That(catalog, Is.Not.Null, CommunityPath);
            return catalog;
        }

        internal static ViewerProfile Profile(string id) => Catalog().Find(id) ?? throw new InvalidOperationException(id);

        [Test]
        public void CommunityIsValidAndAssignedToTheViewerCore()
        {
            ViewerCommunityCatalog catalog = Catalog();
            Assert.That(catalog.ValidationError, Is.Null);
            Assert.That(catalog.Profiles.Count, Is.EqualTo(10));
            var core = AssetDatabase.LoadAssetAtPath<ViewerCoreConfig>(StreamRuntimeConfigTests.ViewerCorePath);
            Assert.That(core.Community, Is.SameAs(catalog));
            Assert.That(AssetDatabase.FindAssets("t:ViewerCommunityCatalog"), Has.Length.EqualTo(1));
        }

        [Test]
        public void CommunityCoversDifferentPeopleNotOneVoice()
        {
            IReadOnlyList<ViewerProfile> profiles = Catalog().Profiles;
            Assert.That(profiles.Count(p => p.Language == ViewerLanguage.Russian), Is.GreaterThanOrEqualTo(7), "a Russian channel's regulars mostly write Russian");
            Assert.That(profiles.Any(p => p.Language == ViewerLanguage.English), Is.True);
            Assert.That(profiles.Any(p => p.Language == ViewerLanguage.Mixed), Is.True);
            Assert.That(profiles.Any(p => p.InitialSentiment < 0), Is.True, "somebody dislikes the streamer");
            Assert.That(profiles.Any(p => p.Talkativeness < .25f), Is.True, "somebody mostly lurks");
            Assert.That(profiles.Count(p => p.DonationTendency >= .15f), Is.LessThanOrEqualTo(3), "not everybody donates");
            Assert.That(profiles.Any(p => p.Schedule.Contains(10)) && profiles.Any(p => p.Schedule.Contains(2)), Is.True, "daytime and night regulars");
            var fingerprints = profiles.Select(p => (p.Style.Case, p.Style.Punctuation, p.Style.Slang, p.Style.Profanity, p.Style.MinimumWords, p.Style.MaximumWords)).ToList();
            Assert.That(fingerprints.Distinct().Count(), Is.EqualTo(profiles.Count), "no two viewers type the same way");
            Assert.That(profiles.Select(p => p.Personality).Distinct().Count(), Is.EqualTo(profiles.Count));
            foreach (ViewerProfile profile in profiles)
            {
                Assert.That(profile.Personality.Length, Is.LessThanOrEqualTo(300), profile.Id + " stays bounded prompt data");
                string words = " " + SpeechRelevance.Normalize(profile.Personality) + " ";
                foreach (string signature in profile.Style.Signatures)
                    Assert.That(words, Does.Not.Contain(" " + SpeechRelevance.Normalize(signature) + " "),
                        profile.Id + ": signature words appear only when C# allows them");
            }
        }

        [Test]
        public void ProfilesBecomeWatchableParticipants()
        {
            ViewerProfile nightOwl = Profile("viewer.nightowl");
            ChatParticipant participant = nightOwl.Participant();
            Assert.That(participant.IsPermanent, Is.True);
            Assert.That(participant.NameForms, Does.Contain("найт оул"));
            Assert.That(participant.Traits.Affinity(StreamEventKind.StreamerSilence), Is.EqualTo(1.4f));
            Assert.That(participant.Persona.Profile, Is.SameAs(nightOwl));
            Assert.That(participant.Persona.Style, Does.Contain("All lowercase"));
            AudienceRoster roster = ReactionFoundationTests.Roster(3, participant);
            SpeechAnalysis speech = SpeechRelevance.Analyze(ReactionFoundationTests.Recognized("найт оул ты тут?"), roster.NamesForMentions());
            Assert.That(speech.MentionedViewerIds, Is.EqualTo(new[] { "viewer.nightowl" }));
        }

        [Test]
        public void PersonalHabitsFollowTheirAuthoredShare()
        {
            double Share(string id, string marker)
            {
                ChatParticipant viewer = Profile(id).Participant();
                AudienceRoster roster = ReactionFoundationTests.Roster(5, viewer);
                ReactionSelector selector = ReactionFoundationTests.Selector(roster, 11);
                int prompts = 0, marked = 0;
                for (int i = 0; i < 400; i++)
                {
                    SpeechAnalysis speech = SpeechRelevance.Analyze(ReactionFoundationTests.Recognized(viewer.DisplayName + " ты тут?", i + 1), roster.NamesForMentions());
                    foreach (ReactionIntent intent in selector.Select(ReactionFoundationTests.Speech(speech), i * 60, true, out _))
                    {
                        if (intent.Viewer != viewer) continue;
                        prompts++;
                        if (ChatContextBuilder.Build(intent, Situation(), 64).User.Contains(marker)) marked++;
                    }
                }
                Assert.That(prompts, Is.GreaterThan(300));
                return marked / (double)prompts;
            }
            Assert.That(Share("viewer.zinaivanovna", "«сынок»"), Is.EqualTo(.3).Within(.07), "Zina calls him сынок in about 30% of messages");
            Assert.That(Share("viewer.arcadekid", "«bro»"), Is.EqualTo(.35).Within(.07));
            Assert.That(Share("viewer.nightowl", "laugh like"), Is.EqualTo(.3).Within(.07));
            Assert.That(Share("viewer.pixelfox", "ask the streamer something"), Is.EqualTo(.45).Within(.07));
        }

        [TestCase("viewer.zinaivanovna", "блин, опять не поел", false)]
        [TestCase("viewer.zinaivanovna", "Какая красивая фигура получилась))", true)]
        [TestCase("viewer.jonas", "hello, how is the rent haha", true)]
        [TestCase("viewer.jonas", "damn that sounds hard", false)]
        [TestCase("viewer.nightowl", "блин опять", true)]
        [TestCase("viewer.nightowl", "бля опять", false)]
        [TestCase("viewer.doshirak", "минус три рубля за чай", true)]
        public void ProfanityIsLimitedPerViewer(string id, string text, bool accepted)
        {
            ChatParticipant viewer = Profile(id).Participant();
            ReactionIntent intent = ChatDirectorTests.Intent(viewer, viewer.DisplayName + " как дела?");
            ChatValidation result = ChatOutputValidator.Validate(text, intent, Array.Empty<StreamChatMessage>());
            Assert.That(result.Accepted, Is.EqualTo(accepted), text + " -> " + result.Reason);
        }

        [Test]
        public void TheViewersOwnRecentLinesKeepThemFromRepeatingThemselves()
        {
            ChatParticipant viewer = Profile("viewer.nightowl").Participant();
            var chat = new StreamChat();
            chat.Add("b", viewer.ViewerId, viewer.DisplayName, "ну ты и тормоз", 10, 0, ReactionSource.LanguageModel);
            chat.Add("b", "anon.1", "kotik", "прив", 12, 0, ReactionSource.LanguageModel);
            chat.Add("b", viewer.ViewerId, viewer.DisplayName, "опять двадцать пять", 20, 0, ReactionSource.LanguageModel);
            ReactionIntent intent = ChatDirectorTests.Intent(viewer, "NightOwl ты тут?");
            string prompt = ChatContextBuilder.Build(intent, new ChatSituation("c", 60, 5, ViewerLanguage.Russian, chat.Messages, null), 64).User;
            Assert.That(prompt, Does.Contain("YOUR LAST MESSAGES (do not repeat them or start the same way): «ну ты и тормоз», «опять двадцать пять»"));
        }

        private static ChatSituation Situation() => new("channel", 600, 8, ViewerLanguage.Russian, Array.Empty<StreamChatMessage>(), null);
    }
}
