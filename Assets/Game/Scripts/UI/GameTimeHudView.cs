using TMPro;
using UnityEngine;

namespace GoLive.GameTime
{
    [DisallowMultipleComponent]
    public sealed class GameTimeHudView : MonoBehaviour
    {
        [SerializeField] private GameClockBehaviour gameClock;
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private TMP_Text dayText;

        private bool _bound;

        private void Start()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            gameClock.Clock.MinuteChanged += OnMinuteChanged;
            gameClock.Clock.DayChanged += OnDayChanged;
            _bound = true;

            Refresh(gameClock.Clock.Current);
        }

        private void OnDisable()
        {
            if (!_bound || gameClock == null || gameClock.Clock == null)
                return;

            gameClock.Clock.MinuteChanged -= OnMinuteChanged;
            gameClock.Clock.DayChanged -= OnDayChanged;
            _bound = false;
        }

        private void OnMinuteChanged(GameTimeSnapshot snapshot)
        {
            RefreshClock(snapshot);
        }

        private void OnDayChanged(int previousDay, int currentDay)
        {
            dayText.text = currentDay.ToString();
        }

        private void Refresh(GameTimeSnapshot snapshot)
        {
            RefreshClock(snapshot);
            dayText.text = snapshot.Day.ToString();
        }

        private void RefreshClock(GameTimeSnapshot snapshot)
        {
            clockText.text = $"{snapshot.Hour:00}:{snapshot.Minute:00}";
        }

        private bool ValidateConfiguration()
        {
            if (gameClock != null && gameClock.Clock != null && clockText != null && dayText != null)
                return true;

            Debug.LogError($"{nameof(GameTimeHudView)} on {name} has incomplete configuration.", this);
            return false;
        }
    }
}