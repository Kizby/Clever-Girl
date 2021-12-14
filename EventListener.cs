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
            ID == CommandEvent.ID ||
            ID == GetCookingActionsEvent.ID;

        public override bool HandleEvent(OwnerGetInventoryActionsEvent E) {
            if (E.Actor == ParentObject && E.Object?.IsPlayerLed() == true && !E.Object.IsPlayer()) {
                if (E.Object.HasPart(typeof(CannotBeInfluenced))) {
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
                    if (action.Valid(E)) {
                        _ = E.AddAction(action.Name, action.Display, action.Command, Key: action.Key, FireOnActor: true, WorksAtDistance: true);
                    }
                }
            }
            return true;
        }

        public override bool HandleEvent(InventoryActionEvent E) {
            if (E.Command == CleverGirl_AIPickupGear.ENABLE.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                _ = E.Item.RequirePart<CleverGirl_AIPickupGear>();
                // Anyone picking up gear should know how to unburden themself
                _ = E.Item.RequirePart<CleverGirl_AIUnburden>();
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
                if (ManageGear.Manage(E.Actor, E.Item)) {
                    ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Manage Gear");
                }
                E.RequestInterfaceExit();
            }
            if (E.Command == Feed.ACTION.Command && ParentObject.CheckCompanionDirection(E.Item)) {
                if (Feed.DoFeed(E.Actor, E.Item)) {
                    ParentObject.CompanionDirectionEnergyCost(E.Item, 100, "Feed");
                }
                E.RequestInterfaceExit();
            }
            if (E.Command == Feed.COOKING_ACTION.Command) {
                if (Feed.CollectFeedableCompanions(E.Actor).Count == 0) {
                    Popup.Show("None of your companions are nearby!");
                } else {
                    int EnergyCost = 100;
                    if (Feed.DoFeed(E.Actor, ref EnergyCost)) {
                        ParentObject.CompanionDirectionEnergyCost(E.Item, EnergyCost, "Feed Companions");
                    }
                    E.RequestInterfaceExit();
                }
            }
            return true;
        }

        public override bool HandleEvent(CommandEvent E) {
            if (E.Command == "CleverGirl_CmdWaitUntilPartyHealed" && !AutoAct.ShouldHostilesInterrupt("r", popSpot: true)) {
                AutoAct.Setting = "r";
                The.Game.ActionManager.RestingUntilHealed = true;
                The.Game.ActionManager.RestingUntilHealedCount = 0;
                RestingUntilPartyHealed = true;
                _ = ParentObject.UseEnergy(1000, "Pass");
                Loading.SetLoadingStatus("Resting until party healed...");
            }
            return true;
        }

        public override bool HandleEvent(GetCookingActionsEvent E) {
            var action = Feed.COOKING_ACTION;
            _ = E.AddAction(action.Name,
                            Campfire.EnabledDisplay(Feed.CollectFeedableCompanions(E.Actor).Count > 0, action.Display),
                            action.Command,
                            Key: action.Key,
                            FireOnActor: true);
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
