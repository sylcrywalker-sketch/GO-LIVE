using System;
using GoLive.GameTime;
using GoLive.Needs;

namespace GoLive.Food
{
    public readonly struct FoodEffect
    {
        public float HungerReduction { get; }
        public float ConcentrationRestore { get; }
        public double ConsumeMinutes { get; }

        public bool IsValid =>
            HungerReduction >= 0f &&
            ConcentrationRestore >= 0f &&
            ConsumeMinutes >= 0d &&
            !float.IsNaN(HungerReduction) &&
            !float.IsInfinity(HungerReduction) &&
            !float.IsNaN(ConcentrationRestore) &&
            !float.IsInfinity(ConcentrationRestore) &&
            !double.IsNaN(ConsumeMinutes) &&
            !double.IsInfinity(ConsumeMinutes);

        public FoodEffect(float hungerReduction, float concentrationRestore, double consumeMinutes)
        {
            HungerReduction = hungerReduction;
            ConcentrationRestore = concentrationRestore;
            ConsumeMinutes = consumeMinutes;
        }
    }

    public sealed class FoodConsumption
    {
        private readonly PlayerNeeds _needs;
        private readonly GameClock _clock;

        public FoodConsumption(PlayerNeeds needs, GameClock clock)
        {
            _needs = needs ?? throw new ArgumentNullException(nameof(needs));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public bool CanConsume(in FoodEffect effect)
        {
            return effect.IsValid && _needs.Current.Hunger > 0f;
        }

        public void Apply(in FoodEffect effect)
        {
            if (!CanConsume(in effect))
                throw new InvalidOperationException("Food cannot be consumed in the current state.");

            if (effect.ConsumeMinutes > 0d)
                _clock.AdvanceMinutes(effect.ConsumeMinutes);

            if (effect.HungerReduction > 0f)
                _needs.SatisfyHunger(effect.HungerReduction);

            if (effect.ConcentrationRestore > 0f)
                _needs.RestoreConcentration(effect.ConcentrationRestore);
        }
    }
}