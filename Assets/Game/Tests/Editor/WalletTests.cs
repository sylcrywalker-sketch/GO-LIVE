using System;
using GoLive.Economy;
using NUnit.Framework;

namespace GoLive.Tests
{
    // The spending contract ShopCheckout's commit relies on: refuse without side effects, or charge and only then notify.
    public sealed class WalletTests
    {
        [Test]
        public void RefusedSpendChangesNothingAndStaysQuiet()
        {
            Wallet wallet = new(500);
            int notifications = 0;
            wallet.BalanceChanged += _ => notifications++;

            Assert.That(wallet.TrySpend(501), Is.False);

            Assert.That(wallet.BalanceCents, Is.EqualTo(500));
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void SpendChargesBeforeItNotifies()
        {
            Wallet wallet = new(500);
            long seen = -1;
            wallet.BalanceChanged += balance => seen = wallet.BalanceCents;

            Assert.That(wallet.TrySpend(200), Is.True);

            Assert.That(seen, Is.EqualTo(300));
            Assert.That(wallet.BalanceCents, Is.EqualTo(300));
        }

        [Test]
        public void SubscriberExceptionReachesTheCallerAfterTheCharge()
        {
            Wallet wallet = new(500);
            wallet.BalanceChanged += _ => throw new InvalidOperationException("subscriber failure");

            Assert.That(() => wallet.TrySpend(200), Throws.InvalidOperationException);
            Assert.That(wallet.BalanceCents, Is.EqualTo(300));
        }
    }
}
