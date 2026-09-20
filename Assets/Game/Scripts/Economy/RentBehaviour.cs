using System;
using GoLive.GameTime;
using UnityEngine;

namespace GoLive.Economy
{
    [DisallowMultipleComponent]
    public sealed class RentBehaviour : MonoBehaviour
    {
        [SerializeField] private GameClockBehaviour _gameClock;
        [SerializeField] private WalletBehaviour _wallet;
        [SerializeField] private RentConfig _config;

        public event Action<RentSnapshot> Changed;

        private RentAccount _rent;
        private bool _bound;

        private void Awake()
        {
            if (!ValidateConfiguration())
                enabled = false;
        }

        private void Start()
        {
            if (!isActiveAndEnabled)
                return;

            if (_gameClock.Clock == null || _wallet.Wallet == null)
            {
                Debug.LogError($"{nameof(RentBehaviour)} could not access initialized Game Clock or Wallet.", this);
                enabled = false;
                return;
            }

            _rent = new RentAccount(_config.CreateRules(), _gameClock.Clock.Current);
            _rent.Changed += HandleRentChanged;

            Bind();
            Changed?.Invoke(_rent.Current);
        }

        private void OnEnable()
        {
            if (_rent != null)
                Bind();
        }

        private void OnDisable()
        {
            Unbind();

            if (_rent != null)
                _rent.Changed -= HandleRentChanged;
        }

        public bool TryGetSnapshot(out RentSnapshot snapshot)
        {
            if (_rent == null)
            {
                snapshot = default;
                return false;
            }

            snapshot = _rent.Current;
            return true;
        }

        public bool TryPayCurrentDue()
        {
            return _rent != null && _rent.TryPay(_wallet.Wallet);
        }

        public void Restore(RentSnapshot snapshot)
        {
            _rent?.Restore(snapshot);
        }

        [ContextMenu("Pay Current Rent")]
        private void PayCurrentRentFromInspector()
        {
            if (!Application.isPlaying)
                return;

            TryPayCurrentDue();
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
            _rent.Advance(advance);
        }

        private void HandleRentChanged(RentSnapshot snapshot)
        {
            Changed?.Invoke(snapshot);
        }

        private bool ValidateConfiguration()
        {
            if (_gameClock == null)
            {
                Debug.LogError($"{nameof(RentBehaviour)} on {name} requires a Game Clock.", this);
                return false;
            }

            if (_wallet == null)
            {
                Debug.LogError($"{nameof(RentBehaviour)} on {name} requires a Wallet.", this);
                return false;
            }

            if (_config == null)
            {
                Debug.LogError($"{nameof(RentBehaviour)} on {name} requires a Rent Config.", this);
                return false;
            }

            if (!_config.IsValid)
            {
                Debug.LogError($"{nameof(RentConfig)} assigned to {name} contains invalid values.", _config);
                return false;
            }

            return true;
        }
    }
}