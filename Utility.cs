using System;
using System.Runtime.CompilerServices;

namespace XRL.World.CleverGirl
{
    public static class Utility {
        public static bool debug = false;

        public static Random Random = XRL.Rules.Stat.GetSeededRandomGenerator("Kizby_CleverGirl");

        public static void MaybeLog(string message, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) {
            if (debug) {
                MetricsManager.LogInfo(filePath + ":" + lineNumber + ": " + message);
            }
        }

        public class InventoryAction {
            public string Name;
            public string Display;
            public string Command;
            public char Key;
        }
    }
}