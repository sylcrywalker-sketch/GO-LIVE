using System.Globalization;
using GoLive.Localization;
using TMPro;
using UnityEngine;

namespace GoLive.Economy
{
    [DisallowMultipleComponent]
    public sealed class RentHudView : MonoBehaviour
    {
        private const string FirstDueKey = "rent.first_due";
        private const string OverdueKey = "rent.overdue";
        private const string AwaitingSecondKey = "rent.awaiting_second";
        private const string FinalDueKey = "rent.final_due";
        private const string PaidKey = "rent.paid";
        private const string FailedKey = "rent.failed";

        [SerializeField] private RentBehaviour _rent;
        [SerializeField] private RentConfig _config;
        [SerializeField] private LocalizationContext _localization;
        [SerializeField] private TMP_Text _objectiveText;

        private bool _bound;

        private void Start()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            Bind();

            if (_rent.TryGetSnapshot(out RentSnapshot snapshot))
                Refresh(snapshot);
        }

        private void OnEnable()
        {
            if (_rent != null && _localization != null)
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

            _rent.Changed += Refresh;
            _localization.LanguageChanged += HandleLanguageChanged;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound)
                return;

            _rent.Changed -= Refresh;
            _localization.LanguageChanged -= HandleLanguageChanged;
            _bound = false;
        }

        private void HandleLanguageChanged(GameLanguage language)
        {
            if (_rent.TryGetSnapshot(out RentSnapshot snapshot))
                Refresh(snapshot);
        }

        private void Refresh(RentSnapshot snapshot)
        {
            RentPhase phase = GetPhase(snapshot);

            _objectiveText.text = phase switch
            {
                RentPhase.FirstPayment => _localization.Format(
                    FirstDueKey,
                    FormatMoney(snapshot.AmountDueCents),
                    _config.FirstDeadlineDay),

                RentPhase.FirstPaymentOverdue => _localization.Format(
                    OverdueKey,
                    FormatMoney(snapshot.AmountDueCents),
                    _config.SecondBillDay),

                RentPhase.AwaitingSecondBill => _localization.Format(
                    AwaitingSecondKey,
                    _config.SecondBillDay),

                RentPhase.FinalPayment => _localization.Format(
                    FinalDueKey,
                    FormatMoney(snapshot.AmountDueCents),
                    _config.FinalDeadlineDay),

                RentPhase.FinalPaymentPaid => _localization.Text(PaidKey),
                RentPhase.Completed => _localization.Text(PaidKey),
                RentPhase.Failed => _localization.Text(FailedKey),
                _ => string.Empty
            };
        }

        private RentPhase GetPhase(RentSnapshot snapshot)
        {
            if (snapshot.Outcome == RentOutcome.Succeeded)
                return RentPhase.Completed;

            if (snapshot.Outcome == RentOutcome.Failed)
                return RentPhase.Failed;

            if (snapshot.SecondBillIssued)
                return snapshot.AmountDueCents > 0 ? RentPhase.FinalPayment : RentPhase.FinalPaymentPaid;

            if (snapshot.FirstPaymentSettled)
                return RentPhase.AwaitingSecondBill;

            return snapshot.FirstDeadlineMissed ? RentPhase.FirstPaymentOverdue : RentPhase.FirstPayment;
        }

        private bool ValidateConfiguration()
        {
            if (_rent != null && _config != null && _localization != null && _objectiveText != null)
                return true;

            Debug.LogError($"{nameof(RentHudView)} on {name} has incomplete configuration.", this);
            return false;
        }

        private static string FormatMoney(long cents)
        {
            decimal dollars = cents / 100m;
            return $"${dollars.ToString("0.00", CultureInfo.InvariantCulture)}";
        }
    }
}