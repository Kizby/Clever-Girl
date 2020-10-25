using System;

namespace XRL.World.Parts.CleverGirl
{
    using System.Collections.Generic;
    using System.Linq;
    using XRL.Rules;
    using XRL.UI;
    using XRL.World.CleverGirl;
    using XRL.World.Skills;

    [Serializable]
    public class AIManageSkills : IPart {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction{
            Name = "Clever Girl - Manage Skills",
            Display = "manage s{{inventoryhotkey|k}}ills",
            Command = "CleverGirl_ManageSkills",
            Key = 'k',
        };

        // these skills don't make sense for followers
        static string[] IgnoreSkills = {
            "Cooking and Gathering",
            "Customs and Folklore",
            "Tinkering",
            "Wayfaring",
            "Set Limb",
            "Fasting Way",
            "Mind over Body",
        };

        Random random = Stat.GetSeededRandomGenerator("Kizby_CleverGirl");

        public List<string> LearningSkills = new List<string>();

        public override bool WantEvent(int ID, int cascade)
        {
            return ID == StatChangeEvent.ID;
        }

        public override bool HandleEvent(StatChangeEvent E) {
            if ("SP" != E.Name) {
                return true;
            }
            var budget = E.NewValue;
            var pool = new List<Tuple<string, int, string>>();
            var toDrop = new List<string>();
            foreach (var skillName in LearningSkills) {
                var skill = SkillFactory.Factory.SkillList[skillName];
                bool hasAllPowers = true;
                if (ParentObject.HasSkill(skill.Class)) {
                    foreach (var power in skill.Powers.Values) {
                        if (!ParentObject.HasSkill(power.Class)) {
                            hasAllPowers = false;
                            if (power.Cost > budget) {
                                continue;
                            }
                            if (!power.MeetsRequirements(ParentObject)) {
                                continue;
                            }
                            pool.Add(Tuple.Create(power.Class, power.Cost, power.Name));
                        }
                    }
                } else {
                    hasAllPowers = false;
                    if (skill.Cost <= budget) {
                        pool.Add(Tuple.Create(skill.Class, skill.Cost, skill.Name));
                    }
                }
                if (hasAllPowers) {
                    toDrop.Add(skillName);
                }
            }
            // drop skills that are already complete
            LearningSkills = LearningSkills.Except(toDrop).ToList();

            if (0 == pool.Count) {
                // nothing to learn
                return true;
            }

            var which = pool.GetRandomElement(random);
            ParentObject.AddSkill(which.Item1);
            E.Stat.Penalty += which.Item2;

            this.DidX("learn", which.Item3, "!", ColorAsGoodFor: this.ParentObject);
            return true;
        }

        public bool Manage() {
            var changed = false;
            var skills = new List<string>(SkillFactory.Factory.SkillList.Count);
            var strings = new List<string>(SkillFactory.Factory.SkillList.Count);
            var keys = new List<char>(SkillFactory.Factory.SkillList.Count);
            foreach (var Skill in SkillFactory.Factory.SkillList.Values) {
                if (IgnoreSkills.Contains(Skill.Name)) {
                    continue;
                }
                skills.Add(Skill.Name);
                var havePowers = 0;
                var totalPowers = 0;
                foreach (var Power in Skill.Powers.Values) {
                    if (IgnoreSkills.Contains(Power.Name)) {
                        continue;
                    }
                    if (ParentObject.HasSkill(Power.Class)) {
                        ++havePowers;
                    }
                    ++totalPowers;
                }
                var prefix = havePowers == totalPowers ? "*" : LearningSkills.Contains(Skill.Name) ? "+" : "-";
                strings.Add(prefix + " " + Skill.Name + ": " + havePowers + "/" + totalPowers);
                keys.Add((char)('a' + keys.Count));
            }

            while (true) {
                var index = Popup.ShowOptionList(Options: strings.ToArray(),
                                                Hotkeys: keys.ToArray(),
                                                Intro: ("What skills should " + ParentObject.the + ParentObject.ShortDisplayName + " learn?"),
                                                AllowEscape: true);
                if (index < 0) {
                    if (0 == LearningSkills.Count) {
                        // don't bother listening if there's nothing to hear
                        ParentObject.RemovePart<AIManageSkills>();
                    }
                    return changed;
                }
                switch (strings[index][0]) {
                    case '*':
                        // ignore
                        break;
                    case '-':
                        // start learning this skill
                        LearningSkills.Add(skills[index]);
                        strings[index] = '+' + strings[index].Substring(1);
                        changed = true;
                        break;
                    case '+':
                        // stop learning this skill
                        LearningSkills.Remove(skills[index]);
                        strings[index] = '-' + strings[index].Substring(1);
                        changed = true;
                        break;
                }
            }
        }
    }
}