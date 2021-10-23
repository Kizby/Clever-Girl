namespace XRL.World.Parts {
    using System;
    using HarmonyLib;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using XRL.UI;
    using XRL.World.CleverGirl;
    using XRL.World.Parts.Mutation;

    [Serializable]
    [HarmonyPatch]
    public class CleverGirl_AIManageMutations : IPart, IXmlSerializable {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction {
            Name = "Clever Girl - Manage Mutations",
            Display = "manage mu{{inventoryhotkey|t}}ations",
            Command = "CleverGirl_ManageMutations",
            Key = 't',
        };

        public List<string> FocusingMutations = new List<string>();

        public bool WantNewMutations;
        public int NewMutationSavings;

        public static HashSet<string> CombatMutations = new HashSet<string>{
            "Corrosive Gas Generation",
            "Electromagnetic Pulse",
            "Flaming Ray",
            "Freezing Ray",
            "Horns",
            "Quills",
            "Sleep Gas Generation",
            "Slime Glands",
            "Spinnerets",
            "Stinger (Confusing Venom)",
            "Stinger (Paralyzing Venom)",
            "Stinger (Poisoning Venom)",
            "Burgeoning",
            "Confusion",
            "Cryokinesis",
            "Disintegration",
            "Force Wall",
            "Pyrokinesis",
            "Space-Time Vortex",
            "Stunning Force",
            "Sunder Mind",
            "Syphon Vim",
            "Teleport Other",
            "Time Dilation",
            "Temporal Fugue",
        };

        public override bool WantEvent(int ID, int cascade) => ID == StatChangeEvent.ID;

        public override bool HandleEvent(StatChangeEvent E) {
            if (E.Name == "MP") {
                SpendMP();
            }
            return true;
        }

        public void SpendMP() {
            var stat = ParentObject.Statistics["MP"];
            var budget = stat.Value - NewMutationSavings;
            if (budget <= 0) {
                // nothing to do
                return;
            }

            var pool = new List<BaseMutation>();
            var toDrop = new List<string>();
            foreach (var mutationName in FocusingMutations) {
                var mutation = ParentObject.GetPart<Mutations>().GetMutation(mutationName);
                if (mutation.CanIncreaseLevel()) {
                    pool.Add(mutation);
                } else if (!mutation.IsPhysical() && mutation.BaseLevel == mutation.GetMaxLevel()) {
                    toDrop.Add(mutationName);
                }
            }

            if (WantNewMutations) {
                // use null as a placeholder to save the MP
                pool.Add(null);
            }

            // drop mutations that are fully leveled
            FocusingMutations = FocusingMutations.Except(toDrop).ToList();

            if (pool.Count == 0) {
                // nothing to learn
                return;
            }

            var Random = Utility.Random(this);
            var which = pool.GetRandomElement(Random);
            if (which == null) {
                ++NewMutationSavings;
                if (NewMutationSavings < 4) {
                    SpendMP(); // spend any additional MP if relevant
                } else {
                    Utility.MaybeLog("Learning a new mutation");
                    // learn a new mutation
                    var mutations = ParentObject.GetPart<Mutations>();
                    var possibleMutations = mutations.GetMutatePool()
                                                     .Shuffle(Random);
                    if (!ParentObject.IsCombatObject()) {
                        // don't offer combat mutations to NoCombat followers
                        possibleMutations = possibleMutations.Where(m => !CombatMutations.Contains(m.DisplayName))
                                                             .ToList();
                    }
                    var valuableMutations = possibleMutations.Where(m => m.Cost > 1);
                    var cheapMutations = possibleMutations.Where(m => m.Cost <= 1);
                    const int choiceCount = 3;
                    var choices = new List<BaseMutation>(choiceCount);
                    var strings = new List<string>(choiceCount);
                    var newPartIndex = ParentObject.IsChimera() ? Random.Next(choiceCount) : -1;
                    // only offer valuable mutations if possible, but backfill with cheap ones
                    foreach (var mutationType in valuableMutations.Concat(cheapMutations)) {
                        var mutation = mutationType.CreateInstance();
                        var newPartString = choices.Count != newPartIndex ? "" : "{{G|+ grow a new body part}}";
                        choices.Add(mutation);
                        strings.Add("{{W|" + mutation.DisplayName + "}} " + newPartString +
                                    " {{y|- " + mutation.GetDescription() + "}}\n" + mutation.GetLevelText(1));
                        if (choices.Count == choiceCount) {
                            break;
                        }
                    }
                    if (choices.Count == 0) {
                        WantNewMutations = false;
                        NewMutationSavings = 0;
                        // spend our points if we can
                        SpendMP();
                        return;
                    }

                    var choice = -1;
                    while (-1 == choice) {
                        choice = Popup.ShowOptionList(Options: strings.ToArray(),
                                                      Spacing: 1,
                                                      Intro: "Choose a mutation for " + ParentObject.the + ParentObject.ShortDisplayName + ".",
                                                      MaxWidth: 78,
                                                      RespectOptionNewlines: true);
                    }

                    var result = choices[choice];
                    if (result.GetVariants() != null) {
                        // let followers choose their variant 😄
                        result.SetVariant(Random.Next(result.GetVariants().Count));
                    }
                    var mutationIndex = mutations.AddMutation(result, 1);
                    DidX("gain", mutations.MutationList[mutationIndex].DisplayName, "!", UsePopup: true, ColorAsGoodFor: ParentObject);
                    if (choice == newPartIndex) {
                        _ = mutations.AddChimericBodyPart();
                    }

                    if (ParentObject.UseMP(4)) {
                        NewMutationSavings -= 4;
                    }
                }
            } else {
                ParentObject.GetPart<Mutations>().LevelMutation(which, which.BaseLevel + 1);
                _ = ParentObject.UseMP(1);
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(BaseMutation), "RapidLevel")]
        public static void RapidLevelInstead(int amount, ref BaseMutation __instance) {
            // check if we're managing this creature
            var manageMutations = __instance.ParentObject.GetPart<CleverGirl_AIManageMutations>();

            if (manageMutations == null) {
                // do nothing otherwise
                return;
            }

            var whichKey = "RapidLevel_" + __instance.GetMutationClass();

            // pre-emptively reduce by the levels this mutation will gain
            _ = __instance.ParentObject.ModIntProperty(whichKey, -amount);

            // pick an appropriate mutation instead
            var mutations = __instance.ParentObject.GetPart<Mutations>();
            var allPhysicalMutations = mutations.MutationList.Where(m => m.IsPhysical() && m.CanLevel())
                                                             .ToList()
                                                             .Shuffle(Utility.Random(manageMutations));
            var instead = allPhysicalMutations.Find(m => manageMutations.FocusingMutations.Contains(m.Name)) ??
                          allPhysicalMutations[0];
            var insteadKey = "RapidLevel_" + instead.GetMutationClass();
            manageMutations.DidX("rapidly advance",
                                 instead.DisplayName + " by " + Language.Grammar.Cardinal(amount) + " ranks to rank " + (instead.Level + amount),
                                 "!", ColorAsGoodFor: __instance.ParentObject);
            _ = __instance.ParentObject.ModIntProperty(insteadKey, amount);

            Utility.MaybeLog("Moved a RapidLevel from " + whichKey + " to " + instead);
        }

        public bool Manage() {
            var changed = false;
            var mutations = new List<string>();
            var strings = new List<string>();
            var keys = new List<char>();
            if (ParentObject.GetPart<Mutations>() is Mutations haveMutations) {
                foreach (var Mutation in haveMutations.MutationList) {
                    mutations.Add(Mutation.Name);
                    // physical mutations can RapidLevel, so can always be selected
                    var canFocus = Mutation.CanLevel() && (Mutation.BaseLevel < Mutation.GetMaxLevel() || Mutation.IsPhysical());
                    var prefix = !canFocus ? "*" :
                                             FocusingMutations.Contains(Mutation.Name) ? "+" : "-";
                    var levelAdjust = Mutation.Level - Mutation.BaseLevel;
                    var levelAdjustString = levelAdjust == 0 ? "" :
                                                               levelAdjust < 0 ? "{{R|-" + (-levelAdjust) + "}}" :
                                                                                 "{{G|+" + levelAdjust + "}}";
                    strings.Add(prefix + " " + Mutation.DisplayName + " (" + Mutation.BaseLevel + levelAdjustString + ")");
                    keys.Add(keys.Count >= 26 ? ' ' : (char)('a' + keys.Count));
                }
            }
            {
                var prefix = ParentObject.GetPart<Mutations>().GetMutatePool().Count == 0 ? "*" : WantNewMutations ? "+" : "-";
                strings.Add(prefix + " Acquire new mutations");
                keys.Add(keys.Count >= 26 ? ' ' : (char)('a' + keys.Count));
            }

            while (true) {
                var index = Popup.ShowOptionList(Options: strings.ToArray(),
                                                Hotkeys: keys.ToArray(),
                                                Intro: "What mutations should " + ParentObject.the + ParentObject.ShortDisplayName + " advance?",
                                                AllowEscape: true);
                if (index < 0) {
                    if (FocusingMutations.Count == 0 && !WantNewMutations) {
                        // don't bother listening if there's nothing to hear
                        ParentObject.RemovePart<CleverGirl_AIManageMutations>();
                    } else {
                        // spend any MP we have if relevant
                        SpendMP();
                    }
                    return changed;
                }
                if (keys.Count - 1 == index) {
                    if (strings[index][0] != '*') {
                        changed = true;
                        WantNewMutations = !WantNewMutations;
                        strings[index] = (WantNewMutations ? '+' : '-') + strings[index].Substring(1);
                    }
                } else if (strings[index][0] == '*') {
                    // ignore
                } else if (strings[index][0] == '-') {
                    // start leveling this mutation
                    FocusingMutations.Add(mutations[index]);
                    strings[index] = '+' + strings[index].Substring(1);
                    changed = true;
                } else if (strings[index][0] == '+') {
                    // stop leveling this mutation
                    _ = FocusingMutations.Remove(mutations[index]);
                    strings[index] = '-' + strings[index].Substring(1);
                    changed = true;
                }
            }
        }

        /// <summary>
        /// XMLSerialization for compatibility with Armithaig's Recur mod
        /// </summary>
        public XmlSchema GetSchema() => null;

        public void WriteXml(XmlWriter writer) {
            writer.WriteStartElement("FocusingMutations");
            foreach (var mutation in FocusingMutations) {
                writer.WriteElementString("name", mutation);
            }
            writer.WriteEndElement();
            writer.WriteElementString("WantNewMutations", WantNewMutations ? "yes" : "no");
            if (WantNewMutations) {
                writer.WriteElementString("NewMutationSavings", NewMutationSavings.ToString());
            }
        }

        public void ReadXml(XmlReader reader) {
            reader.ReadStartElement();

            reader.ReadStartElement("FocusingMutations");
            while (reader.MoveToContent() != XmlNodeType.EndElement) {
                FocusingMutations.Add(reader.ReadElementContentAsString("name", ""));
            }
            reader.ReadEndElement();

            _ = reader.MoveToContent();
            WantNewMutations = reader.ReadElementContentAsString("WantNewMutations", "") == "yes";

            if (WantNewMutations) {
                _ = reader.MoveToContent();
                NewMutationSavings = int.Parse(reader.ReadElementContentAsString("NewMutationSavings", ""));
            }

            reader.ReadEndElement();
        }
    }
}
