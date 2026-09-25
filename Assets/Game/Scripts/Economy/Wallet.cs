using System;

namespace GoLive.Economy
{
    public sealed class Wallet
    {
        public long BalanceCents { get; private set; }

        public event Action<long> BalanceChanged;

        public Wallet(long startingBalanceCents)
        {
            if (startingBalanceCents < 0)
                throw new ArgumentOutOfRangeException(nameof(startingBalanceCents));

            BalanceCents = startingBalanceCents;
        }

        // Refuses without side effects, or changes the balance and only then notifies: an exception thrown by a
        // BalanceChanged subscriber reaches the caller after the charge (ShopCheckout's commit relies on this).
        public bool TrySpend(long amountCents)
        {
            if (!TrySpendSilently(amountCents))
                return false;

            PublishChanged();
            return true;
        }

        public void Add(long amountCents)
        {
            ValidatePositiveAmount(amountCents, nameof(amountCents));

            BalanceCents = checked(BalanceCents + amountCents);
            PublishChanged();
        }

        public void Restore(long balanceCents)
        {
            if (balanceCents < 0)
                throw new ArgumentOutOfRangeException(nameof(balanceCents));

            if (BalanceCents == balanceCents)
                return;

            BalanceCents = balanceCents;
            PublishChanged();
        }

        internal bool TrySpendSilently(long amountCents)
        {
            ValidatePositiveAmount(amountCents, nameof(amountCents));

            if (BalanceCents < amountCents)
                return false;

            BalanceCents -= amountCents;
            return true;
        }

        internal void PublishChanged()
        {
            BalanceChanged?.Invoke(BalanceCents);
        }

        private static void ValidatePositiveAmount(
            long amountCents,
            string parameterName)
        {
            if (amountCents <= 0)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
