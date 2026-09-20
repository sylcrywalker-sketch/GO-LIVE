using GoLive.GameTime;
using GoLive.Items;
using GoLive.Needs;
using GoLive.Player;
using UnityEngine;

namespace GoLive.Food
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCarry))]
    public sealed class PlayerFoodConsumption : MonoBehaviour
    {
        [SerializeField] private PlayerNeedsBehaviour _playerNeeds;
        [SerializeField] private GameClockBehaviour _gameClock;

        private PlayerCarry _playerCarry;
        private FoodConsumption _foodConsumption;

        private void Awake()
        {
            _playerCarry = GetComponent<PlayerCarry>();

            if (_playerNeeds != null && _gameClock != null)
                return;

            Debug.LogError($"{nameof(PlayerFoodConsumption)} on {name} requires Player Needs and Game Clock references.", this);
            enabled = false;
        }

        private void Start()
        {
            if (_playerNeeds.Needs == null || _gameClock.Clock == null)
            {
                Debug.LogError($"{nameof(PlayerFoodConsumption)} could not access initialized Needs or Game Clock.", this);
                enabled = false;
                return;
            }

            _foodConsumption = new FoodConsumption(_playerNeeds.Needs, _gameClock.Clock);
        }

        public bool CanConsume(WorldItem item, in FoodEffect effect)
        {
            return isActiveAndEnabled &&
                   _foodConsumption != null &&
                   item != null &&
                   _playerCarry.CarriedItem == item &&
                   item.Instance != null &&
                   item.Instance.Location == ItemLocation.Carried &&
                   _foodConsumption.CanConsume(in effect);
        }

        public bool TryConsume(WorldItem item, in FoodEffect effect)
        {
            if (!CanConsume(item, in effect))
                return false;

            if (!_playerCarry.TryRemoveCarriedItem(item))
                return false;

            _foodConsumption.Apply(in effect);
            return true;
        }
    }
}