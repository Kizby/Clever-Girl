namespace XRL.World.Parts.CleverGirl {
    using HarmonyLib;
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using XRL.UI;
    using XRL.World.CleverGirl;

    // disable smart use if we might be interfacing companions
    [HarmonyPatch(typeof(CyberneticsTerminal2), "HandleEvent", new Type[] { typeof(CanSmartUseEvent) })]
    public static class CyberneticsTerminal2_HandleEvent_CanSmartUseEvent_Patch {
        public static void Postfix(CanSmartUseEvent E, ref bool __result) {
            __result = __result || Utility.CollectNearbyCompanions(E.Actor).Any(c => c.IsTrueKin());
        }
    }

    // add "interface a companion" option to terminal
    [HarmonyPatch(typeof(CyberneticsTerminal2), "HandleEvent", new Type[] { typeof(GetInventoryActionsEvent) })]
    public static class CyberneticsTerminal2_HandleEvent_GetInventoryActionsEvent_Patch {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction {
            Name = "Clever Girl - Interface Companion",
            Display = "interface a companion",
            Command = "CleverGirl_Interface",
            Key = 'c',
            Valid = E => Utility.CollectNearbyCompanions(E.Actor).Any(c => c.IsTrueKin()),
        };
        public static void Postfix(GetInventoryActionsEvent E) {
            if (ACTION.Valid(E)) {
                _ = E.AddAction(ACTION.Name, ACTION.Display, ACTION.Command, Key: ACTION.Key, FireOnActor: true);
            }
        }
    }

    // include player inventory in collection of possible implants and credits
    [HarmonyPatch(typeof(CyberneticsTerminal), "set_currentScreen")]
    public static class CyberneticsTerminal_set_currentScreen_Patch {
        public static void Postfix(CyberneticsTerminal __instance) {
            if (__instance.obj == The.Player) {
                return;
            }
            The.Player.ForeachInventoryAndEquipment(obj => {
                if ((obj.GetPart<CyberneticsCreditWedge>() is CyberneticsCreditWedge part) && part.Credits > 0) {
                    __instance.nCredits += part.Credits * obj.Count;
                    __instance.Wedges.Add(part);
                }
            });
            The.Player.Inventory?.ForeachObject(obj => {
                if (obj.IsImplant && obj.Understood()) {
                    __instance.Implants.Add(obj);
                }
            });
        }
    }

    // put unimplanted cybernetics in the player's inventory
    [HarmonyPatch(typeof(CyberneticsScreenRemove), "Activate")]
    public static class CyberneticsScreenRemove_Activate_Patch {
        public static void Postfix(CyberneticsScreenRemove __instance) {
            if (__instance.terminal.obj == The.Player || The.Player.Inventory == null) {
                return;
            }
            var cybernetic = AccessTools.Field(typeof(CyberneticsScreenRemove), "cybernetic").GetValue(__instance) as List<GameObject>;
            if (__instance.terminal.nSelected < cybernetic.Count) {
                var implant = cybernetic[__instance.terminal.nSelected];
                if (!implant.HasTag("CyberneticsNoRemove") && !implant.HasTag("CyberneticsDestroyOnRemoval")) {
                    __instance.terminal.obj.Inventory?.RemoveObject(implant);
                    _ = The.Player.Inventory.AddObject(implant, Silent: true);
                }
            }
        }
    }
}
