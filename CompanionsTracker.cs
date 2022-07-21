namespace XRL.World.CleverGirl {
    using System.Linq;
    using System.Collections.Generic;
    using XRL.World;
    using XRL.UI;
    using XRL.Rules;
    using HarmonyLib;
    using System.Reflection;
    using ConsoleLib.Console;
    using Qud.UI;
    using System;

    public static class CompanionsTracker {
        public static void OpenMenu() {
            if (The.Player == null) {
                // too early?
                return;
            }
            var companionMap = new Dictionary<GameObject, SortedSet<GameObject>>();
            The.ActiveZone.ForeachObject(o => {
                if (o.IsPlayerLed()) {
                    var set = companionMap.GetValue(o.PartyLeader);
                    if (set == null) {
                        set = new SortedSet<GameObject>(Comparer<GameObject>.Create((a, b) => {
                            var c = ColorUtility.CompareExceptFormattingAndCase(CompanionName(a), CompanionName(b));
                            if (c != 0) {
                                return c;
                            }
                            return a.id.CompareTo(b.id);
                        }));
                        companionMap.Add(o.PartyLeader, set);
                    }
                    _ = set.Add(o);
                }
            });
            if (companionMap.GetValue(The.Player) == null) {
                // no companions
                return;
            }

            var names = new List<string>();
            var status = new List<string>();
            var effects = new List<string>();
            var companionList = new List<GameObject>();
            void HarvestFields(IEnumerable<GameObject> Companions, string IndentString = "") {
                foreach (var companion in Companions) {
                    companionList.Add(companion);
                    names.Add(IndentString + CompanionName(companion));
                    if (!companion.IsVisible()) {
                        status.Add((companion.IsAudible(The.Player) ? "{{W|" : "{{O|") + The.Player.DescribeDirectionToward(companion.CurrentCell) + "}}");
                        effects.Add("");
                    } else {
                        status.Add(Strings.WoundLevel(companion));
                        var effectString = "";
                        foreach (var effect in companion.Effects) {
                            var description = effect.GetDescription();
                            if (!description.IsNullOrEmpty()) {
                                if (effectString.Length > 0) {
                                    effectString += ", ";
                                }
                                effectString += description;
                            }
                        }
                        effects.Add(effectString);
                    }
                    if (companionMap.TryGetValue(companion, out SortedSet<GameObject> subCompanions)) {
                        HarvestFields(subCompanions, IndentString + "\0");
                    }
                }
            }
            HarvestFields(companionMap[The.Player]);

            var selected = Utility.ShowTabularPopup("Companions", new List<List<string>>() { names, status, effects }, new List<int> { 30, 20, 20 });
            if (selected != -1) {
                _ = companionList[selected].Twiddle();
            }
        }

        private static readonly PropertyInfo DisplayNameBaseProperty = AccessTools.Property(typeof(GameObject), "DisplayNameBase");
        private static string CompanionName(GameObject Companion) => ColorUtility.ClipToFirstExceptFormatting(DisplayNameBaseProperty.GetValue(Companion) as string, ',');
    }
}
