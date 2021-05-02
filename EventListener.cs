namespace XRL.World.Parts {
    using System;
    using System.Collections.Generic;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using XRL.UI;
    using XRL.World.Capabilities;
    using XRL.World.CleverGirl;

    [Serializable]
    public class CleverGirl_EventListener : IPart, IXmlSerializable {
        public bool RestingUntilPartyHealed;
        public override bool WantEvent(int ID, int cascade) =>
            base.WantEvent(ID, cascade) ||
            ID == OwnerGetInventoryActionsEvent.ID ||
            ID == InventoryActionEvent.ID ||
            ID == CommandEvent.ID;

        public override bool HandleEvent(OwnerGetInventoryActionsEvent e) {
            if (e.Actor == ParentObject && e.Object?.IsPlayerLed() == true && !e.Object.IsPlayer()) {
                if (e.Object.HasPart(typeof(CannotBeInfluenced))) {
                    // don't manage someone who can't be managed
                    return true;
                }
                var actions = new List<Utility.InventoryAction>{
                        CleverGirl_AIPickupGear.ENABLE,
                        CleverGirl_AIPickupGear.DISABLE,
                        CleverGirl_AIManageSkills.ACTION,
                        CleverGirl_AIManageMutations.ACTION,
                        CleverGirl_AIManageAttributes.ACTION,
                        ManageGear.ACTION,
                        Feed.ACTION,
                    };
                foreach (var action in actions) {
                    if (action.Valid(e)) {
                        _ = e.AddAction(action.Name, action.Display, action.Command, Key: action.Key, FireOnActor: true, WorksAtDistance: true);
                    }
                }
            }
            return true;
        }

        public override bool HandleEvent(InventoryActionEvent e) {
            if (e.Command == CleverGirl_AIPickupGear.ENABLE.Command && ParentObject.CheckCompanionDirection(e.Item)) {
                _ = e.Item.RequirePart<CleverGirl_AIPickupGear>();
                // Anyone picking up gear should know how to unburden themself
                _ = e.Item.RequirePart<CleverGirl_AIUnburden>();
                ParentObject.CompanionDirectionEnergyCost(e.Item, 100, "Enable Gear Pickup");
            }
            if (e.Command == CleverGirl_AIPickupGear.DISABLE.Command && ParentObject.CheckCompanionDirection(e.Item)) {
                e.Item.RemovePart<CleverGirl_AIPickupGear>();
                e.Item.RemovePart<CleverGirl_AIUnburden>();
                ParentObject.CompanionDirectionEnergyCost(e.Item, 100, "Disable Gear Pickup");
            }
            if (e.Command == CleverGirl_AIManageSkills.ACTION.Command && ParentObject.CheckCompanionDirection(e.Item)) {
                if (e.Item.RequirePart<CleverGirl_AIManageSkills>().Manage()) {
                    ParentObject.CompanionDirectionEnergyCost(e.Item, 100, "Manage Skills");
                }
                e.RequestInterfaceExit();
            }
            if (e.Command == CleverGirl_AIManageMutations.ACTION.Command && ParentObject.CheckCompanionDirection(e.Item)) {
                if (e.Item.RequirePart<CleverGirl_AIManageMutations>().Manage()) {
                    ParentObject.CompanionDirectionEnergyCost(e.Item, 100, "Manage Mutations");
                }
                e.RequestInterfaceExit();
            }
            if (e.Command == CleverGirl_AIManageAttributes.ACTION.Command && ParentObject.CheckCompanionDirection(e.Item)) {
                if (e.Item.RequirePart<CleverGirl_AIManageAttributes>().Manage()) {
                    ParentObject.CompanionDirectionEnergyCost(e.Item, 100, "Manage Attributes");
                }
                e.RequestInterfaceExit();
            }
            if (e.Command == ManageGear.ACTION.Command && ParentObject.CheckCompanionDirection(e.Item)) {
                if (ManageGear.Manage(e.Actor, e.Item)) {
                    ParentObject.CompanionDirectionEnergyCost(e.Item, 100, "Manage Gear");
                }
                e.RequestInterfaceExit();
            }
            if (e.Command == Feed.ACTION.Command && ParentObject.CheckCompanionDirection(e.Item)) {
                if (Feed.DoFeed(e.Actor, e.Item)) {
                    ParentObject.CompanionDirectionEnergyCost(e.Item, 100, "Feed");
                }
                e.RequestInterfaceExit();
            }
            return true;
        }

        public bool HandleCommandEvent(CommandEvent e) {
            Utility.MaybeLog("Command Event: " + e.Command);
            if (e.Command == "CmdWaitUntilPartyHealed" && !AutoAct.ShouldHostilesInterrupt("r", popSpot: true)) {
                AutoAct.Setting = "r";
                The.Game.ActionManager.RestingUntilHealed = true;
                The.Game.ActionManager.RestingUntilHealedCount = 0;
                RestingUntilPartyHealed = true;
                _ = ParentObject.UseEnergy(1000, "Pass");
                Loading.SetLoadingStatus("Resting until party healed...");
            }
            return true;
        }

        /// <summary>
        /// XMLSerialization for compatibility with Armithaig's Recur mod
        /// </summary>
        public XmlSchema GetSchema() => null;

        /// <summary>
        /// no actual state to write beyond the existence of this part
        /// </summary>
        /// <param name="writer"></param>
        public void WriteXml(XmlWriter writer) { }
        public void ReadXml(XmlReader reader) {
            reader.Skip();
        }
    }
}
