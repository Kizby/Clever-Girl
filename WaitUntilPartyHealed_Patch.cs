namespace XRL.World.CleverGirl {
    using HarmonyLib;
    using System.Collections.Generic;
    using System.Reflection.Emit;
    using XRL.Core;
    using XRL.World.Parts;

    [HarmonyPatch(typeof(XRLCore), "PlayerTurn")]
    public static class XRLCore_PlayerTurn_Patch {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator) {
            bool FixNextLdc = false;
            bool FixArrayElements = false;
            bool FixCommandString = false;

            bool GrabStr1Var = false;
            object Str1Var = null;

            bool GrabNum2Var = false;
            object Num2Var = null;

            bool InterruptNextBranch = false;
            var BranchTarget = generator.DefineLabel();
            int PeekDistance = 0;
            foreach (var instruction in instructions) {
                if (instruction.Is(OpCodes.Ldstr, "CmdWaitMenu")) {
                    FixNextLdc = true;
                } else if (FixNextLdc) {
                    ++PeekDistance;
                    if (PeekDistance > 10) {
                        MetricsManager.LogCallingModError("WaitUntilPartyHealedEvent transpiler broke, please file a bug with Clever Girl <3");
                        FixNextLdc = false;
                    }
                    if (FixNextLdc && instruction.LoadsConstant(6)) {
                        FixNextLdc = false;
                        FixArrayElements = true;
                        yield return new CodeInstruction(OpCodes.Ldc_I4_7);
                        continue;
                    }
                } else if (FixArrayElements && instruction.Is(OpCodes.Ldstr, "Wait Until Morning")) {
                    // slip our wait option in just before Wait Until Morning
                    yield return new CodeInstruction(OpCodes.Ldstr, "Wait Until Party Healed");
                    yield return new CodeInstruction(OpCodes.Stelem_Ref);
                    yield return new CodeInstruction(OpCodes.Dup);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_6);
                    FixArrayElements = false;
                    FixCommandString = true;
                } else if (FixCommandString && instruction.Is(OpCodes.Ldstr, "CmdWaitUntilHealed")) {
                    // grab a reference to the local from the next line
                    FixCommandString = false;
                    GrabStr1Var = true;
                } else if (GrabStr1Var) {
                    GrabStr1Var = false;
                    if (!instruction.IsStloc()) {
                        MetricsManager.LogCallingModError("WaitUntilPartyHealedEvent transpiler broke, please file a bug with Clever Girl <3");
                    } else {
                        Str1Var = instruction.operand;
                        GrabNum2Var = true;
                    }
                } else if (GrabNum2Var) {
                    GrabNum2Var = false;
                    if (!instruction.IsLdloc()) {
                        MetricsManager.LogCallingModError("WaitUntilPartyHealedEvent transpiler broke, please file a bug with Clever Girl <3");
                    } else {
                        Num2Var = instruction.operand;
                        InterruptNextBranch = true;
                    }
                } else if (InterruptNextBranch && instruction.Branches(out _)) {
                    InterruptNextBranch = false;
                    yield return new CodeInstruction(OpCodes.Bne_Un_S, BranchTarget);
                    yield return new CodeInstruction(OpCodes.Ldstr, "CmdWaitUntilPartyHealed");
                    yield return new CodeInstruction(OpCodes.Stloc_S, Str1Var);
                    yield return new CodeInstruction(OpCodes.Ldloc_S, Num2Var).WithLabels(BranchTarget);
                    yield return new CodeInstruction(OpCodes.Ldc_I4_6);
                }
                yield return instruction;
            }
        }
    }

    [HarmonyPatch(typeof(ActionManager), "RunSegment")]
    public static class ActionManager_RunSegment_Patch {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) {
            bool LookingForPenalty = false;
            foreach (var instruction in instructions) {
                if (instruction.Is(OpCodes.Stfld, AccessTools.Field(typeof(ActionManager), "RestingUntilHealed"))) {
                    // when we set RestingUntilHealed back to false, set RestingUntilPartyHealed too
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ActionManager_RunSegment_Patch), "ResetPartyHealing"));
                }
                if (instruction.Is(OpCodes.Ldstr, "Resting until healed... Turn: ")) {
                    // use custom logic instead of a hard-coded constant
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ActionManager_RunSegment_Patch), "GetHealingMessage"));
                    LookingForPenalty = true;
                    continue;
                } else if (LookingForPenalty && instruction.Is(OpCodes.Callvirt, AccessTools.Method(typeof(Statistic), "get_Penalty"))) {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ActionManager_RunSegment_Patch), "AddFollowerPenalties"));
                    LookingForPenalty = false;
                    continue;
                }
                yield return instruction;
            }
        }

        public static string GetHealingMessage() {
            return The.Player.GetPart<CleverGirl_EventListener>()?.RestingUntilPartyHealed == true
                ? "Resting until party healed... Turn: "
                : "Resting until healed... Turn: ";
        }
        public static int AddFollowerPenalties(int PlayerPenalty) {
            int TotalPenalty = PlayerPenalty;
            if (The.Player.GetPart<CleverGirl_EventListener>()?.RestingUntilPartyHealed == true) {
                Cell currentCell = The.PlayerCell;
                foreach (GameObject gameObject in currentCell.ParentZone.FastFloodVisibility(currentCell.X, currentCell.Y, 30, "Brain", The.Player)) {
                    if (gameObject.IsPlayerLed()) {
                        TotalPenalty += gameObject.GetStat("Hitpoints").Penalty;
                    }
                }
            }
            return TotalPenalty;
        }

        public static void ResetPartyHealing() {
            CleverGirl_EventListener part = The.Player.GetPart<CleverGirl_EventListener>();
            if (part != null) {
                part.RestingUntilPartyHealed = false;
            }
        }
    }
}
