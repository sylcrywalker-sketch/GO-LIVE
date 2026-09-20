using System;
using System.Collections.Generic;
using GoLive.GameTime;
using UnityEngine;

namespace GoLive.Phone
{
    [Serializable]
    public sealed class PhoneMessageScheduleEntry
    {
        [SerializeField] private string messageId;
        [SerializeField] private string contactId;
        [SerializeField] private MessageDirection direction;
        [SerializeField, Min(1)] private int day = 1;
        [SerializeField, Range(0, 23)] private int hour;
        [SerializeField, Range(0, 59)] private int minute;
        [SerializeField] private string localizationKey;

        public string MessageId => messageId;
        public string ContactId => contactId;
        public MessageDirection Direction => direction;
        public string LocalizationKey => localizationKey;

        public long DueSeconds =>
            (day - 1L) * GameTimeSnapshot.SecondsPerDay +
            hour * 3600L +
            minute * 60L;

        public bool IsValid =>
            !string.IsNullOrWhiteSpace(messageId) &&
            !string.IsNullOrWhiteSpace(contactId) &&
            !string.IsNullOrWhiteSpace(localizationKey) &&
            day >= 1 &&
            hour is >= 0 and <= 23 &&
            minute is >= 0 and <= 59 &&
            (direction == MessageDirection.Incoming || direction == MessageDirection.Outgoing);
    }

    [CreateAssetMenu(fileName = "PhoneMessageSchedule", menuName = "GO! LIVE/Phone/Message Schedule")]
    public sealed class PhoneMessageScheduleConfig : ScriptableObject
    {
        [SerializeField] private PhoneMessageScheduleEntry[] entries = Array.Empty<PhoneMessageScheduleEntry>();

        public IReadOnlyList<PhoneMessageScheduleEntry> Entries => entries;

        public bool IsValid
        {
            get
            {
                if (entries == null)
                    return false;

                HashSet<string> messageIds = new(StringComparer.Ordinal);
                long previousDueSeconds = -1;

                for (int i = 0; i < entries.Length; i++)
                {
                    PhoneMessageScheduleEntry entry = entries[i];

                    if (entry == null ||
                        !entry.IsValid ||
                        !messageIds.Add(entry.MessageId) ||
                        entry.DueSeconds < previousDueSeconds)
                    {
                        return false;
                    }

                    previousDueSeconds = entry.DueSeconds;
                }

                return true;
            }
        }
    }
}