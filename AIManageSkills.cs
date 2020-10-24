using System;

namespace XRL.World.Parts.CleverGirl
{
    using XRL.World.CleverGirl;

    [Serializable]
    public class AIManageSkills : IPart {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction{
            Name = "Clever Girl - Manage Skills",
            Display = "manage s{{inventoryhotkey|k}}ills",
            Command = "CleverGirl_ManageSkills",
            Key = 'k',
        };

        public void Manage() {
            
        }
    }
}