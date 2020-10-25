using System;

namespace XRL.World.Parts.CleverGirl
{
    using System.Collections.Generic;
    using System.Linq;
    using XRL.Rules;
    using XRL.UI;
    using XRL.World.CleverGirl;
    using XRL.World.Parts.Mutation;

    [Serializable]
    public class AIManageMutations : IPart {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction{
            Name = "Clever Girl - Manage Mutations",
            Display = "manage mu{{inventoryhotkey|t}}ations",
            Command = "CleverGirl_ManageMutations",
            Key = 't',
        };

        public List<string> FocusingMutations = new List<string>();

        public bool WantNewMutations = false;
        public int NewMutationSavings = 0;
        public static Random MutationsRandom = Stat.GetSeededRandomGenerator("Kizby_CleverGirl_Mutations");

        public override bool WantEvent(int ID, int cascade)
        {
            return ID == StatChangeEvent.ID;
        }

        public override bool HandleEvent(StatChangeEvent E) {
            if ("MP" != E.Name) {
                return true;
            }
            var budget = E.NewValue - NewMutationSavings;
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

            if (0 == pool.Count) {
                // nothing to learn
                return true;
            }

            var which = pool.GetRandomElement(Utility.Random);
            if (null == which) {
                ++NewMutationSavings;
                if (NewMutationSavings >= 4) {
                    // learn a new mutation
                    var mutations = ParentObject.GetPart<Mutations>();
                    var possibleMutations = mutations.GetMutatePool();
                    var choiceCount = 3;
                    var choices = new List<BaseMutation>(choiceCount);
                    var strings = new List<String>(choiceCount);
                    foreach (var mutationType in possibleMutations.InRandomOrder(MutationsRandom)) {
                        var mutation = mutationType.CreateInstance();
                        choices.Add(mutation);
                        strings.Add("{{W|" + mutation.DisplayName + "}}  {{y|- " + mutation.GetDescription() + "}}\n" + mutation.GetLevelText(1));
                        if (choices.Count == choiceCount) {
                            break;
                        }
                    }
                    if (0 == choices.Count) {
                        WantNewMutations = false;
                        NewMutationSavings = 0;
                        // spend our points if we can
                        ParentObject.UseMP(0);
                        return true;
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
                        result.SetVariant(MutationsRandom.Next(result.GetVariants().Count));
                    }
                    var mutationIndex = mutations.AddMutation(result, 1);
                    this.DidX("gain", mutations.MutationList[mutationIndex].DisplayName, "!", UsePopup: true);

                    NewMutationSavings -= 4;
                    ParentObject.UseMP(4);
                }
            } else {
                ParentObject.GetPart<Mutations>().LevelMutation(which, which.BaseLevel + 1);
                ParentObject.UseMP(1);
            }

            return true;
        }

        public override void Register(GameObject obj) {
            obj.RegisterPartEvent(this, "SyncMutationsLevels");
            base.Register(obj);
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
                    var prefix = (Mutation.BaseLevel == Mutation.GetMaxLevel() && !Mutation.IsPhysical()) ?
                                    "*" :
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
                                                Intro: ("What mutations should " + ParentObject.the + ParentObject.ShortDisplayName + " advance?"),
                                                AllowEscape: true);
                if (index < 0) {
                    if (0 == FocusingMutations.Count && !WantNewMutations) {
                        // don't bother listening if there's nothing to hear
                        ParentObject.RemovePart<AIManageMutations>();
                    }
                    return changed;
                }
                if (keys.Count - 1 == index) {
                    if (strings[index][0] != '*') {
                        changed = true;
                        WantNewMutations = !WantNewMutations;
                        strings[index] = (WantNewMutations ? '+' : '-') + strings[index].Substring(1);
                    }
                } else {
                    switch (strings[index][0]) {
                        case '*':
                            // ignore
                            break;
                        case '-':
                            // start leveling this mutation
                            FocusingMutations.Add(mutations[index]);
                            strings[index] = '+' + strings[index].Substring(1);
                            changed = true;
                            break;
                        case '+':
                            // stop leveling this mutation
                            FocusingMutations.Remove(mutations[index]);
                            strings[index] = '-' + strings[index].Substring(1);
                            changed = true;
                            break;
                    }
                }
            }
        }
    }
}