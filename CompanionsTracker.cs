namespace XRL.World.CleverGirl {
    using System.Linq;
    using System.Collections.Generic;
    using XRL.World;
    using XRL.UI;
    using XRL.Rules;
    using HarmonyLib;
    using System.Reflection;
    using ConsoleLib.Console;

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

            var indents = new List<int>();
            var names = new List<string>();
            var status = new List<string>();
            var effects = new List<string>();
            void HarvestFields(IEnumerable<GameObject> Companions, int Indent = 0) {
                foreach (var companion in Companions) {
                    indents.Add(Indent);
                    names.Add(CompanionName(companion));
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
                        HarvestFields(subCompanions, Indent + 1);
                    }
                }
            }
            HarvestFields(companionMap[The.Player]);

            var lines = new string[names.Count];
            for (var i = 0; i < lines.Length; ++i) {
                var line = names[i];
                for (var j = 0; j < indents[i]; ++j) {
                    line = " | " + line;
                }
                if (status[i].Length > 0) {
                    line += " | " + status[i];
                }
                if (effects[i].Length > 0) {
                    line += " | " + effects[i];
                }
                lines[i] = line;
                Utility.MaybeLog(line);
            }
            var selected = 0;
            while (selected != -1) {
                selected = Popup.ShowOptionList("Companions", lines, AllowEscape: true);
            }
        }

        private static readonly PropertyInfo DisplayNameBaseProperty = AccessTools.Property(typeof(GameObject), "DisplayNameBase");
        private static string CompanionName(GameObject Companion) => DisplayNameBaseProperty.GetValue(Companion) as string;
    }
}
