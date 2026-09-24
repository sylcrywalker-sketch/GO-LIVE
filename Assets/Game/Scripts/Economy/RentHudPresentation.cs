using System;
using System.Globalization;
using GoLive.GameTime;
using GoLive.Localization;

namespace GoLive.Economy
{
    public enum RentHudStatus
    {
        DaysLeft,
        DueToday,
        Overdue,
        PaidNextDue,
        Paid,
        Unpaid
    }

    public readonly struct RentHudState
    {
        public RentHudStatus Status { get; }
        public long AmountCents { get; }
        public int DaysLeft { get; }

        public RentHudState(RentHudStatus status, long amountCents, int daysLeft)
        {
            Status = status;
            AmountCents = amountCents;
            DaysLeft = daysLeft;
        }
    }

    // The two localized halves of the HUD line: "Аренда: $50.00" and "осталось 2 дня" (Status is null when there is none).
    public readonly struct RentHudText
    {
        public string Lead { get; }
        public string Status { get; }

        public RentHudText(string lead, string status)
        {
            Lead = lead;
            Status = status;
        }
    }

    // Projects the rent account onto the HUD objective line. The one owner of the "days left" rule: a payment is due by
    // the end of its deadline day (RentRules), so the count is deadline day minus today and the deadline day itself says
    // "due today".
    public static class RentHudPresentation
    {
        public const string AmountKey = "rent.hud.amount";
        public const string DaysLeftKey = "rent.hud.days_left";
        public const string DueTodayKey = "rent.hud.due_today";
        public const string OverdueKey = "rent.hud.overdue";
        public const string PaidKey = "rent.hud.paid";
        public const string NextDueKey = "rent.hud.next_due";
        public const string UnpaidKey = "rent.hud.unpaid";

        public static RentHudState Evaluate(RentRules rules, RentSnapshot rent, GameTimeSnapshot now)
        {
            return rent.Phase switch
            {
                RentPhase.FirstPayment => Due(rent.AmountDueCents, rules.FirstDeadlineDay - now.Day),
                RentPhase.FirstPaymentOverdue => new RentHudState(RentHudStatus.Overdue, rent.AmountDueCents, 0),
                RentPhase.AwaitingSecondBill => new RentHudState(RentHudStatus.PaidNextDue, rules.SecondPaymentCents, rules.FinalDeadlineDay - now.Day),
                RentPhase.FinalPayment => Due(rent.AmountDueCents, rules.FinalDeadlineDay - now.Day),
                RentPhase.FinalPaymentPaid => new RentHudState(RentHudStatus.Paid, 0, 0),
                RentPhase.Completed => new RentHudState(RentHudStatus.Paid, 0, 0),
                RentPhase.Failed => new RentHudState(RentHudStatus.Unpaid, rent.AmountDueCents, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(rent), rent.Phase, "Unknown rent phase.")
            };
        }

        public static RentHudText Localize(RentHudState state, LocalizationContext localization)
        {
            if (localization == null)
                throw new ArgumentNullException(nameof(localization));

            string amount = FormatMoney(state.AmountCents);

            return state.Status switch
            {
                RentHudStatus.DaysLeft => new RentHudText(localization.Format(AmountKey, amount), localization.FormatCount(DaysLeftKey, state.DaysLeft)),
                RentHudStatus.DueToday => new RentHudText(localization.Format(AmountKey, amount), localization.Text(DueTodayKey)),
                RentHudStatus.Overdue => new RentHudText(localization.Format(AmountKey, amount), localization.Text(OverdueKey)),
                RentHudStatus.PaidNextDue => new RentHudText(localization.Text(PaidKey), localization.FormatCount(NextDueKey, state.DaysLeft)),
                RentHudStatus.Paid => new RentHudText(localization.Text(PaidKey), null),
                RentHudStatus.Unpaid => new RentHudText(localization.Text(UnpaidKey), null),
                _ => throw new ArgumentOutOfRangeException(nameof(state), state.Status, "Unknown rent HUD status.")
            };
        }

        public static string FormatMoney(long cents)
        {
            decimal dollars = cents / 100m;
            return $"${dollars.ToString("0.00", CultureInfo.InvariantCulture)}";
        }

        private static RentHudState Due(long amountCents, int daysLeft)
        {
            return daysLeft > 0
                ? new RentHudState(RentHudStatus.DaysLeft, amountCents, daysLeft)
                : new RentHudState(RentHudStatus.DueToday, amountCents, 0);
        }
    }
}
