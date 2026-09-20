using GoLive.GameTime;
using UnityEngine;

namespace GoLive.Needs
{
    [DisallowMultipleComponent]
    public sealed class PlayerNeedsBehaviour : MonoBehaviour
    {
        [SerializeField] private GameClockBehaviour _gameClock;
        [SerializeField] private PlayerNeedsConfig _config;

        public PlayerNeeds Needs { get; private set; }

        private bool _bound;

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            Needs = new PlayerNeeds(_config.CreateRules());
        }

        private void Start()
        {
            if (_gameClock.Clock == null)
            {
                Debug.LogError($"{nameof(PlayerNeedsBehaviour)} could not access the Game Clock.", this);
                enabled = false;
                return;
            }

            Bind();
        }

        private void OnEnable()
        {
            if (Needs != null && _gameClock != null && _gameClock.Clock != null)
                Bind();
        }

        private void OnDisable()
        {
            Unbind();
        }

        public void SatisfyHunger(float amount)
        {
            Needs?.SatisfyHunger(amount);
        }

        public void IncreaseHunger(float amount)
        {
            Needs?.IncreaseHunger(amount);
        }

        public void RestoreConcentration(float amount)
        {
            Needs?.RestoreConcentration(amount);
        }

        public void ConsumeConcentration(float amount)
        {
            Needs?.ConsumeConcentration(amount);
        }

        public void Restore(PlayerNeedsSnapshot snapshot)
        {
            Needs?.Restore(snapshot);
        }

        private void Bind()
        {
            if (_bound)
                return;

            _gameClock.Clock.Advanced += HandleTimeAdvanced;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _gameClock == null || _gameClock.Clock == null)
                return;

            _gameClock.Clock.Advanced -= HandleTimeAdvanced;
            _bound = false;
        }

        private void HandleTimeAdvanced(GameTimeAdvance advance)
        {
            Needs.AdvanceTime(advance.AdvancedSeconds);
        }

        private bool ValidateConfiguration()
        {
            if (_gameClock == null)
            {
                Debug.LogError($"{nameof(PlayerNeedsBehaviour)} on {name} requires a Game Clock.", this);
                return false;
            }

            if (_config == null)
            {
                Debug.LogError($"{nameof(PlayerNeedsBehaviour)} on {name} requires a Player Needs Config.", this);
                return false;
            }

            if (!_config.IsValid)
            {
                Debug.LogError($"{nameof(PlayerNeedsConfig)} assigned to {name} contains invalid values.", _config);
                return false;
            }

            return true;
        }
    }
}