namespace XRL.World.CleverGirl {
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;

    public static class Utility {
        public static bool debug;

        public static void MaybeLog(string message, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) {
            if (debug) {
                MetricsManager.LogInfo(filePath + ":" + lineNumber + ": " + message);
            }
        }

        private static readonly Dictionary<string, Random> RandomDict = new Dictionary<string, Random>();
        public static Random Random(IPart part) {
            var key = "Kizby_CleverGirl_" + part.GetType().Name;
            if (part.ParentObject != null) {
                key += "_" + part.ParentObject.id;
            }
            if (!RandomDict.ContainsKey(key)) {
                MaybeLog("Creating Random " + key);
                RandomDict[key] = Rules.Stat.GetSeededRandomGenerator(key);
            }
            return RandomDict[key];
        }

        public class InventoryAction {
            public string Name;
            public string Display;
            public string Command;
            public char Key;
        }
    }
}
