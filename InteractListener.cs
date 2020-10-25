using System;
using System.Collections.Generic;

namespace XRL.World.Parts.CleverGirl
{
    using XRL.World.CleverGirl;

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
                    var actions = new List<Utility.InventoryAction>{
                        E.Object.HasPart("AIPickupGear") ? AIPickupGear.DISABLE : AIPickupGear.ENABLE,
                        AIManageSkills.ACTION,
                    };
                    foreach (var action in actions) {
                        E.AddAction(action.Name, action.Display, action.Command, action.Key, true, WorksAtDistance: true);
                    }
                }
            }
            return true;
        }

        public override bool HandleEvent(InventoryActionEvent E) {
            if (E.Command == AIPickupGear.ENABLE.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                E.Item.RequirePart<AIPickupGear>();
                // Anyone picking up gear should know how to unburden themself
                E.Item.RequirePart<AIUnburden>();
                ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Enable Gear Pickup");
            }
            if (E.Command == AIPickupGear.DISABLE.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                E.Item.RemovePart<AIPickupGear>();
                E.Item.RemovePart<AIUnburden>();
                ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Disable Gear Pickup");
            }
            if (E.Command == AIManageSkills.ACTION.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                if (E.Item.RequirePart<AIManageSkills>().Manage()) {
                    ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Manage Skills");
                }
                E.RequestInterfaceExit();
            }
            return true;
        }
    }
}