using System;
using GoLive.Localization;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

namespace GoLive.GameTime
{
    [DisallowMultipleComponent]
    public sealed class GameTimeHudView : MonoBehaviour
    {
        private const string DayLocalizationKey = "hud.day";

        [FormerlySerializedAs("gameClock")]
        [SerializeField] private GameClockBehaviour _gameClock;

        [FormerlySerializedAs("clockText")]
        [SerializeField] private TMP_Text _clockText;

        [FormerlySerializedAs("dayText")]
        [SerializeField] private TMP_Text _dayText;

        [SerializeField] private TMP_Text _weekdayText;
        [SerializeField] private LocalizationContext _localization;

        private bool _started;
        private bool _bound;

        public static string WeekdayKey(DayOfWeek day)
        {
            return day switch
            {
                DayOfWeek.Monday => "hud.weekday.monday",
                DayOfWeek.Tuesday => "hud.weekday.tuesday",
                DayOfWeek.Wednesday => "hud.weekday.wednesday",
                DayOfWeek.Thursday => "hud.weekday.thursday",
                DayOfWeek.Friday => "hud.weekday.friday",
                DayOfWeek.Saturday => "hud.weekday.saturday",
                DayOfWeek.Sunday => "hud.weekday.sunday",
                _ => throw new ArgumentOutOfRangeException(nameof(day))
            };
        }

        private void Start()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _started = true;
            Bind();
            Refresh(_gameClock.Clock.Current);
        }

        private void OnEnable()
        {
            if (_started)
                Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (_bound)
                return;

            _bound = true;

            _gameClock.Clock.MinuteChanged += HandleMinuteChanged;
            _gameClock.Clock.DayChanged += HandleDayChanged;
            _localization.LanguageChanged += HandleLanguageChanged;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _bound = false;

            _gameClock.Clock.MinuteChanged -= HandleMinuteChanged;
            _gameClock.Clock.DayChanged -= HandleDayChanged;
            _localization.LanguageChanged -= HandleLanguageChanged;
        }

        private void HandleMinuteChanged(GameTimeSnapshot snapshot)
        {
            RefreshClock(snapshot);
        }

        private void HandleDayChanged(int previousDay, int currentDay)
        {
            RefreshDay(_gameClock.Clock.Current);
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            RefreshDay(_gameClock.Clock.Current);
        }

        private void Refresh(GameTimeSnapshot snapshot)
        {
            RefreshClock(snapshot);
            RefreshDay(snapshot);
        }

        private void RefreshClock(GameTimeSnapshot snapshot)
        {
            _clockText.text = $"{snapshot.Hour:00}:{snapshot.Minute:00}";
        }

        private void RefreshDay(GameTimeSnapshot snapshot)
        {
            _dayText.text = _localization.Format(DayLocalizationKey, snapshot.Day);
            _weekdayText.text = _localization.Text(WeekdayKey(snapshot.DayOfWeek));
        }

        private bool ValidateConfiguration()
        {
            if (_gameClock != null &&
                _gameClock.Clock != null &&
                _clockText != null &&
                _dayText != null &&
                _weekdayText != null &&
                _localization != null)
            {
                return true;
            }

            Debug.LogError($"{nameof(GameTimeHudView)} on {name} has incomplete configuration.", this);
            return false;
        }
    }
}
