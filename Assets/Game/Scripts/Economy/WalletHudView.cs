using System;
using System.Globalization;
using TMPro;
using UnityEngine;

namespace GoLive.Economy
{
    [DisallowMultipleComponent]
    public sealed class WalletHudView : MonoBehaviour
    {
        [SerializeField] private WalletBehaviour _wallet;
        [SerializeField] private TMP_Text _balanceText;
        [SerializeField, Min(0.01f)] private float _countResponseSeconds = 0.12f;

        private bool _started;
        private bool _bound;
        private long _targetCents;
        private double _shownCents;
        private long _renderedCents = long.MinValue;

        private void Start()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _started = true;
            _targetCents = _wallet.Wallet.BalanceCents;
            _shownCents = _targetCents;
            Render(_targetCents);
            Bind();
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

        // The shown amount counts toward the new balance for a moment instead of jumping.
        private void Update()
        {
            if (_renderedCents == _targetCents)
                return;

            _shownCents += (_targetCents - _shownCents) * (1d - Math.Exp(-Time.unscaledDeltaTime / _countResponseSeconds));

            if (Math.Abs(_targetCents - _shownCents) < 0.5d)
                _shownCents = _targetCents;

            Render((long)Math.Round(_shownCents));
        }

        private void Bind()
        {
            if (_bound)
                return;

            _wallet.Wallet.BalanceChanged += Refresh;
            _bound = true;
            Refresh(_wallet.Wallet.BalanceCents);
        }

        private void Unbind()
        {
            if (!_bound || _wallet == null || _wallet.Wallet == null)
                return;

            _wallet.Wallet.BalanceChanged -= Refresh;
            _bound = false;
        }

        private void Refresh(long balanceCents)
        {
            _targetCents = balanceCents;
        }

        private void Render(long cents)
        {
            if (_renderedCents == cents)
                return;

            _renderedCents = cents;
            decimal dollars = cents / 100m;
            _balanceText.text = $"${dollars.ToString("0.00", CultureInfo.InvariantCulture)}";
        }

        private bool ValidateConfiguration()
        {
            if (_wallet != null && _wallet.Wallet != null && _balanceText != null)
                return true;

            Debug.LogError($"{nameof(WalletHudView)} on {name} has incomplete configuration.", this);
            return false;
        }
    }
}
