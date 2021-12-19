namespace XRL.World.Parts {
    using HarmonyLib;
    using System;
    using System.Collections.Generic;

    public class CleverGirl_INoSavePart : IPart { }

    namespace CleverGirl {
        [HarmonyPatch(typeof(GameObject), "Save", new Type[] { typeof(SerializationWriter) })]
        public static class GameObject_Save_Patch {
            private static List<CleverGirl_INoSavePart> cachedParts;
            public static void Prefix(GameObject __instance) {
                cachedParts = __instance.GetPartsDescendedFrom<CleverGirl_INoSavePart>();
                if (cachedParts.Count > 0) {
                    _ = __instance.PartsList.RemoveAll(p => p is CleverGirl_INoSavePart);
                }
            }
            public static void Postfix(GameObject __instance) {
                if (cachedParts != null) {
                    __instance.PartsList.AddRange(cachedParts);
                    cachedParts = null;
                }
            }
        }
    }
}
