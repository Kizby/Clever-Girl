using System;

namespace XRL.World.Parts.CleverGirl
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using XRL.UI;
    using XRL.World.CleverGirl;
    using XRL.World.Skills;

    [Serializable]
    public class AIManageSkills : IPart, IXmlSerializable {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction{
            Name = "Clever Girl - Manage Skills",
            Display = "manage s{{inventoryhotkey|k}}ills",
            Command = "CleverGirl_ManageSkills",
            Key = 'k',
        };

        // these skills don't make sense for followers
        public static HashSet<string> IgnoreSkills = new HashSet<string>{
            "Cooking and Gathering",
            "Customs and Folklore",
            "Tinkering",
            "Wayfaring",
            "Set Limb",
            "Fasting Way",
            "Mind over Body",
        };

        public static HashSet<string> CombatSkills = new HashSet<string>{
            "Axe",
            "Bow and Rifle",
            "Cudgel",
            "Dual Wield",
            "Heavy Weapon",
            "Long Blade",
            "Pistol",
            "Short Blade",
            "Menacing Stare",
            "Intimidate",
            "Berate",
            "Shield Slam",
            "Deft Throwing",
            "Charge",
        };

        public List<string> LearningSkills = new List<string>();


        // XMLSerialization for compatibility with Armithaig's Recur mod
        public XmlSchema GetSchema() => null;
        public void WriteXml(XmlWriter writer) {
            writer.WriteStartElement("LearningSkills");
            foreach (var skill in LearningSkills) {
                writer.WriteElementString("name", skill);
            }
            writer.WriteEndElement();
        }

        public void ReadXml(XmlReader reader) {
            var startDepth = reader.Depth;
            reader.ReadStartElement("LearningSkills");
            while (reader.Depth > startDepth) {
                LearningSkills.Add(reader.ReadElementContentAsString("name", null));
            }
            reader.ReadEndElement();
        }

        public override bool WantEvent(int ID, int cascade)
        {
            return ID == StatChangeEvent.ID;
        }

        public override bool HandleEvent(StatChangeEvent E) {
            if ("SP" == E.Name) {
                SpendSP();
            }
            return true;
        }

        public void SpendSP() {
            var stat = ParentObject.Statistics["SP"];
            var budget = stat.Value;
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

            if (0 < pool.Count) {
                var which = pool.GetRandomElement(Utility.Random(this));
                ParentObject.AddSkill(which.Item1);
                stat.Penalty += which.Item2; // triggers a StatChangeEvent which will call this again until all points are spent

                this.DidX("learn", which.Item3, "!", ColorAsGoodFor: this.ParentObject);
            }
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
                if (!ParentObject.IsCombatObject() && CombatSkills.Contains(Skill.Name)) {
                    continue;
                }
                skills.Add(Skill.Name);
                var havePowers = 0;
                var totalPowers = 0;
                foreach (var Power in Skill.Powers.Values) {
                    if (IgnoreSkills.Contains(Power.Name)) {
                        continue;
                    }
                    if (!ParentObject.IsCombatObject() && CombatSkills.Contains(Power.Name)) {
                        continue;
                    }
                    if (ParentObject.HasSkill(Power.Class)) {
                        ++havePowers;
                    }
                    ++totalPowers;
                }
                var prefix = havePowers == totalPowers ? "*" : LearningSkills.Contains(Skill.Name) ? "+" : "-";
                strings.Add(prefix + " " + Skill.Name + ": " + havePowers + "/" + totalPowers);
                keys.Add(keys.Count >= 26 ? ' ' : (char)('a' + keys.Count));
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
                    } else {
                        // spend any skill points we have saved up
                        SpendSP();
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