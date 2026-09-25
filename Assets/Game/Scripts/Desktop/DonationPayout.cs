using System;
using GoLive.Economy;

namespace GoLive.Desktop
{
    // Credits every newly accepted donation receipt to the player's wallet exactly once. DonationAccount
    // deduplicates receipt ids and never raises Received for restored history, so a repeated callback or
    // a save/load cannot pay twice. Viewer activity never touches the wallet by any other path.
    public sealed class DonationPayout : IDisposable
    {
        private readonly DonationAccount _donations;
        private readonly Wallet _wallet;
        private bool _disposed;

        public DonationPayout(DonationAccount donations, Wallet wallet)
        {
            _donations = donations ?? throw new ArgumentNullException(nameof(donations));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _donations.Received += Credit;
        }

        private void Credit(DonationReceipt receipt) => _wallet.Add(receipt.AmountCents);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _donations.Received -= Credit;
        }
    }
}
