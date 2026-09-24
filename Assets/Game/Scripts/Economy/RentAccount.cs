using System;
using GoLive.GameTime;

namespace GoLive.Economy
{
    public enum RentOutcome
    {
        Active,
        Succeeded,
        Failed
    }

    public enum RentPhase
    {
        FirstPayment,
        FirstPaymentOverdue,
        AwaitingSecondBill,
        FinalPayment,
        FinalPaymentPaid,
        Completed,
        Failed
    }

    public readonly struct RentSnapshot
    {
        public long AmountDueCents { get; }
        public bool FirstPaymentSettled { get; }
        public bool FirstDeadlineMissed { get; }
        public bool SecondBillIssued { get; }
        public RentOutcome Outcome { get; }
        public long ProcessedThroughSeconds { get; }

        public RentPhase Phase
        {
            get
            {
                if (Outcome == RentOutcome.Succeeded)
                    return RentPhase.Completed;

                if (Outcome == RentOutcome.Failed)
                    return RentPhase.Failed;

                if (SecondBillIssued)
                    return AmountDueCents > 0 ? RentPhase.FinalPayment : RentPhase.FinalPaymentPaid;

                if (FirstPaymentSettled)
                    return RentPhase.AwaitingSecondBill;

                return FirstDeadlineMissed ? RentPhase.FirstPaymentOverdue : RentPhase.FirstPayment;
            }
        }

        public RentSnapshot(
            long amountDueCents,
            bool firstPaymentSettled,
            bool firstDeadlineMissed,
            bool secondBillIssued,
            RentOutcome outcome,
            long processedThroughSeconds)
        {
            AmountDueCents = amountDueCents;
            FirstPaymentSettled = firstPaymentSettled;
            FirstDeadlineMissed = firstDeadlineMissed;
            SecondBillIssued = secondBillIssued;
            Outcome = outcome;
            ProcessedThroughSeconds = processedThroughSeconds;
        }
    }

    public sealed class RentAccount
    {
        public long AmountDueCents { get; private set; }
        public bool FirstPaymentSettled { get; private set; }
        public bool FirstDeadlineMissed { get; private set; }
        public bool SecondBillIssued { get; private set; }
        public RentOutcome Outcome { get; private set; } = RentOutcome.Active;
        public long ProcessedThroughSeconds { get; private set; }

        public RentPhase Phase => Current.Phase;

        public RentSnapshot Current => new(
            AmountDueCents,
            FirstPaymentSettled,
            FirstDeadlineMissed,
            SecondBillIssued,
            Outcome,
            ProcessedThroughSeconds);

        public event Action<RentSnapshot> Changed;

        private readonly RentRules _rules;

        public RentAccount(RentRules rules, GameTimeSnapshot currentTime)
        {
            _rules = rules;
            AmountDueCents = rules.FirstPaymentCents;
            AdvanceTo(currentTime.TotalSeconds, false);
        }

        public bool TryPay(Wallet wallet)
        {
            if (wallet == null)
                throw new ArgumentNullException(nameof(wallet));

            if (Outcome != RentOutcome.Active || AmountDueCents <= 0)
                return false;

            long amount = AmountDueCents;

            if (!wallet.TrySpend(amount))
                return false;

            AmountDueCents = 0;

            if (!FirstPaymentSettled)
                FirstPaymentSettled = true;

            Changed?.Invoke(Current);
            return true;
        }

        public void Advance(GameTimeAdvance advance)
        {
            AdvanceTo(advance.Current.TotalSeconds, true);
        }

        public void Restore(RentSnapshot snapshot)
        {
            if (snapshot.AmountDueCents < 0 || snapshot.ProcessedThroughSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(snapshot));

            AmountDueCents = snapshot.AmountDueCents;
            FirstPaymentSettled = snapshot.FirstPaymentSettled;
            FirstDeadlineMissed = snapshot.FirstDeadlineMissed;
            SecondBillIssued = snapshot.SecondBillIssued;
            Outcome = snapshot.Outcome;
            ProcessedThroughSeconds = snapshot.ProcessedThroughSeconds;

            Changed?.Invoke(Current);
        }

        private void AdvanceTo(long totalSeconds, bool notify)
        {
            if (totalSeconds < ProcessedThroughSeconds)
                throw new InvalidOperationException("Rent time cannot move backwards without restoring a RentSnapshot.");

            bool changed = false;

            if (Crossed(_rules.FirstDeadlineBoundarySeconds, totalSeconds) && !FirstPaymentSettled)
            {
                FirstDeadlineMissed = true;
                AmountDueCents = checked(AmountDueCents + _rules.LatePenaltyCents);
                changed = true;
            }

            if (Crossed(_rules.SecondBillBoundarySeconds, totalSeconds) && !SecondBillIssued)
            {
                SecondBillIssued = true;
                AmountDueCents = checked(AmountDueCents + _rules.SecondPaymentCents);
                changed = true;
            }

            if (Crossed(_rules.FinalDeadlineBoundarySeconds, totalSeconds) && Outcome == RentOutcome.Active)
            {
                Outcome = AmountDueCents == 0 ? RentOutcome.Succeeded : RentOutcome.Failed;
                changed = true;
            }

            ProcessedThroughSeconds = totalSeconds;

            if (notify && changed)
                Changed?.Invoke(Current);
        }

        private bool Crossed(long boundarySeconds, long currentSeconds)
        {
            return ProcessedThroughSeconds < boundarySeconds && currentSeconds >= boundarySeconds;
        }
    }
}