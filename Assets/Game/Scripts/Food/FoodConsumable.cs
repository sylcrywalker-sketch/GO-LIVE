using GoLive.Interaction;
using GoLive.Items;
using UnityEngine;

namespace GoLive.Food
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WorldItem))]
    public sealed class FoodConsumable : MonoBehaviour, IInteractable
    {
        private const string EatPromptKey = "interaction.eat";

        [SerializeField, Min(0f)] private float _hungerReduction = 25f;
        [SerializeField, Min(0f)] private float _concentrationRestore = 5f;
        [SerializeField, Min(0f)] private float _consumeMinutes = 5f;

        private WorldItem _worldItem;

        private FoodEffect Effect => new(_hungerReduction, _concentrationRestore, _consumeMinutes);

        private void Awake()
        {
            _worldItem = GetComponent<WorldItem>();

            if (Effect.IsValid)
                return;

            Debug.LogError($"{nameof(FoodConsumable)} on {name} contains invalid food values.", this);
            enabled = false;
        }

        public bool CanInteract(in InteractionContext context)
        {
            if (context.Action != InteractionAction.Use ||
                _worldItem.Instance == null ||
                _worldItem.Instance.Location != ItemLocation.Carried)
            {
                return false;
            }

            if (!context.Actor.TryGetComponent(out PlayerFoodConsumption consumption))
                return false;

            FoodEffect effect = Effect;
            return consumption.CanConsume(_worldItem, in effect);
        }

        public string GetPromptKey(in InteractionContext context)
        {
            return CanInteract(in context) ? EatPromptKey : null;
        }

        public void Interact(in InteractionContext context)
        {
            if (!context.Actor.TryGetComponent(out PlayerFoodConsumption consumption))
                return;

            FoodEffect effect = Effect;
            consumption.TryConsume(_worldItem, in effect);
        }
    }
}