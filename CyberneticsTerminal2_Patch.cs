namespace XRL.World.Parts.CleverGirl {
    using HarmonyLib;
    using System;
    using System.Linq;
    using XRL.World.CleverGirl;

    // disable smart use if we might be interfacing companions
    [HarmonyPatch(typeof(CyberneticsTerminal2), "HandleEvent", new Type[] { typeof(CanSmartUseEvent) })]
    public static class CyberneticsTerminal2_HandleEvent_CanSmartUseEvent {
        static void Postfix(CanSmartUseEvent E, ref bool __result) {
            __result = __result || Utility.CollectNearbyCompanions(E.Actor).Any(c => c.IsTrueKin());
        }
    }

    // add "interface a companion" option to terminal
    [HarmonyPatch(typeof(CyberneticsTerminal2), "HandleEvent", new Type[] { typeof(GetInventoryActionsEvent) })]
    public static class CyberneticsTerminal2_HandleEvent_GetInventoryActionsEvent {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction {
            Name = "Clever Girl - Interface Companion",
            Display = "interface a companion",
            Command = "CleverGirl_Interface",
            Key = 'c',
            Valid = E => Utility.CollectNearbyCompanions(E.Actor).Any(c => c.IsTrueKin()),
        };
        static void Postfix(GetInventoryActionsEvent E) {
            if (ACTION.Valid(E)) {
                _ = E.AddAction(ACTION.Name, ACTION.Display, ACTION.Command, Key: ACTION.Key, FireOnActor: true);
            }
        }
    }
}
