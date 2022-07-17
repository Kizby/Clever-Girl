namespace XRL.World.Parts.CleverGirl {
    using HarmonyLib;
    using XRL.World.CleverGirl;

    [HarmonyPatch(typeof(GameObject), "AddPartInternals")]
    public static class GameObject_AddPartInternals_Patch {
        public static void Postfix(IPart P, GameObject __instance) {
            if (P.Name == "Brain") {
                var leader = (P as Brain)?._PartyLeader;
                if (leader != null) {
                    CompanionsTracker.AddBond(leader, __instance);
                }
            }
        }
    }

    [HarmonyPatch(typeof(Brain), "set_PartyLeader")]
    public static class Brain_set_PartyLeader_Patch {
        public static void Prefix(Brain __instance, GameObject value) {
            if (__instance._PartyLeader == value) {
                return;
            }
            if (__instance._PartyLeader != null) {
                CompanionsTracker.RemoveBond(__instance._PartyLeader, __instance.ParentObject);
            }
            if (value != null) {
                CompanionsTracker.AddBond(value, __instance.ParentObject);
            }
        }
    }

    [HarmonyPatch(typeof(Brain), "TakeOnAttitudesOf", new System.Type[] { typeof(Brain), typeof(bool), typeof(bool) })]
    public static class Brain_TakeOnAttitudesOf_Patch {
        public static void Postfix(Brain __instance, Brain o, bool CopyLeader) {
            if (!CopyLeader || o == null || __instance._PartyLeader == o._PartyLeader) {
                return;
            }
            if (__instance._PartyLeader != null) {
                CompanionsTracker.RemoveBond(__instance._PartyLeader, __instance.ParentObject);
            }
            if (o._PartyLeader != null) {
                CompanionsTracker.AddBond(o._PartyLeader, __instance.ParentObject);
            }
        }
    }
}
