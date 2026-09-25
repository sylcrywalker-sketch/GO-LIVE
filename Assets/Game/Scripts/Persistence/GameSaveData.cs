using System;
using GoLive.Delivery;
using GoLive.Desktop;
using GoLive.Economy;
using GoLive.Items;
using GoLive.PcBuilding;
using GoLive.Phone;
using GoLive.Shop;
using UnityEngine;

namespace GoLive.Persistence
{
    [Serializable]
    public sealed class GameSaveData
    {
        public int Version;
        public long SavedAtUtcTicks;
        public PlayerSaveData Player;
        public long GameTimeSeconds;
        public float Hunger;
        public float Concentration;
        public long BalanceCents;
        public RentSaveData Rent;
        public PhoneMessagesSnapshot Messages;
        public ShopOrdersSnapshot Orders;
        public DeliverySnapshot Delivery;
        public PcAssemblySnapshot PcAssembly;
        public DesktopSnapshot Desktop;
        public ItemSaveData[] Items = Array.Empty<ItemSaveData>();
    }

    [Serializable]
    public sealed class PlayerSaveData
    {
        public Vector3 Position;
        public Quaternion Rotation;
        public float Pitch;
    }

    [Serializable]
    public sealed class RentSaveData
    {
        public long AmountDueCents;
        public bool FirstPaymentSettled;
        public bool FirstDeadlineMissed;
        public bool SecondBillIssued;
        public RentOutcome Outcome;
        public long ProcessedThroughSeconds;
    }

    [Serializable]
    public sealed class ItemSaveData
    {
        public string InstanceId;
        public string DefinitionId;
        public ItemLocation Location;
        public int InventoryIndex = -1;
        public Vector3 Position;
        public Quaternion Rotation;
    }
}
