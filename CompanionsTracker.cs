namespace XRL.World.CleverGirl {
    using System.Linq;
    using System.Collections.Generic;
    using XRL.World;

    [HasGameBasedStaticCache]
    public static class CompanionsTracker {
        public static string PROPERTY = "CleverGirl_CompanionsList";
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
            foreach (var id in Leader.GetStringProperty(PROPERTY, "").Split(',')) {
                _ = knownCompanions.Add(id);
            }
            var anyNew = false;
            foreach (var companionId in loadedCompanions) {
                if (knownCompanions.Add(companionId)) {
                    anyNew = true;
                    var companion = GameObject.findById(companionId);
                    if (companion != null) {
                        InitializeCompanionsList(companion);
                    }
                }
            }
            if (anyNew) {
                Leader.SetStringProperty(PROPERTY, string.Join(",", knownCompanions));
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
            if (Leader.HasStringProperty(PROPERTY)) {
                // remove companion from the list
                Leader.SetStringProperty(PROPERTY, string.Join(",", Leader.GetStringProperty(PROPERTY).Split(',').Where(s => s != Companion.id)));
                if (Leader.GetStringProperty(PROPERTY).Length == 0) {
                    Leader.RemoveStringProperty(PROPERTY);
                }
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
            if (Leader.HasStringProperty(PROPERTY) || Leader.IsPlayerControlled()) {
                var currentList = Leader.GetStringProperty(PROPERTY);
                if (currentList?.Contains(Companion.id) != true) {
                    Leader.SetStringProperty(PROPERTY, currentList.IsNullOrEmpty() ? Companion.id : currentList + "," + Companion.id);
                }
            }
        }
    }
}
