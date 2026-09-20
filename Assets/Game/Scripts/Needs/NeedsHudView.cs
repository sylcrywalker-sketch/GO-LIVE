using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GoLive.Needs
{
    [DisallowMultipleComponent]
    public sealed class NeedsHudView : MonoBehaviour
    {
        [SerializeField] private PlayerNeedsBehaviour _playerNeeds;

        [Header("Hunger")]
        [SerializeField] private Slider _hungerSlider;
        [SerializeField] private TMP_Text _hungerValueText;

        [Header("Concentration")]
        [SerializeField] private Slider _concentrationSlider;
        [SerializeField] private TMP_Text _concentrationValueText;

        private bool _bound;

        private void Start()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            ConfigureSlider(_hungerSlider);
            ConfigureSlider(_concentrationSlider);

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
            _hungerSlider.SetValueWithoutNotify(snapshot.HungerNormalized);
            _concentrationSlider.SetValueWithoutNotify(snapshot.ConcentrationNormalized);

            _hungerValueText.text = Mathf.RoundToInt(snapshot.Hunger).ToString();
            _concentrationValueText.text = Mathf.RoundToInt(snapshot.Concentration).ToString();
        }

        private bool ValidateConfiguration()
        {
            if (_playerNeeds != null &&
                _hungerSlider != null &&
                _hungerValueText != null &&
                _concentrationSlider != null &&
                _concentrationValueText != null)
            {
                return true;
            }

            Debug.LogError($"{nameof(NeedsHudView)} on {name} has incomplete configuration.", this);
            return false;
        }

        private static void ConfigureSlider(Slider slider)
        {
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
        }
    }
}