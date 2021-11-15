namespace XRL.World.Parts.CleverGirl {
    using HarmonyLib;
    using System;
    using System.Collections.Generic;
    using System.Reflection.Emit;

    [HarmonyPatch(typeof(Polygel), "HandleEvent", new Type[] { typeof(InventoryActionEvent) })]
    public static class Polygel_HandleEvent_Patch {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            foreach (var inst in instructions) {
                yield return inst;

                // carry over any temporariness
                if (inst.opcode == OpCodes.Stloc_3) {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(IPart), "get_ParentObject")); // the polygel
                    yield return new CodeInstruction(OpCodes.Ldloc_3); // the duplicated object
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Temporary), "CarryOver"));
                }
            }
        }
    }
}
