using UnityEngine;

namespace GoLive.GameTime
{
    [DisallowMultipleComponent]
    public sealed class GameClockBehaviour : MonoBehaviour
    {
        [SerializeField] private GameTimeConfig config;

        public GameClock Clock { get; private set; }
        public DayPhaseSchedule PhaseSchedule { get; private set; }
        public DayPhase CurrentPhase => PhaseSchedule.GetPhase(Clock.Current.MinuteOfDay);

        private void Awake()
        {
            if (!ValidateConfiguration())
            {
                enabled = false;
                return;
            }

            Clock = new GameClock(config.StartingDay, config.StartingHour, config.StartingMinute);
            PhaseSchedule = config.CreatePhaseSchedule();
        }

        private void Update()
        {
            double gameSeconds = UnityEngine.Time.deltaTime * config.GameMinutesPerRealMinute;
            Clock.AdvanceSeconds(gameSeconds);
        }

        public void AdvanceMinutes(double minutes)
        {
            if (Clock == null)
                return;

            Clock.AdvanceMinutes(minutes);
        }

        private bool ValidateConfiguration()
        {
            if (config == null)
            {
                Debug.LogError($"{nameof(GameClockBehaviour)} on {name} requires a Game Time Config.", this);
                return false;
            }

            if (!config.HasValidPhaseOrder)
            {
                Debug.LogError($"{nameof(GameTimeConfig)} requires Morning < Day < Evening < Night.", config);
                return false;
            }

            return true;
        }
    }
}