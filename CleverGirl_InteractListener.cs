using System;

namespace XRL.World.Parts
{
    [Serializable]
    public class CleverGirl_InteractListener : IPart {
        public override bool WantEvent(int ID, int cascade) =>
            base.WantEvent(ID, cascade) ||
            ID == OwnerGetInventoryActionsEvent.ID ||
            ID == InventoryActionEvent.ID;

        public override bool HandleEvent(OwnerGetInventoryActionsEvent E) {
            if (E.Actor == ParentObject && E.Object != null)
            {
                if (E.Object.IsPlayerLed() && !E.Object.IsPlayer())
                {
                    var lootPickup = E.Object.RequirePart<CleverGirl_LootPickup>();
                    E.AddAction(lootPickup.ActionName, lootPickup.ActionDisplay, lootPickup.ActionCommand, lootPickup.ActionKey, true, WorksAtDistance: true);
                }
            }
            return true;
        }

        public override bool HandleEvent(InventoryActionEvent E) {
            if (E.Command == CleverGirl_LootPickup.ENABLE_COMMAND) {
                E.Item.RequirePart<CleverGirl_LootPickup>().Enable();
            }
            if (E.Command == CleverGirl_LootPickup.DISABLE_COMMAND) {
                E.Item.RequirePart<CleverGirl_LootPickup>().Disable();
            }
            return true;
        }
    }
}