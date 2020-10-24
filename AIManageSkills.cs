using System;

namespace XRL.World.Parts.CleverGirl
{
    using System.Collections.Generic;
    using System.Linq;
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
            "CookingAndGathering",
            "Customs",
            "Survival",
            "Tinkering",
            "Firstaid_Setlimb",
            "Discipline_FastingWay",
            "Discipline_MindOverBody",
        };

        public List<string> LearningSkills = new List<string>();

        public void Manage() {
            var skills = new List<string>(SkillFactory.Factory.SkillList.Count);
            var strings = new List<string>(SkillFactory.Factory.SkillList.Count);
            var keys = new List<char>(SkillFactory.Factory.SkillList.Count);
            foreach (var Skill in SkillFactory.Factory.SkillList.Values) {
                if (IgnoreSkills.Contains(Skill.Class)) {
                    continue;
                }
                skills.Add(Skill.Class);
                var havePowers = 0;
                var totalPowers = 0;
                foreach (var Power in Skill.Powers.Values) {
                    if (IgnoreSkills.Contains(Power.Class)) {
                        continue;
                    }
                    if (ParentObject.HasSkill(Power.Class)) {
                        ++havePowers;
                    }
                    ++totalPowers;
                }
                var prefix = havePowers == totalPowers ? "*" : LearningSkills.Contains(Skill.Class) ? "+" : "-";
                strings.Add(prefix + " " + Skill.Name + ": " + havePowers + "/" + totalPowers);
                keys.Add((char)('a' + keys.Count));
            }

            while (true) {
                var index = Popup.ShowOptionList(Options: strings.ToArray(),
                                                Hotkeys: keys.ToArray(),
                                                Intro: ("What skills should " + ParentObject.the + ParentObject.ShortDisplayName + " learn?"),
                                                AllowEscape: true);
                if (index < 0) {
                    return;
                }
                switch (strings[index][0]) {
                    case '*':
                        // ignore
                        break;
                    case '-':
                        // start learning this skill
                        LearningSkills.Add(skills[index]);
                        strings[index] = '+' + strings[index].Substring(1);
                        break;
                    case '+':
                        // stop learning this skill
                        LearningSkills.Remove(skills[index]);
                        strings[index] = '-' + strings[index].Substring(1);
                        break;
                }
            }
        }
    }
}