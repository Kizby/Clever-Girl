using System;

namespace XRL.World.Parts.CleverGirl
{
    [Serializable]
    public class InteractListener : IPart {
        public override bool WantEvent(int ID, int cascade) =>
            base.WantEvent(ID, cascade) ||
            ID == OwnerGetInventoryActionsEvent.ID ||
            ID == InventoryActionEvent.ID;

        public override bool HandleEvent(OwnerGetInventoryActionsEvent E) {
            if (E.Actor == ParentObject && E.Object != null)
            {
                if (E.Object.IsPlayerLed() && !E.Object.IsPlayer())
                {
                    var aiPickupGear = E.Object.RequirePart<AIPickupGear>();
                    E.AddAction(aiPickupGear.ActionName, aiPickupGear.ActionDisplay, aiPickupGear.ActionCommand, aiPickupGear.ActionKey, true, WorksAtDistance: true);
                }
            }
            return true;
        }

        public override bool HandleEvent(InventoryActionEvent E) {
            if (E.Command == AIPickupGear.ENABLE_COMMAND) {
                E.Item.RequirePart<AIPickupGear>().Enabled = true;
                // Anyone picking up gear should know how to unburden themself
                E.Item.RequirePart<AIUnburden>().Enabled = true;
            }
            if (E.Command == AIPickupGear.DISABLE_COMMAND) {
                E.Item.RequirePart<AIPickupGear>().Enabled = false;
                E.Item.RequirePart<AIUnburden>().Enabled = false;
            }
            return true;
        }
    }
}