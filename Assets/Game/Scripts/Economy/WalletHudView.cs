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

        private bool _started;
        private bool _bound;

        private void Start()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            _started = true;
            Bind();
            Refresh(_wallet.Wallet.BalanceCents);
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

            _wallet.Wallet.BalanceChanged += Refresh;
            _bound = true;
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
            decimal dollars = balanceCents / 100m;
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