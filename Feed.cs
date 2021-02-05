namespace XRL.World.CleverGirl {
    using System;
    using System.Collections.Generic;
    using XRL.Rules;
    using XRL.UI;
    using XRL.World.Effects;
    using XRL.World.Parts;
    using XRL.World.Parts.Mutation;

    public static class Feed {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction {
            Name = "Clever Girl - Feed",
            Display = "{{inventoryhotkey|f}}eed",
            Command = "CleverGirl_Feed",
            Key = 'f',
            Valid = CanFeed,
        };

        private static bool CanFeed(OwnerGetInventoryActionsEvent E) {
            return Utility.InventoryAction.Adjacent(E) &&
                E.Object.HasPart(typeof(Stomach)) &&
                (HasFood(E.Actor) || HasFood(E.Object) || NextToUsableCampfire(E.Actor));
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
            return (Leader, Follower) => {
                Food Food = Item.GetPart<Food>();
                bool IsCarnivorous = Follower.HasPart(typeof(Carnivorous));
                bool IsMeat = Item.HasTag("Meat");
                bool Gross = Food.Gross;
                if (Gross && IsCarnivorous && IsMeat) {
                    // carnivores will eat even gross meat
                    Gross = false;
                }

                Stomach Stomach = Follower.GetPart<Stomach>();
                bool WillEat = !Gross;
                bool Convincing = false;
                if (!WillEat) {
                    Convincing = Utility.Roll("2d6", Stomach) + Leader.StatMod("Ego") - 6 > Stats.GetCombatMA(Follower);
                    if (Convincing) {
                        WillEat = true;
                    } else {
                        // chance to agree regardless
                        WillEat = Utility.Roll("1d6", Stomach) >= 6;
                    }
                }
                if (!WillEat) {
                    Popup.Show(Follower.ShortDisplayName + Follower.GetVerb("refuse") + " to eat " + Item.SituationalArticle() + " disgusting " + Item.DisplayNameSingle + "!");
                    return true;
                }

                bool WasIll = Follower.HasEffect<Ill>();
                // fake hunger so they'll eat whatever they're fed
                Stomach.HungerLevel = 2;
                GetInventoryActionsEvent GetInventoryActionsEvent = GetInventoryActionsEvent.FromPool(Follower, Item, null);
                _ = Item.HandleEvent(GetInventoryActionsEvent);
                if (!GetInventoryActionsEvent.Actions.ContainsKey("Eat")) {
                    // can't eat it?
                    return false;
                }
                _ = GetInventoryActionsEvent.Actions["Eat"].Process(Item, Follower);

                string Message = Food.Message;
                if (Message == "That hits the spot!" && Gross) {
                    // no it doesn't
                    Message = "Blech!";
                }
                if (Message.Length > 0) {
                    Message = ". " + Message;
                }
                if (!Message.EndsWith("!") && !Message.EndsWith(".") && !Message.EndsWith("?")) {
                    Message += ".";
                }
                if (!WasIll && Follower.HasEffect<Ill>()) {
                    Message += " " + Follower.It + Follower.GetVerb("look") + " sick.";
                }
                if (Convincing) {
                    Popup.Show(Leader.It + Leader.GetVerb("convince") + " " + Follower.SituationalArticle() + Follower.ShortDisplayName +
                               " to eat " + Item.SituationalArticle() + " disgusting " + Item.DisplayNameSingle + Message);
                } else {
                    string Adverb = Gross ? " begrudgingly" : " hungrily";
                    Popup.Show(Follower.ShortDisplayName + Adverb + Follower.GetVerb("eat") + " " + Item.SituationalArticle() + Item.DisplayNameSingle + Message);
                }
                return true;
            };
        }
    }
}
