using UnityEngine;

namespace GoLive.Economy
{
    [DisallowMultipleComponent]
    public sealed class WalletBehaviour : MonoBehaviour
    {
        [SerializeField] private EconomyConfig _config;

        public Wallet Wallet { get; private set; }

        private void Awake()
        {
            if (_config == null)
            {
                Debug.LogError($"{nameof(WalletBehaviour)} on {name} requires an Economy Config.", this);
                enabled = false;
                return;
            }

            if (!_config.IsValid)
            {
                Debug.LogError($"{nameof(EconomyConfig)} assigned to {name} contains invalid values.", _config);
                enabled = false;
                return;
            }

            Wallet = new Wallet(_config.StartingBalanceCents);
        }
    }
}