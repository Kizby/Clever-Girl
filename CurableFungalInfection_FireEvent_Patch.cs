/// <summary>
/// logic copied and modified from the original CurableFungalInfection.FireEvent
/// </summary>
namespace XRL.World.Parts.CleverGirl {
    using HarmonyLib;
    using XRL.Language;
    using XRL.UI;

    [HarmonyPatch(typeof(CurableFungalInfection), "FireEvent")]
    public static class CurableFungalInfection_FireEvent_Patch {
        public static bool Prefix(CurableFungalInfection __instance, Event E, ref bool __result) {
            // there's a FireEvent called from destroy where not everything is still wired up like we expect
            GameObject Owner = __instance.ParentObject?.Equipped;
            if (Owner?.IsPlayer() == true) {
                // use the default logic for the player
                return true;
            }
            if (E.ID == "AppliedLiquidCovered") {
                if (IComponent<GameObject>.TheGame.GetStringGameState("FungalCureLiquid").Length == 0) {
                    IComponent<GameObject>.TheCore.GenerateFungalCure();
                }
                var FungalCureLiquid = IComponent<GameObject>.TheGame.GetStringGameState("FungalCureLiquid");
                var AppliedLiquid = E.GetParameter<LiquidVolume>("Liquid");
                if (AppliedLiquid.ComponentLiquids.ContainsKey("gel") && AppliedLiquid.ComponentLiquids.ContainsKey(FungalCureLiquid)) {
                    if (__instance.CorpseTimer <= 0) {
                        Popup.Show(Owner.The + Owner.ShortDisplayName + Owner.GetVerb("squirm") + " pitifully.");
                    } else if (__instance.ParentObject.Blueprint.Equals("PaxInfection")) {
                        Popup.Show(__instance.ParentObject.DisplayNameOnlyDirect + " is immune to conventional treatments.");
                    } else {
                        var BodyPart = __instance.ParentObject.EquippedOn().GetOrdinalName();
                        if (__instance.ParentObject.Destroy(Silent: true)) {
                            Popup.Show("The infected crust of skin on " + Grammar.MakePossessive(Owner.the + Owner.ShortDisplayName) + " " + BodyPart + " loosens and breaks away.");
                        }
                    }
                }
            } else if (E.ID == "Eating") {
                var FungalCureWorm = IComponent<GameObject>.TheGame.GetStringGameState("FungalCureWorm");
                if (FungalCureWorm.Length == 0) {
                    IComponent<GameObject>.TheCore.GenerateFungalCure();
                    FungalCureWorm = IComponent<GameObject>.TheGame.GetStringGameState("FungalCureWorm");
                }
                var Food = E.GetGameObjectParameter("Food");
                if (Food != null && Food.Blueprint == FungalCureWorm) {
                    if (__instance.CorpseTimer <= 0) {
                        Popup.Show(Owner.The + Owner.ShortDisplayName + Owner.GetVerb("look") + " green around the gills.");
                    }
                    __instance.CorpseTimer = 100;
                }
            } else if (E.ID == "EndTurn") {
                if (__instance.CorpseTimer > 0) {
                    --__instance.CorpseTimer;
                    if (__instance.CorpseTimer <= 0 && Owner.IsVisible()) {
                        Popup.Show(Owner.The + Owner.ShortDisplayName + Owner.GetVerb("regain") + " " + Owner.its + " color.");
                    }
                }
            } else {
                // default logic is fine for everything else
                return true;
            }

            // too many little finicky things to be worth transpiling, so just replace the method wholesale
            __result = true;
            return false;
        }
    }
}
