namespace XRL.World.CleverGirl {
    using ConsoleLib.Console;
    using Qud.API;
    using System.Collections.Generic;
    using XRL.Core;
    using XRL.UI;
    using XRL.World.Parts;

    public static class Feed {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction {
            Name = "Clever Girl - Feed",
            Display = "{{inventoryhotkey|f}}eed",
            Command = "CleverGirl_Feed",
            Key = 'f',
            Valid = CanFeed,
        };

        private static bool CanFeed(OwnerGetInventoryActionsEvent e) {
            return HasFood(e.Actor) || HasFood(e.Object) || NextToCampfire(e.Actor);
        }

        private static bool HasFood(GameObject gameObject) {
            return gameObject.HasObjectInInventory(item => item.HasPart(typeof(Food)));
        }

        private static bool NextToCampfire(GameObject gameObject) {
            Cell cell = gameObject.pPhysics.CurrentCell;
            if (cell == null) {
                // how are we interacting with someone not in a cell?
                return false;
            }
            return cell.GetObjectCountWithPart("Campfire") > 0 || cell.AnyAdjacentCell(adj => adj.GetObjectCountWithPart("Campfire") > 0);
        }
    }
}
