using System;
using GoLive.Economy;
using GoLive.GameTime;
using GoLive.Localization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GoLive.Tests
{
    // The HUD rent line: RentHudPresentation over the real default rent rules and a real clock (wired the way
    // RentBehaviour wires them), and its Russian/English text from the real catalog.
    public sealed class RentHudPresentationTests
    {
        private const string RentConfigPath = "Assets/Game/Scripts/Economy/Config/DefaultRentConfig.asset";
        private const string CatalogPath = "Assets/Game/Scripts/Localization/Catalog/GameLocalizationCatalog.asset";

        private RentRules _rules;
        private GameClock _clock;
        private RentAccount _rent;
        private GameObject _localizationObject;
        private LocalizationContext _localization;

        [SetUp]
        public void SetUp()
        {
            _rules = AssetDatabase.LoadAssetAtPath<RentConfig>(RentConfigPath).CreateRules();
            _clock = new GameClock(1, 7, 0);
            _rent = new RentAccount(_rules, _clock.Current);
            _clock.Advanced += _rent.Advance;

            _localizationObject = new GameObject("Localization (test)");
            _localization = _localizationObject.AddComponent<LocalizationContext>();
            SaveTestWorld.SetField(_localization, "_catalog", AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(CatalogPath));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_localizationObject);
        }

        [Test]
        public void TheDefaultRulesAreTheFirstRentCycle()
        {
            Assert.That(_rules.FirstPaymentCents, Is.EqualTo(5000), "$50 first rent");
            Assert.That(_rules.LatePenaltyCents, Is.EqualTo(2000), "$20 late penalty");
            Assert.That(_rules.FirstDeadlineDay, Is.EqualTo(3), "first rent due by the end of Day 3");
            Assert.That(_rules.FinalDeadlineDay, Is.EqualTo(6), "second rent due by the end of Day 6");
        }

        [Test]
        public void FirstRentCountsDownDayByDayToItsDueDay()
        {
            Assert.That(Line(), Is.EqualTo(new RentHudState(RentHudStatus.DaysLeft, 5000, 2)), "Day 1");

            AdvanceTo(2, 7, 0);
            Assert.That(Line(), Is.EqualTo(new RentHudState(RentHudStatus.DaysLeft, 5000, 1)), "Day 2");

            AdvanceTo(3, 7, 0);
            Assert.That(Line(), Is.EqualTo(new RentHudState(RentHudStatus.DueToday, 5000, 0)), "Day 3");

            AdvanceTo(3, 23, 59);
            Assert.That(Line().Status, Is.EqualTo(RentHudStatus.DueToday), "still due at 23:59 on Day 3");

            AdvanceTo(4, 0, 0);
            Assert.That(Line(), Is.EqualTo(new RentHudState(RentHudStatus.Overdue, 7000, 0)), "Day 4: overdue with the penalty");
        }

        [Test]
        public void TheCountChangesExactlyAtMidnight()
        {
            AdvanceTo(1, 23, 59);
            Assert.That(Line().DaysLeft, Is.EqualTo(2));

            AdvanceTo(2, 0, 0);
            Assert.That(Line().DaysLeft, Is.EqualTo(1));
        }

        [Test]
        public void RussianFirstRentLines()
        {
            AssertText(GameLanguage.Russian, "Аренда: $50.00", "осталось 2 дня");

            AdvanceTo(2, 7, 0);
            AssertText(GameLanguage.Russian, "Аренда: $50.00", "остался 1 день");

            AdvanceTo(3, 7, 0);
            AssertText(GameLanguage.Russian, "Аренда: $50.00", "сегодня день оплаты");

            AdvanceTo(4, 7, 0);
            AssertText(GameLanguage.Russian, "Аренда: $70.00", "оплата просрочена");
        }

        [Test]
        public void EnglishFirstRentLines()
        {
            AssertText(GameLanguage.English, "Rent: $50.00", "2 days left");

            AdvanceTo(2, 7, 0);
            AssertText(GameLanguage.English, "Rent: $50.00", "1 day left");

            AdvanceTo(3, 7, 0);
            AssertText(GameLanguage.English, "Rent: $50.00", "due today");

            AdvanceTo(4, 7, 0);
            AssertText(GameLanguage.English, "Rent: $70.00", "overdue");
        }

        [Test]
        public void PayingTheFirstRentShowsWhenTheNextOneIsDue()
        {
            AdvanceTo(2, 9, 0);
            Assert.That(_rent.TryPay(new Wallet(10000)), Is.True);

            Assert.That(Line(), Is.EqualTo(new RentHudState(RentHudStatus.PaidNextDue, 5000, 4)), "second rent due by the end of Day 6");
            AssertText(GameLanguage.Russian, "Аренда оплачена", "следующая через 4 дня");
            AssertText(GameLanguage.English, "Rent paid", "next due in 4 days");

            AdvanceTo(5, 7, 0);
            AssertText(GameLanguage.Russian, "Аренда оплачена", "следующая через 1 день");

            AdvanceTo(6, 7, 0);
            Assert.That(Line(), Is.EqualTo(new RentHudState(RentHudStatus.DueToday, 5000, 0)), "the second bill is due on Day 6");
            AssertText(GameLanguage.Russian, "Аренда: $50.00", "сегодня день оплаты");
        }

        [Test]
        public void TheFinalDayOfAnUnpaidCycleShowsEverythingOwed()
        {
            AdvanceTo(6, 7, 0);

            Assert.That(Line(), Is.EqualTo(new RentHudState(RentHudStatus.DueToday, 12000, 0)), "$50 + $20 penalty + $50");
            AssertText(GameLanguage.English, "Rent: $120.00", "due today");
        }

        [Test]
        public void SettledAndFailedRentHaveNoDeadline()
        {
            Wallet wallet = new(20000);
            Assert.That(_rent.TryPay(wallet), Is.True);
            AdvanceTo(6, 7, 0);
            Assert.That(_rent.TryPay(wallet), Is.True);

            Assert.That(Line(), Is.EqualTo(new RentHudState(RentHudStatus.Paid, 0, 0)));
            AssertText(GameLanguage.Russian, "Аренда оплачена", null);

            AdvanceTo(7, 7, 0);
            Assert.That(Line().Status, Is.EqualTo(RentHudStatus.Paid), "completed cycle");

            GameClock clock = new(1, 7, 0);
            RentAccount unpaid = new(_rules, clock.Current);
            clock.Advanced += unpaid.Advance;
            clock.AdvanceMinutes(6 * 24 * 60);

            RentHudState failed = RentHudPresentation.Evaluate(_rules, unpaid.Current, clock.Current);
            Assert.That(failed.Status, Is.EqualTo(RentHudStatus.Unpaid));
            _localization.SetLanguage(GameLanguage.English);
            Assert.That(RentHudPresentation.Localize(failed, _localization).Lead, Is.EqualTo("Rent unpaid"));
        }

        [Test]
        public void LongerCyclesUseTheRussianPluralForms()
        {
            RentRules rules = new(5000, 5000, 2000, 26, 30, 30);
            GameClock clock = new(1, 7, 0);
            RentAccount rent = new(rules, clock.Current);
            clock.Advanced += rent.Advance;
            _localization.SetLanguage(GameLanguage.Russian);

            string Status() => RentHudPresentation.Localize(RentHudPresentation.Evaluate(rules, rent.Current, clock.Current), _localization).Status;

            Assert.That(Status(), Is.EqualTo("осталось 25 дней"), "Day 1");
            clock.AdvanceMinutes(24 * 60);
            Assert.That(Status(), Is.EqualTo("осталось 24 дня"), "Day 2");
            clock.AdvanceMinutes(3 * 24 * 60);
            Assert.That(Status(), Is.EqualTo("остался 21 день"), "Day 5");
            clock.AdvanceMinutes(24 * 60);
            Assert.That(Status(), Is.EqualTo("осталось 20 дней"), "Day 6");
        }

        [TestCase(0, "many")]
        [TestCase(1, "one")]
        [TestCase(2, "few")]
        [TestCase(4, "few")]
        [TestCase(5, "many")]
        [TestCase(11, "many")]
        [TestCase(12, "many")]
        [TestCase(14, "many")]
        [TestCase(21, "one")]
        [TestCase(22, "few")]
        [TestCase(25, "many")]
        [TestCase(101, "one")]
        [TestCase(111, "many")]
        [TestCase(122, "few")]
        public void RussianPluralForms(int count, string form)
        {
            Assert.That(LocalizationRuntime.PluralForm(GameLanguage.Russian, count), Is.EqualTo(form));
        }

        [TestCase(0, "many")]
        [TestCase(1, "one")]
        [TestCase(2, "many")]
        [TestCase(21, "many")]
        public void EnglishPluralForms(int count, string form)
        {
            Assert.That(LocalizationRuntime.PluralForm(GameLanguage.English, count), Is.EqualTo(form));
        }

        [Test]
        public void EveryRentLineTextExistsInBothLanguages()
        {
            LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(CatalogPath);
            string[] plain =
            {
                RentHudPresentation.AmountKey,
                RentHudPresentation.DueTodayKey,
                RentHudPresentation.OverdueKey,
                RentHudPresentation.PaidKey,
                RentHudPresentation.UnpaidKey
            };
            string[] counted = { RentHudPresentation.DaysLeftKey, RentHudPresentation.NextDueKey };

            foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
            {
                foreach (string key in plain)
                    Assert.That(catalog.TryGetText(key, language, out _), Is.True, $"{key} ({language})");

                foreach (string key in counted)
                {
                    foreach (string form in new[] { "one", "few", "many" })
                        Assert.That(catalog.TryGetText($"{key}.{form}", language, out _), Is.True, $"{key}.{form} ({language})");
                }
            }
        }

        [Test]
        public void TheCalendarStartsOnMondayAndEveryWeekdayIsLocalized()
        {
            LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(CatalogPath);
            DayOfWeek[] expected =
            {
                DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
                DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday, DayOfWeek.Monday
            };

            for (int day = 1; day <= expected.Length; day++)
            {
                DayOfWeek weekday = new GameTimeSnapshot((day - 1L) * GameTimeSnapshot.SecondsPerDay).DayOfWeek;
                Assert.That(weekday, Is.EqualTo(expected[day - 1]), $"Day {day}");

                foreach (GameLanguage language in Enum.GetValues(typeof(GameLanguage)))
                    Assert.That(catalog.TryGetText(GameTimeHudView.WeekdayKey(weekday), language, out _), Is.True, $"{weekday} ({language})");
            }

            Assert.That(_localization.Text(GameTimeHudView.WeekdayKey(DayOfWeek.Monday)), Is.EqualTo("Пн"));
        }

        private RentHudState Line()
        {
            return RentHudPresentation.Evaluate(_rules, _rent.Current, _clock.Current);
        }

        private void AssertText(GameLanguage language, string lead, string status)
        {
            _localization.SetLanguage(language);
            RentHudText text = RentHudPresentation.Localize(Line(), _localization);

            Assert.That(text.Lead, Is.EqualTo(lead), $"{language} lead on Day {_clock.Current.Day}");
            Assert.That(text.Status, Is.EqualTo(status), $"{language} deadline on Day {_clock.Current.Day}");
        }

        private void AdvanceTo(int day, int hour, int minute)
        {
            long target = (day - 1L) * GameTimeSnapshot.SecondsPerDay + hour * GameTimeSnapshot.SecondsPerHour + minute * GameTimeSnapshot.SecondsPerMinute;
            _clock.AdvanceSeconds(target - _clock.Current.TotalSeconds);
        }
    }
}
