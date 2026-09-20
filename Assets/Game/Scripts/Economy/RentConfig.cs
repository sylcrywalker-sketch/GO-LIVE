using GoLive.GameTime;
using UnityEngine;

namespace GoLive.Economy
{
    [CreateAssetMenu(fileName = "RentConfig", menuName = "GO! LIVE/Economy/Rent Config")]
    public sealed class RentConfig : ScriptableObject
    {
        [Header("Amounts")]
        [field: SerializeField, Min(1)] public long FirstPaymentCents { get; private set; } = 5000;
        [field: SerializeField, Min(1)] public long SecondPaymentCents { get; private set; } = 5000;
        [field: SerializeField, Min(0)] public long LatePenaltyCents { get; private set; } = 2000;

        [Header("Schedule")]
        [field: SerializeField, Min(1)] public int FirstDeadlineDay { get; private set; } = 3;
        [field: SerializeField, Min(1)] public int SecondBillDay { get; private set; } = 6;
        [field: SerializeField, Min(1)] public int FinalDeadlineDay { get; private set; } = 6;

        public bool IsValid =>
            FirstPaymentCents > 0 &&
            SecondPaymentCents > 0 &&
            LatePenaltyCents >= 0 &&
            FirstDeadlineDay >= 1 &&
            SecondBillDay > FirstDeadlineDay &&
            FinalDeadlineDay >= SecondBillDay;

        public RentRules CreateRules()
        {
            return new RentRules(
                FirstPaymentCents,
                SecondPaymentCents,
                LatePenaltyCents,
                FirstDeadlineDay,
                SecondBillDay,
                FinalDeadlineDay);
        }
    }

    public readonly struct RentRules
    {
        public long FirstPaymentCents { get; }
        public long SecondPaymentCents { get; }
        public long LatePenaltyCents { get; }
        public int FirstDeadlineDay { get; }
        public int SecondBillDay { get; }
        public int FinalDeadlineDay { get; }

        public long FirstDeadlineBoundarySeconds => FirstDeadlineDay * GameTimeSnapshot.SecondsPerDay;
        public long SecondBillBoundarySeconds => (SecondBillDay - 1L) * GameTimeSnapshot.SecondsPerDay;
        public long FinalDeadlineBoundarySeconds => FinalDeadlineDay * GameTimeSnapshot.SecondsPerDay;

        public RentRules(
            long firstPaymentCents,
            long secondPaymentCents,
            long latePenaltyCents,
            int firstDeadlineDay,
            int secondBillDay,
            int finalDeadlineDay)
        {
            FirstPaymentCents = firstPaymentCents;
            SecondPaymentCents = secondPaymentCents;
            LatePenaltyCents = latePenaltyCents;
            FirstDeadlineDay = firstDeadlineDay;
            SecondBillDay = secondBillDay;
            FinalDeadlineDay = finalDeadlineDay;
        }
    }
}