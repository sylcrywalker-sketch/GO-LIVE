namespace GoLive.Inventory
{
    // What moving an item between the hands and the Inventory would do right now.
    // Store, Take and Swap are the transfers that can happen; every other value says why nothing would move.
    public enum InventoryTransfer
    {
        Store,
        Take,
        Swap,
        Unavailable,
        NothingCarried,
        NotStorable,
        InventoryFull,
        NotInInventory,
        HandsBusy
    }

    public static class InventoryTransferExtensions
    {
        public static bool IsAllowed(this InventoryTransfer transfer)
        {
            return transfer is InventoryTransfer.Store or InventoryTransfer.Take or InventoryTransfer.Swap;
        }
    }
}
