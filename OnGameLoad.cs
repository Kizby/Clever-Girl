namespace CleverGirl {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using HarmonyLib;

    using XRL;
    using XRL.Core;
    using XRL.World;
    using XRL.World.Parts;

    [PlayerMutator]
    [HasCallAfterGameLoaded]
    public class CleverGirlPlayerMutator : IPlayerMutator {
        public void mutate(GameObject player) {
            // add our listener to the player when a New Game begins
            _ = player.AddPart<CleverGirl_EventListener>();
        }

        [CallAfterGameLoaded]
        public static void GameLoadedCallback() {
            // Called whenever loading a save game
            var player = XRLCore.Core?.Game?.Player?.Body;
            _ = (player?.RequirePart<CleverGirl_EventListener>());
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
