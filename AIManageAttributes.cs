namespace XRL.World.Parts {
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml;
    using System.Xml.Schema;
    using System.Xml.Serialization;
    using XRL.UI;
    using XRL.World.CleverGirl;

    [Serializable]
    public class CleverGirl_AIManageAttributes : CleverGirl_INoSavePart, IXmlSerializable {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction {
            Name = "Clever Girl - Manage Attributes",
            Display = "manage att{{inventoryhotkey|r}}ibutes",
            Command = "CleverGirl_ManageAttributes",
            Key = 'r',
        };
        public static string PROPERTY => "CleverGirl_AIManageAttributes";
        public static string HONINGATTRIBUTES_PROPERTY => PROPERTY + "_HoningAttributes";
        public override void Register(GameObject Object) {
            _ = Object.SetIntProperty(PROPERTY, 1);
            if (!Object.HasStringProperty(HONINGATTRIBUTES_PROPERTY)) {
                Object.SetStringProperty(HONINGATTRIBUTES_PROPERTY, "");
            }
        }
        public override void Remove() {
            ParentObject.RemoveIntProperty(PROPERTY);
            ParentObject.RemoveStringProperty(HONINGATTRIBUTES_PROPERTY);
        }
        public List<string> HoningAttributes {
            get => ParentObject.GetStringProperty(HONINGATTRIBUTES_PROPERTY).Split(',').Where(s => !s.IsNullOrEmpty()).ToList();
            set => ParentObject.SetStringProperty(HONINGATTRIBUTES_PROPERTY, string.Join(",", value));
        }

        public override bool WantEvent(int ID, int cascade) => ID == StatChangeEvent.ID;

        public override bool HandleEvent(StatChangeEvent E) {
            if (E.Name == "AP") {
                SpendAP();
            }
            return true;
        }

        [NonSerialized]
        public static Dictionary<string, string> Comparatives = new Dictionary<string, string>{
            {"Strength", "stronger"},
            {"Agility", "quicker"},
            {"Toughness", "tougher"},
            {"Intelligence", "smarter"},
            {"Willpower", "stronger-willed"},
            {"Ego", "more compelling"}
        };

        [NonSerialized]
        public static Dictionary<string, string[]> Categories = new Dictionary<string, string[]>{
            {"Strength", new string[]{"feeble", "weak", "average", "strong", "beefy", "heckin' swole"}},
            {"Agility", new string[]{"ponderous", "slow", "average", "quick", "olympian", "sonic fast"}},
            {"Toughness", new string[]{"frail", "vulnerable", "average", "tough", "tanky", "slug sponge"}},
            {"Intelligence", new string[]{"incompetent", "dull", "average", "smart", "brilliant", "galaxy brain"}},
            {"Willpower", new string[]{"pushover", "gullible", "average", "strong-willed", "stalwart", "indefatigable"}},
            {"Ego", new string[]{"intolerable", "abrasive", "average", "compelling", "magnificent", "deific"}}
        };

        [NonSerialized]
        public static string[] CategoryColors = new string[] { "dark red", "red", "gray", "green", "orange", "extradimensional" };

        public void SpendAP() {
            var apStat = ParentObject.Statistics["AP"];

            if (apStat.Value > 0 && HoningAttributes.Count > 0) {
                var which = HoningAttributes.GetRandomElement(Utility.Random(this));
                ++ParentObject.Statistics[which].BaseValue;
                ++apStat.Penalty;

                DidX("become", Comparatives[which], "!", ColorAsGoodFor: ParentObject);
            }
        }

        public bool Manage() {
            var changed = false;
            var attributes = new List<string> { "Strength", "Agility", "Toughness", "Intelligence", "Willpower", "Ego" };
            var strings = new List<string>(attributes.Count);
            var keys = new List<char>(attributes.Count);
            foreach (var attr in attributes) {
                var prefix = HoningAttributes.Contains(attr) ? "+" : "-";
                var value = ParentObject.Statistics[attr].Value;
                var bucket = value <= 6 ? 0 :
                             value <= 12 ? 1 :
                             value <= 17 ? 2 :
                             value <= 25 ? 3 :
                             value <= 35 ? 4 :
                                           5;
                strings.Add(prefix + " " + attr + ": {{" + CategoryColors[bucket] + "|" + Categories[attr][bucket] + "}}");
                keys.Add(keys.Count >= 26 ? ' ' : (char)('a' + keys.Count));
            }

            while (true) {
                var index = Popup.ShowOptionList(Options: strings.ToArray(),
                                                Hotkeys: keys.ToArray(),
                                                Intro: "What attributes should " + ParentObject.the + ParentObject.ShortDisplayName + " hone?",
                                                AllowEscape: true);
                if (index < 0) {
                    if (HoningAttributes.Count == 0) {
                        // don't bother listening if there's nothing to hear
                        ParentObject.RemovePart<CleverGirl_AIManageAttributes>();
                    } else {
                        // spend any ability points we have saved up
                        SpendAP();
                    }
                    return changed;
                }
                if (strings[index][0] == '-') {
                    // start honing this attribute
                    var working = HoningAttributes;
                    working.Add(attributes[index]);
                    HoningAttributes = working;

                    strings[index] = '+' + strings[index].Substring(1);
                    changed = true;
                } else if (strings[index][0] == '+') {
                    // stop honing this attribute
                    var working = HoningAttributes;
                    _ = working.Remove(attributes[index]);
                    HoningAttributes = working;

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
            writer.WriteStartElement("HoningAttributes");
            foreach (string attr in HoningAttributes) {
                writer.WriteElementString("name", attr);
            }
            writer.WriteEndElement();
        }

        public void ReadXml(XmlReader reader) {
            reader.ReadStartElement();

            reader.ReadStartElement("HoningAttributes");
            while (reader.MoveToContent() != XmlNodeType.EndElement) {
                HoningAttributes.Add(reader.ReadElementContentAsString("name", ""));
            }
            reader.ReadEndElement();

            reader.ReadEndElement();
        }
    }
}
