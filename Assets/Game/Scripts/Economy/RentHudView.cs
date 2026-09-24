using GoLive.GameTime;
using GoLive.Localization;
using TMPro;
using UnityEngine;

namespace GoLive.Economy
{
    [DisallowMultipleComponent]
    public sealed class RentHudView : MonoBehaviour
    {
        [SerializeField] private RentBehaviour _rent;
        [SerializeField] private RentConfig _config;
        [SerializeField] private GameClockBehaviour _gameClock;
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private TMP_Text _objectiveText;

        [Header("Deadline tone")]
        [SerializeField] private Color _separatorColor = new(0.62f, 0.61f, 0.58f, 0.7f);
        [SerializeField] private Color _upcomingColor = new(0.86f, 0.76f, 0.6f, 1f);
        [SerializeField] private Color _dueTodayColor = new(0.95f, 0.71f, 0.43f, 1f);
        [SerializeField] private Color _overdueColor = new(0.93f, 0.53f, 0.43f, 1f);

        private RentRules _rules;
        private bool _started;
        private bool _bound;

        private void Start()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _rules = _config.CreateRules();
            _started = true;
            Bind();
            Refresh();
        }

        private void OnEnable()
        {
            if (!_started)
                return;

            Bind();
            Refresh();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (_bound)
                return;

            _rent.Changed += HandleRentChanged;
            _gameClock.Clock.DayChanged += HandleDayChanged;
            _localization.LanguageChanged += HandleLanguageChanged;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _rent.Changed -= HandleRentChanged;
            _gameClock.Clock.DayChanged -= HandleDayChanged;
            _localization.LanguageChanged -= HandleLanguageChanged;
            _bound = false;
        }

        private void HandleRentChanged(RentSnapshot snapshot)
        {
            Refresh();
        }

        private void HandleDayChanged(int previousDay, int currentDay)
        {
            Refresh();
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (!_rent.TryGetSnapshot(out RentSnapshot snapshot))
                return;

            RentHudState state = RentHudPresentation.Evaluate(_rules, snapshot, _gameClock.Clock.Current);
            RentHudText text = RentHudPresentation.Localize(state, _localization);

            _objectiveText.text = text.Status == null
                ? text.Lead
                : $"{text.Lead}<color=#{Hex(_separatorColor)}>  •  </color><color=#{Hex(ToneOf(state.Status))}>{text.Status}</color>";
        }

        private Color ToneOf(RentHudStatus status)
        {
            return status switch
            {
                RentHudStatus.DueToday => _dueTodayColor,
                RentHudStatus.Overdue => _overdueColor,
                _ => _upcomingColor
            };
        }

        private bool ValidateConfiguration()
        {
            if (_rent != null &&
                _config != null &&
                _gameClock != null &&
                _gameClock.Clock != null &&
                _localization != null &&
                _objectiveText != null)
            {
                return true;
            }

            Debug.LogError($"{nameof(RentHudView)} on {name} has incomplete configuration.", this);
            return false;
        }

        private static string Hex(Color color)
        {
            return ColorUtility.ToHtmlStringRGBA(color);
        }
    }
}
