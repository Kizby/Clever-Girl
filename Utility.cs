using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace XRL.World.CleverGirl
{
    public static class Utility {
        public static bool debug = false;

        public static void MaybeLog(string message, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) {
            if (debug) {
                MetricsManager.LogInfo(filePath + ":" + lineNumber + ": " + message);
            }
        }

        private static Dictionary<string, Random> RandomDict = new Dictionary<string, Random>();
        public static Random Random(IPart part) {
            string key = "Kizby_CleverGirl_" + part.GetType().Name;
            if (null != part.ParentObject) {
                key += "_" + part.ParentObject.id;
            }
            if (!RandomDict.ContainsKey(key)) {
                MaybeLog("Creating Random " + key);
                RandomDict[key] = XRL.Rules.Stat.GetSeededRandomGenerator(key);
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