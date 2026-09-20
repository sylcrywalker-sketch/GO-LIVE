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

        public bool TrySpend(long amountCents)
        {
            ValidatePositiveAmount(amountCents, nameof(amountCents));

            if (BalanceCents < amountCents)
                return false;

            BalanceCents -= amountCents;
            BalanceChanged?.Invoke(BalanceCents);

            return true;
        }

        public void Add(long amountCents)
        {
            ValidatePositiveAmount(amountCents, nameof(amountCents));

            BalanceCents = checked(BalanceCents + amountCents);
            BalanceChanged?.Invoke(BalanceCents);
        }

        public void Restore(long balanceCents)
        {
            if (balanceCents < 0)
                throw new ArgumentOutOfRangeException(nameof(balanceCents));

            if (BalanceCents == balanceCents)
                return;

            BalanceCents = balanceCents;
            BalanceChanged?.Invoke(BalanceCents);
        }

        private static void ValidatePositiveAmount(long amountCents, string parameterName)
        {
            if (amountCents <= 0)
                throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}