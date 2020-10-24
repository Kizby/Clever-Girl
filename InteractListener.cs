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
            if (E.Command == AIPickupGear.ENABLE.Command) {
                E.Item.RequirePart<AIPickupGear>();
                // Anyone picking up gear should know how to unburden themself
                E.Item.RequirePart<AIUnburden>();
            }
            if (E.Command == AIPickupGear.DISABLE.Command) {
                E.Item.RemovePart<AIPickupGear>();
                E.Item.RemovePart<AIUnburden>();
            }
            if (E.Command == AIManageSkills.ACTION.Command) {
                E.Item.RequirePart<AIManageSkills>().Manage();
                E.RequestInterfaceExit();
            }
            return true;
        }
    }
}