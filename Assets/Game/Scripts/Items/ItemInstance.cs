using System;

namespace GoLive.Items
{
    // Where the one physical item is. Installed hardware sits inside a PC; which slot holds it is owned by the
    // PC assembly, not by the item.
    public enum ItemLocation
    {
        World,
        Carried,
        Inventory,
        Removed,
        Installed
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

        public bool TryMove(ItemLocation expectedLocation, ItemLocation destination)
        {
            if (Location != expectedLocation || destination == expectedLocation || Location == ItemLocation.Removed)
                return false;

            Location = destination;
            return true;
        }
    }
}