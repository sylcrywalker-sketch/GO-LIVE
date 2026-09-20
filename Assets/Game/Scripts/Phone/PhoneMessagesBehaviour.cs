using GoLive.GameTime;
using UnityEngine;
using System.Collections.Generic;

namespace GoLive.Phone
{
    [DisallowMultipleComponent]
    public sealed class PhoneMessagesBehaviour : MonoBehaviour
    {
        [SerializeField] private GameClockBehaviour gameClock;
        [SerializeField] private PhoneMessageScheduleConfig schedule;

        public PhoneMessages Messages { get; } = new();

        private GameClock _clock;
        private bool _started;
        private bool _bound;

        private void Awake()
        {
            if (gameClock == null)
            {
                Debug.LogError($"{nameof(PhoneMessagesBehaviour)} on {name} requires a Game Clock.", this);
                enabled = false;
                return;
            }

            if (schedule == null)
            {
                Debug.LogError($"{nameof(PhoneMessagesBehaviour)} on {name} requires a Message Schedule.", this);
                enabled = false;
                return;
            }

            if (!schedule.IsValid)
            {
                Debug.LogError($"{nameof(PhoneMessageScheduleConfig)} assigned to {name} contains invalid values.", schedule);
                enabled = false;
            }
        }

        private void Start()
        {
            if (!isActiveAndEnabled)
                return;

            _clock = gameClock.Clock;

            if (_clock == null)
            {
                Debug.LogError($"{nameof(PhoneMessagesBehaviour)} could not access initialized Game Clock.", this);
                enabled = false;
                return;
            }

            _started = true;

            Bind();
            ApplyDueMessages(_clock.Current);
        }

        private void OnEnable()
        {
            if (!_started)
                return;

            Bind();
            ApplyDueMessages(_clock.Current);
        }

        private void OnDisable()
        {
            Unbind();
        }

        private void Bind()
        {
            if (_bound || _clock == null)
                return;

            _clock.MinuteChanged += HandleMinuteChanged;
            _bound = true;
        }

        private void Unbind()
        {
            if (!_bound || _clock == null)
                return;

            _clock.MinuteChanged -= HandleMinuteChanged;
            _bound = false;
        }

        private void HandleMinuteChanged(GameTimeSnapshot time)
        {
            ApplyDueMessages(time);
        }

        private void ApplyDueMessages(GameTimeSnapshot time)
        {
            IReadOnlyList<PhoneMessageScheduleEntry> entries = schedule.Entries;

            for (int i = 0; i < entries.Count; i++)
            {
                PhoneMessageScheduleEntry entry = entries[i];

                if (time.TotalSeconds < entry.DueSeconds)
                    break;

                MessageContent content = MessageContent.FromLocalizationKey(entry.LocalizationKey);
                GameTimeSnapshot timestamp = new(entry.DueSeconds);

                if (entry.Direction == MessageDirection.Incoming)
                    Messages.TryAddIncoming(entry.MessageId, entry.ContactId, content, timestamp);
                else
                    Messages.TryAddOutgoing(entry.MessageId, entry.ContactId, content, timestamp);
            }
        }
    }
}