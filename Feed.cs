namespace XRL.World.CleverGirl {
    using System;
    using System.Collections.Generic;
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
            return Utility.InventoryAction.Adjacent(e) && (HasFood(e.Actor) || HasFood(e.Object) || NextToUsableCampfire(e.Actor));
        }

        private static bool HasFood(GameObject gameObject) {
            return gameObject.HasObjectInInventory(item => item.HasPart(typeof(Food)));
        }

        private static bool NextToUsableCampfire(GameObject gameObject) {
            if (!gameObject.HasSkill("CookingAndGathering")) {
                return false;
            }
            Cell cell = gameObject.pPhysics.CurrentCell;
            if (cell == null) {
                // how are we interacting with someone not in a cell?
                return false;
            }
            return cell.GetObjectCountWithPart("Campfire") > 0 || cell.AnyAdjacentCell(adj => adj.GetObjectCountWithPart("Campfire") > 0);
        }

        public static bool DoFeed(GameObject Leader, GameObject Follower) {
            var options = new List<string>();
            var keys = new List<char>();
            var actions = new List<Func<GameObject, GameObject, bool>>();
            if (NextToUsableCampfire(Leader)) {
                options.Add("Choose ingredients to cook with.");
                actions.Add(FeedFromIngredients);

                options.Add("Cook from a recipe.");
                actions.Add(FeedFromRecipe);
            }
            if (Leader.Inventory != null) {
                foreach (var item in Leader.Inventory.GetObjects(o => o.HasPart(typeof(Food)))) {
                    options.Add(item.DisplayName);
                    actions.Add(FeedItem(item));
                }
            }
            if (Follower.Inventory != null) {
                foreach (var item in Follower.Inventory.GetObjects(o => o.HasPart(typeof(Food)))) {
                    options.Add(item.DisplayName);
                    actions.Add(FeedItem(item));
                }
            }
            while (keys.Count < options.Count) {
                if (keys.Count < 26) {
                    keys.Add((char)('a' + keys.Count));
                } else {
                    keys.Add(' ');
                }
            }
            while (true) {
                var index = Popup.ShowOptionList(Options: options.ToArray(),
                                                Hotkeys: keys.ToArray(),
                                                Intro: "What's for dinner, boss?",
                                                AllowEscape: true);
                if (index == -1) {
                    return false;
                }
                if (actions[index](Leader, Follower)) {
                    return true;
                }
            }
        }

        private static bool FeedFromIngredients(GameObject Leader, GameObject Follower) {
            return false;
        }

        private static bool FeedFromRecipe(GameObject Leader, GameObject Follower) {
            return false;
        }

        private static Func<GameObject, GameObject, bool> FeedItem(GameObject Item) {
            return (GameObject Leader, GameObject Follower) => false;
        }
    }
}
