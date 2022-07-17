namespace XRL.World.Parts.CleverGirl {
    using HarmonyLib;
    using XRL.World.CleverGirl;

    [HarmonyPatch(typeof(GameObject), "HandleInventoryActionEvent")]
    public static class GameObject_HandleInventoryActionEvent_Patch {
        public static void Postfix(InventoryActionEvent E) {
            if (E.Command == "CompanionRename" && E.Item.PartyLeader?.HasStringProperty(CompanionsTracker.NAME_PROPERTY + E.Item.id) == true) {
                E.Item.PartyLeader.SetStringProperty(CompanionsTracker.NAME_PROPERTY + E.Item.id, E.Item.DisplayName);
            }
        }
    }
}
