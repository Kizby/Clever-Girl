namespace XRL.World.Parts {
    using HarmonyLib;
    using System;
    using System.Linq;
    using System.Collections.Generic;

    public class CleverGirl_INoSavePart : IPart { }

    namespace CleverGirl {
        // hide any INoSaveParts so GameObject doesn't try to save them; restore after save
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

        // attach all the parts we didn't save based on whether they have their corresponding property
        [HarmonyPatch(typeof(GameObject), "Load", new Type[] { typeof(SerializationReader) })]
        public static class GameObject_Load_Patch {
            private static List<Type> classes;
            public static void Postfix(GameObject __instance) {
                if (classes == null) {
                    classes = AppDomain.CurrentDomain.GetAssemblies()
                                .SelectMany(x => x.GetTypes())
                                .Where(x => typeof(CleverGirl_INoSavePart).IsAssignableFrom(x) && !x.IsInterface && !x.IsAbstract)
                                .ToList();
                }
                foreach (var clazz in classes) {
                    string prop = clazz.GetProperty("PROPERTY")?.GetValue(null) as string ?? "";
                    if (prop != "" && __instance.HasProperty(prop)) {
                        _ = __instance.AddPart(Activator.CreateInstance(clazz) as IPart);
                    }
                }
            }
        }
    }
}
