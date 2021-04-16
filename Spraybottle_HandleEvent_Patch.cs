namespace XRL.World.Parts.CleverGirl {
    using HarmonyLib;
    using System;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using XRL.UI;
    using XRL.World.Effects;

    [HarmonyPatch(typeof(Spraybottle), "HandleEvent", new Type[] { typeof(InventoryActionEvent) })]
    public static class Spraybottle_HandleEvent_Patch {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            bool Skipping = false;
            foreach (var inst in instructions) {
                if (inst.Is(OpCodes.Callvirt, AccessTools.Method(typeof(GameObject), "get_Body"))) {
                    // instead of an option of player equipped and inventory items, use PickObject
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Spraybottle_HandleEvent_Patch), "PickObject"));
                    Skipping = true;
                }
                if (inst.Is(OpCodes.Newobj, AccessTools.DeclaredConstructor(typeof(LiquidCovered), new Type[] { typeof(LiquidVolume), typeof(int), typeof(int), typeof(bool), typeof(GameObject), typeof(bool) }))) {
                    // fix up the last couple arguments to the constructor so Actor is blamed for any damage
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Pop);
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(IActOnItemEvent), "Actor"));
                    yield return new CodeInstruction(OpCodes.Ldc_I4_0);
                }
                if (!Skipping) {
                    yield return inst;
                }
                if (inst.Is(OpCodes.Call, AccessTools.Method(typeof(PickItem), "ShowPicker", new Type[] { typeof(List<GameObject>), typeof(string), typeof(PickItem.PickItemDialogStyle), typeof(GameObject), typeof(GameObject), typeof(Cell), typeof(string), typeof(bool), typeof(Func<List<GameObject>>), typeof(bool), typeof(bool) }))) {
                    // use PickObject's return value instead of ShowPicker's
                    Skipping = false;
                }
            }
        }
        public static GameObject PickObject(GameObject Actor, Spraybottle Bottle) {
            GameObject target = Actor;
            if (Actor.IsPlayer()) {
                Cell cell = Actor.PickDirection();
                target = cell.GetCombatTarget(Actor);
            }
            if (target?.IsPlayerControlled() != true) {
                return target;
            }
            var objects = target.Body.GetEquippedObjects();
            if (target == Actor) {
                objects.AddRange(target.Inventory.GetObjects());
                _ = objects.Remove(Bottle.ParentObject);
            } else {
                objects.Add(target);
            }
            return PickItem.ShowPicker(objects, "Fungal Infection", Actor: target, ShowContext: true);
        }
    }
}
