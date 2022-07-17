namespace XRL.World.CleverGirl {
    using System.Linq;
    using System.Collections.Generic;
    using XRL.World;
    using XRL.UI;
    using System.Collections.Immutable;
    using XRL.Rules;
    using HarmonyLib;
    using System.Reflection;

    [HasGameBasedStaticCache]
    public static class CompanionsTracker {
        public static string LIST_PROPERTY = "CleverGirl_CompanionsList";
        public static string NAME_PROPERTY = "CleverGirl_CompanionName_";
        private static readonly Dictionary<string, string> LeaderMap = new Dictionary<string, string>();
        private static readonly Dictionary<string, HashSet<string>> CompanionMap = new Dictionary<string, HashSet<string>>();
        public static void Reset() {
            LeaderMap.Clear();
            CompanionMap.Clear();
        }
        public static void InitializeCompanionsList(GameObject Leader) {
            if (!CompanionMap.ContainsKey(Leader.id)) {
                // no companions to initialize!
                return;
            }
            var loadedCompanions = CompanionMap[Leader.id];

            var knownCompanions = new SortedSet<string>();
            foreach (var id in Leader.GetStringProperty(LIST_PROPERTY, "").Split(',')) {
                _ = knownCompanions.Add(id);
            }
            var anyNew = false;
            foreach (var companionId in loadedCompanions) {
                if (knownCompanions.Add(companionId)) {
                    anyNew = true;
                    var companion = GameObject.findById(companionId);
                    if (companion != null) {
                        Leader.SetStringProperty(NAME_PROPERTY + companionId, CompanionName(companion));
                        InitializeCompanionsList(companion);
                    }
                }
            }
            if (anyNew) {
                Leader.SetStringProperty(LIST_PROPERTY, string.Join(",", knownCompanions));
            }
        }

        public static void RemoveBond(GameObject Leader, GameObject Companion) {
            _ = LeaderMap.Remove(Companion.id);
            if (!CompanionMap.ContainsKey(Leader.id)) {
                return;
            }
            _ = CompanionMap[Leader.id].Remove(Companion.id);
            if (CompanionMap[Leader.id].Count == 0) {
                _ = CompanionMap.Remove(Leader.id);
            }
            if (Leader.HasStringProperty(LIST_PROPERTY)) {
                // remove companion from the list
                Leader.SetStringProperty(LIST_PROPERTY, string.Join(",", Leader.GetStringProperty(LIST_PROPERTY).Split(',').Where(s => s != Companion.id)));
                if (Leader.GetStringProperty(LIST_PROPERTY).Length == 0) {
                    Leader.RemoveStringProperty(LIST_PROPERTY);
                }
                Leader.RemoveStringProperty(NAME_PROPERTY + Companion.id);
            }
        }
        public static void AddBond(GameObject Leader, GameObject Companion) {
            if (LeaderMap.ContainsKey(Companion.id)) {
                Utility.MaybeLog("Clobbering a leader, probably accidentally :(");
            }
            LeaderMap.Set(Companion.id, Leader.id);
            if (!CompanionMap.ContainsKey(Leader.id)) {
                CompanionMap.Set(Leader.id, new HashSet<string>());
            }
            _ = CompanionMap[Leader.id].Add(Companion.id);
            if (Leader.HasStringProperty(LIST_PROPERTY) || Leader.IsPlayerControlled()) {
                var currentList = Leader.GetStringProperty(LIST_PROPERTY);
                if (currentList?.Contains(Companion.id) != true) {
                    Leader.SetStringProperty(LIST_PROPERTY, currentList.IsNullOrEmpty() ? Companion.id : currentList + "," + Companion.id);
                    Leader.SetStringProperty(NAME_PROPERTY + Companion.id, CompanionName(Companion));
                }
            }
        }

        public static void OpenMenu() {
            if (The.Player == null) {
                // too early?
                return;
            }
            if (!The.Player.HasStringProperty(LIST_PROPERTY)) {
                Popup.Show("You have no companions.");
                return;
            }
            var companionIds = The.Player.GetStringProperty(LIST_PROPERTY).Split(',').ToImmutableHashSet();
            var localCompanions = The.ActiveZone.FindObjects(obj => companionIds.Contains(obj.id));
            localCompanions.Sort((a, b) => CompanionName(a).CompareTo(CompanionName(b)));

            var names = new List<string>();
            var status = new List<string>();
            var effects = new List<string>();
            foreach (var companion in localCompanions) {
                names.Add(CompanionName(companion));
                if (!companion.IsVisible()) {
                    status.Add("{{O|" + The.Player.DescribeDirectionToward(companion.CurrentCell) + "}}");
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
            }
            foreach (var companionId in companionIds) {
                if (localCompanions.Any(obj => obj.id == companionId)) {
                    continue;
                }
                if (The.Player.HasStringProperty(NAME_PROPERTY + companionId)) {
                    names.Add(The.Player.GetStringProperty(NAME_PROPERTY + companionId));
                    status.Add("");
                    effects.Add("");
                } else if (Utility.debug) {
                    names.Add(companionId);
                    status.Add("");
                    effects.Add("");
                }
            }
            var lines = new string[names.Count];
            for (var i = 0; i < lines.Length; ++i) {
                var line = names[i];
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
