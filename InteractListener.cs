using System;
using System.Collections.Generic;

namespace XRL.World.Parts
{
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using XRL.World.CleverGirl;

    [Serializable]
    public class CleverGirl_InteractListener : IPart, IXmlSerializable {
        public override bool WantEvent(int ID, int cascade) =>
            base.WantEvent(ID, cascade) ||
            ID == OwnerGetInventoryActionsEvent.ID ||
            ID == InventoryActionEvent.ID;

        public override bool HandleEvent(OwnerGetInventoryActionsEvent E) {
            if (E.Actor == ParentObject && E.Object != null)
            {
                if (E.Object.IsPlayerLed() && !E.Object.IsPlayer())
                {
                    if (E.Object.HasPart(typeof(CannotBeInfluenced))) {
                        // don't manage someone who can't be managed
                        return true;
                    }
                    var actions = new List<Utility.InventoryAction>{
                        E.Object.HasPart("CleverGirl_AIPickupGear") ? CleverGirl_AIPickupGear.DISABLE : CleverGirl_AIPickupGear.ENABLE,
                        CleverGirl_AIManageSkills.ACTION,
                        CleverGirl_AIManageMutations.ACTION,
                        CleverGirl_AIManageAttributes.ACTION,
                        ManageGear.ACTION,
                    };
                    foreach (var action in actions) {
                        E.AddAction(action.Name, action.Display, action.Command, action.Key, true, WorksAtDistance: true);
                    }
                }
            }
            return true;
        }

        public override bool HandleEvent(InventoryActionEvent E) {
            if (E.Command == CleverGirl_AIPickupGear.ENABLE.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                E.Item.RequirePart<CleverGirl_AIPickupGear>();
                // Anyone picking up gear should know how to unburden themself
                E.Item.RequirePart<CleverGirl_AIUnburden>();
                ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Enable Gear Pickup");
            }
            if (E.Command == CleverGirl_AIPickupGear.DISABLE.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                E.Item.RemovePart<CleverGirl_AIPickupGear>();
                E.Item.RemovePart<CleverGirl_AIUnburden>();
                ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Disable Gear Pickup");
            }
            if (E.Command == CleverGirl_AIManageSkills.ACTION.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                if (E.Item.RequirePart<CleverGirl_AIManageSkills>().Manage()) {
                    ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Manage Skills");
                }
                E.RequestInterfaceExit();
            }
            if (E.Command == CleverGirl_AIManageMutations.ACTION.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                if (E.Item.RequirePart<CleverGirl_AIManageMutations>().Manage()) {
                    ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Manage Mutations");
                }
                E.RequestInterfaceExit();
            }
            if (E.Command == CleverGirl_AIManageAttributes.ACTION.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                if (E.Item.RequirePart<CleverGirl_AIManageAttributes>().Manage()) {
                    ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Manage Attributes");
                }
                E.RequestInterfaceExit();
            }
            if (E.Command == ManageGear.ACTION.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                if (ManageGear.Manage(E.Item)) {
                    ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Manage Gear");
                }
                E.RequestInterfaceExit();
            }
            return true;
        }

        // XMLSerialization for compatibility with Armithaig's Recur mod
        public XmlSchema GetSchema() => null;

        // no actual state to write beyond the existence of this part
        public void WriteXml(XmlWriter writer) {}
        public void ReadXml(XmlReader reader) {
            reader.Skip();
        }
    }
}