using System;

namespace XRL.World.Parts.CleverGirl
{
    [Serializable]
    public class AIPickupGear : IPart {
        public const string ENABLE_COMMAND = "CleverGirl_EnableGearPickup";
        public const string DISABLE_COMMAND = "CleverGirl_DisableGearPickup";

        public bool Enabled { get; private set; } = false;
        public string ActionName => Enabled ? "Clever Girl - Disable Gear Pickup" : "Clever Girl - Enable Gear Pickup";
        public string ActionDisplay => Enabled ? "disable gear {{inventoryhotkey|p}}ickup" : "enable gear {{inventoryhotkey|p}}ickup";
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