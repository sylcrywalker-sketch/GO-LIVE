using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Needs
{
    [DisallowMultipleComponent]
    public sealed class NeedsHudView : MonoBehaviour
    {
        // One HUD row: icon, label above a thin bar, value on the right. Presentation state only.
        [System.Serializable]
        private sealed class NeedRow
        {
            [SerializeField] private Slider _bar;
            [SerializeField] private Graphic _fill;
            [SerializeField] private Graphic _icon;
            [SerializeField] private TMP_Text _valueText;
            [SerializeField] private Color _fillColor = Color.white;

            private float _target;
            private float _shown = -1f;
            private float _warning;
            private float _emphasis;
            private bool _inWarning;
            private int _shownPercent = -1;

            public bool IsConfigured => _bar != null && _fill != null && _icon != null && _valueText != null;

            public void Configure()
            {
                _bar.minValue = 0f;
                _bar.maxValue = 1f;
                _bar.wholeNumbers = false;
            }

            public void SetTarget(float normalized, bool warning)
            {
                _target = Mathf.Clamp01(normalized);

                if (_shown < 0f)
                    _shown = _target;

                if (warning && !_inWarning)
                    _emphasis = 1f;

                _inWarning = warning;
            }

            public void Tick(float deltaTime, float response, float emphasisSeconds, Color iconColor, Color valueColor, Color warningColor)
            {
                float blend = 1f - Mathf.Exp(-deltaTime / response);
                _shown = Mathf.Abs(_shown - _target) < 0.0005f ? _target : Mathf.Lerp(_shown, _target, blend);
                _warning = Mathf.Lerp(_warning, _inWarning ? 1f : 0f, blend);
                _emphasis = Mathf.Max(0f, _emphasis - deltaTime / emphasisSeconds);

                _bar.SetValueWithoutNotify(_shown);

                int percent = Mathf.RoundToInt(_shown * 100f);
                if (percent != _shownPercent)
                {
                    _shownPercent = percent;
                    _valueText.text = $"{percent}%";
                }

                float flash = _emphasis * _emphasis * 0.55f;
                _fill.color = Color.Lerp(Color.Lerp(_fillColor, warningColor, _warning), Color.white, flash);
                _icon.color = Color.Lerp(Color.Lerp(iconColor, warningColor, _warning), Color.white, flash);
                _valueText.color = Color.Lerp(Color.Lerp(valueColor, warningColor, _warning), Color.white, flash);
            }
        }

        [SerializeField] private PlayerNeedsBehaviour _playerNeeds;
        [SerializeField] private NeedRow _hunger = new();
        [SerializeField] private NeedRow _concentration = new();

        [Header("Presentation")]
        [SerializeField, Range(0f, 1f)] private float _hungerWarningAt = 0.75f;
        [SerializeField, Range(0f, 1f)] private float _concentrationWarningBelow = 0.25f;
        [SerializeField, Min(0.01f)] private float _barResponseSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float _emphasisSeconds = 0.9f;
        [SerializeField] private Color _iconColor = new(0.78f, 0.77f, 0.74f, 1f);
        [SerializeField] private Color _valueColor = new(0.93f, 0.92f, 0.89f, 1f);
        [SerializeField] private Color _warningColor = new(0.92f, 0.52f, 0.4f, 1f);

        private bool _bound;

        private void Start()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _hunger.Configure();
            _concentration.Configure();

            Bind();
            Refresh(_playerNeeds.Needs.Current);
        }

        private void OnEnable()
        {
            if (_playerNeeds != null && _playerNeeds.Needs != null)
                Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            float deltaTime = Time.unscaledDeltaTime;
            _hunger.Tick(deltaTime, _barResponseSeconds, _emphasisSeconds, _iconColor, _valueColor, _warningColor);
            _concentration.Tick(deltaTime, _barResponseSeconds, _emphasisSeconds, _iconColor, _valueColor, _warningColor);
        }

        private void Bind()
        {
            if (_bound)
                return;

            _playerNeeds.Needs.Changed += Refresh;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _playerNeeds == null || _playerNeeds.Needs == null)
                return;

            _playerNeeds.Needs.Changed -= Refresh;
            _bound = false;
        }

        private void Refresh(PlayerNeedsSnapshot snapshot)
        {
            _hunger.SetTarget(snapshot.HungerNormalized, snapshot.HungerNormalized >= _hungerWarningAt);
            _concentration.SetTarget(snapshot.ConcentrationNormalized, snapshot.ConcentrationNormalized <= _concentrationWarningBelow);
        }

        private bool ValidateConfiguration()
        {
            if (_playerNeeds != null && _playerNeeds.Needs != null && _hunger.IsConfigured && _concentration.IsConfigured)
                return true;

            Debug.LogError($"{nameof(NeedsHudView)} on {name} has incomplete configuration.", this);
            return false;
        }
    }
}
