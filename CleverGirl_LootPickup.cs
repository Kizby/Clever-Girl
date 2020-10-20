using System;

namespace XRL.World.Parts
{
    [Serializable]
    public class CleverGirl_LootPickup : IPart {
        public const string ENABLE_COMMAND = "CleverGirl_EnableLootPickup";
        public const string DISABLE_COMMAND = "CleverGirl_DisableLootPickup";

        public bool Enabled { get; private set; } = false;
        public string ActionName => Enabled ? "Clever Girl - Disable Loot Pickup" : "Clever Girl - Enable Loot Pickup";
        public string ActionDisplay => Enabled ? "disable loot {{inventoryhotkey|p}}ickup" : "enable loot {{inventoryhotkey|p}}ickup";
        public string ActionCommand => Enabled ? DISABLE_COMMAND : ENABLE_COMMAND;
        public char ActionKey => 'p';

        public override bool WantEvent(int ID, int cascade) => 
            base.WantEvent(ID, cascade) ||
            ID == InventoryActionEvent.ID;

        public void Enable() {
            Enabled = true;
        }
        
        public void Disable() {
            Enabled = false;
        }
    }
}