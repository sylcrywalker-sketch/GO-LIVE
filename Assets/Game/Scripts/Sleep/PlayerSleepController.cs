using System;
using GoLive.GameTime;
using GoLive.Needs;
using UnityEngine;

namespace GoLive.Sleep
{
    [DisallowMultipleComponent]
    public sealed class PlayerSleepController : MonoBehaviour
    {
        [SerializeField] private GameClockBehaviour _gameClock;
        [SerializeField] private PlayerNeedsBehaviour _playerNeeds;
        [SerializeField] private SleepConfig _config;

        public event Action<SleepResult> SleepCompleted;

        private SleepService _sleep;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void Start()
        {
            if (!isActiveAndEnabled)
                return;

            if (_gameClock.Clock == null || _playerNeeds.Needs == null)
            {
                Debug.LogError($"{nameof(PlayerSleepController)} could not access initialized Game Clock or Player Needs.", this);
                enabled = false;
                return;
            }

            _sleep = new SleepService(_gameClock.Clock, _playerNeeds.Needs, _config.CreateRules());
        }

        public bool CanSleepDefault()
        {
            return _sleep != null && _sleep.CanSleep(_sleep.DefaultSleepHours);
        }

        public bool TrySleepDefault()
        {
            return _sleep != null && TrySleep(_sleep.DefaultSleepHours);
        }

        public bool TrySleep(double hours)
        {
            if (_sleep == null || !_sleep.TrySleep(hours, out SleepResult result))
                return false;

            SleepCompleted?.Invoke(result);
            return true;
        }

        private bool ValidateConfiguration()
        {
            if (_gameClock == null)
            {
                Debug.LogError($"{nameof(PlayerSleepController)} on {name} requires a Game Clock.", this);
                return false;
            }

            if (_playerNeeds == null)
            {
                Debug.LogError($"{nameof(PlayerSleepController)} on {name} requires Player Needs.", this);
                return false;
            }

            if (_config == null)
            {
                Debug.LogError($"{nameof(PlayerSleepController)} on {name} requires a Sleep Config.", this);
                return false;
            }

            if (!_config.IsValid)
            {
                Debug.LogError($"{nameof(SleepConfig)} assigned to {name} contains invalid values.", _config);
                return false;
            }

            return true;
        }
    }
}