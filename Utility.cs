namespace XRL.World.CleverGirl {
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text.RegularExpressions;
    using XRL.Rules;

    public static class Utility {
        public static bool debug;

        public static void MaybeLog(string message, [CallerFilePath] string filePath = "", [CallerLineNumber] int lineNumber = 0) {
            if (debug) {
                MetricsManager.LogInfo(filePath + ":" + lineNumber + ": " + message);
            }
        }

        private static readonly Dictionary<string, Random> RandomDict = new Dictionary<string, Random>();
        public static Random Random(IPart part) {
            var key = GetKey(part);
            if (!RandomDict.ContainsKey(key)) {
                MaybeLog("Creating Random " + key);
                RandomDict[key] = Stat.GetSeededRandomGenerator(key);
            }
            return RandomDict[key];
        }

        public static int Roll(string Dice, IPart part) {
            return Stat.Roll(Dice, GetKey(part));
        }

        private static string GetKey(IPart part) {
            var key = "Kizby_CleverGirl_" + part.GetType().Name;
            if (part.ParentObject != null) {
                key += "_" + part.ParentObject.id;
            }
            return key;
        }

        private static readonly Dictionary<string, Regex> RegexCache = new Dictionary<string, Regex>();
        public static string RegexReplace(string String, string Regex, string Replacement) {
            if (!RegexCache.ContainsKey(Regex)) {
                RegexCache.Add(Regex, new Regex(Regex));
            }
            return RegexCache[Regex].Replace(String, Replacement);
        }

        /// <summary>
        /// a lot of messages in the game hardcode "you" pronouns and need reconjugating
        /// I have /zero/ interest in generalizing distinguishing verbs, so hardcode the ones we've seen and treat as an object otherwise
        /// </summary>
        public static string AdjustSubject(string Message, GameObject Subject) {
            if (Subject.IsPlural || Subject.IsPseudoPlural) {
                Message = RegexReplace(Message, "\\bUsted ama\\b", "Ustedes aman");
            }
            Message = RegexReplace(Message, "\\bYou feel\\b", Subject.It + Subject.GetVerb("feel"));
            Message = RegexReplace(Message, "\\bYou don't\\b", Subject.It + Subject.GetVerb("don't"));
            Message = RegexReplace(Message, "\\byou start\\b", Subject.it + Subject.GetVerb("start"));
            Message = RegexReplace(Message, "\\byour\\b", Subject.its);
            Message = RegexReplace(Message, "\\bYour\\b", Subject.Its);
            Message = RegexReplace(Message, "\\byou\\b", Subject.them);
            return Message;
        }

        public static List<GameObject> CollectNearbyCompanions(GameObject Leader) {
            var result = new List<GameObject>();

            // allow companions to be daisy-chained so long as they're adjacent to each other
            var toInspect = new List<Cell> { Leader.CurrentCell };
            for (int i = 0; i < toInspect.Count; ++i) {
                var cell = toInspect[i];
                cell.ForeachObject(obj => {
                    if (obj == Leader || obj.IsLedBy(Leader)) {
                        cell.ForeachLocalAdjacentCell(adj => {
                            if (!toInspect.Contains(adj)) {
                                toInspect.Add(adj);
                            }
                        });
                        if (obj != Leader) {
                            result.Add(obj);
                        }
                    }
                });
            }
            return result;
        }

        public class InventoryAction {
            public string Name;
            public string Display;
            public string Command;
            public char Key;
            public Predicate<IInventoryActionsEvent> Valid = _ => true;
            public static bool Adjacent(IInventoryActionsEvent e) {
                return e.Actor.CurrentCell.IsAdjacentTo(e.Object.CurrentCell);
            }
        }
    }
}
