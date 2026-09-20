using System;

namespace GoLive.Needs
{
    public readonly struct PlayerNeedsRules
    {
        public float MaxHunger { get; }
        public float StartingHunger { get; }
        public float HungerPerGameHour { get; }
        public float MaxConcentration { get; }
        public float StartingConcentration { get; }

        public PlayerNeedsRules(float maxHunger, float startingHunger, float hungerPerGameHour, float maxConcentration, float startingConcentration)
        {
            if (maxHunger <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxHunger));

            if (startingHunger < 0f || startingHunger > maxHunger)
                throw new ArgumentOutOfRangeException(nameof(startingHunger));

            if (hungerPerGameHour < 0f)
                throw new ArgumentOutOfRangeException(nameof(hungerPerGameHour));

            if (maxConcentration <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maxConcentration));

            if (startingConcentration < 0f || startingConcentration > maxConcentration)
                throw new ArgumentOutOfRangeException(nameof(startingConcentration));

            MaxHunger = maxHunger;
            StartingHunger = startingHunger;
            HungerPerGameHour = hungerPerGameHour;
            MaxConcentration = maxConcentration;
            StartingConcentration = startingConcentration;
        }
    }

    public readonly struct PlayerNeedsSnapshot
    {
        public float Hunger { get; }
        public float MaxHunger { get; }
        public float Concentration { get; }
        public float MaxConcentration { get; }

        public float HungerNormalized => MaxHunger <= 0f ? 0f : Hunger / MaxHunger;
        public float ConcentrationNormalized => MaxConcentration <= 0f ? 0f : Concentration / MaxConcentration;

        public PlayerNeedsSnapshot(float hunger, float maxHunger, float concentration, float maxConcentration)
        {
            Hunger = hunger;
            MaxHunger = maxHunger;
            Concentration = concentration;
            MaxConcentration = maxConcentration;
        }
    }

    public sealed class PlayerNeeds
    {
        public PlayerNeedsSnapshot Current => new(_hunger, _rules.MaxHunger, _concentration, _rules.MaxConcentration);

        public event Action<PlayerNeedsSnapshot> Changed;

        private readonly PlayerNeedsRules _rules;

        private float _hunger;
        private float _concentration;

        public PlayerNeeds(PlayerNeedsRules rules)
        {
            _rules = rules;
            _hunger = rules.StartingHunger;
            _concentration = rules.StartingConcentration;
        }

        public void AdvanceTime(long gameSeconds)
        {
            if (gameSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));

            if (gameSeconds == 0 || _hunger >= _rules.MaxHunger)
                return;

            float gameHours = gameSeconds / 3600f;
            SetHunger(_hunger + _rules.HungerPerGameHour * gameHours);
        }

        public void SatisfyHunger(float amount)
        {
            ValidatePositiveAmount(amount, nameof(amount));
            SetHunger(_hunger - amount);
        }

        public void IncreaseHunger(float amount)
        {
            ValidatePositiveAmount(amount, nameof(amount));
            SetHunger(_hunger + amount);
        }

        public void RestoreConcentration(float amount)
        {
            ValidatePositiveAmount(amount, nameof(amount));
            SetConcentration(_concentration + amount);
        }

        public void ConsumeConcentration(float amount)
        {
            ValidatePositiveAmount(amount, nameof(amount));
            SetConcentration(_concentration - amount);
        }

        public void Restore(PlayerNeedsSnapshot snapshot)
        {
            if (snapshot.MaxHunger != _rules.MaxHunger || snapshot.MaxConcentration != _rules.MaxConcentration)
                throw new InvalidOperationException("Needs snapshot does not match the current needs rules.");

            float hunger = Math.Clamp(snapshot.Hunger, 0f, _rules.MaxHunger);
            float concentration = Math.Clamp(snapshot.Concentration, 0f, _rules.MaxConcentration);

            if (Approximately(_hunger, hunger) && Approximately(_concentration, concentration))
                return;

            _hunger = hunger;
            _concentration = concentration;

            Changed?.Invoke(Current);
        }

        private void SetHunger(float value)
        {
            float clamped = Math.Clamp(value, 0f, _rules.MaxHunger);

            if (Approximately(_hunger, clamped))
                return;

            _hunger = clamped;
            Changed?.Invoke(Current);
        }

        private void SetConcentration(float value)
        {
            float clamped = Math.Clamp(value, 0f, _rules.MaxConcentration);

            if (Approximately(_concentration, clamped))
                return;

            _concentration = clamped;
            Changed?.Invoke(Current);
        }

        private static void ValidatePositiveAmount(float amount, string parameterName)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || amount < 0f)
                throw new ArgumentOutOfRangeException(parameterName);
        }

        private static bool Approximately(float left, float right)
        {
            return Math.Abs(left - right) < 0.0001f;
        }
    }
}