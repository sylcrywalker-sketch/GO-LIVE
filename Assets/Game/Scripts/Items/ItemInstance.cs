using System;

namespace GoLive.Items
{
    public enum ItemLocation
    {
        World,
        Carried,
        Inventory
    }

    public sealed class ItemInstance
    {
        public string InstanceId { get; }
        public string DefinitionId { get; }
        public ItemLocation Location { get; private set; }

        public ItemInstance(string instanceId, string definitionId, ItemLocation location)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("Instance ID cannot be empty.", nameof(instanceId));

            if (string.IsNullOrWhiteSpace(definitionId))
                throw new ArgumentException("Definition ID cannot be empty.", nameof(definitionId));

            InstanceId = instanceId;
            DefinitionId = definitionId;
            Location = location;
        }

        public static ItemInstance CreateNew(string definitionId)
        {
            return new ItemInstance(Guid.NewGuid().ToString("N"), definitionId, ItemLocation.World);
        }

        internal void MoveTo(ItemLocation location)
        {
            Location = location;
        }
    }
}