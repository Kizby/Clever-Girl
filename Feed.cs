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

            // haven't implemented campfires yet
            return false && (cell.GetObjectCountWithPart("Campfire") > 0 || cell.AnyAdjacentCell(adj => adj.GetObjectCountWithPart("Campfire") > 0));
        }

        public static bool DoFeed(GameObject Leader, GameObject Companion) {
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
            if (Companion.Inventory != null) {
                foreach (var item in Companion.Inventory.GetObjects(o => o.HasPart(typeof(Food)))) {
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
                if (actions[index](Leader, Companion)) {
                    return true;
                }
            }
        }

        private static bool FeedFromIngredients(GameObject Leader, GameObject Companion) {
            return false;
        }

        private static bool FeedFromRecipe(GameObject Leader, GameObject Companion) {
            return false;
        }

        private static Func<GameObject, GameObject, bool> FeedItem(GameObject Item) {
            return (Leader, Companion) => {
                Food Food = Item.GetPart<Food>();
                bool IsCarnivorous = Companion.HasPart(typeof(Carnivorous));
                bool IsMeat = Item.HasTag("Meat");
                bool Gross = Food.Gross;
                if (Gross && IsCarnivorous && IsMeat) {
                    // carnivores will eat even gross meat
                    Gross = false;
                }

                Stomach Stomach = Companion.GetPart<Stomach>();
                bool WillEat = !Gross;
                bool Convincing = false;
                if (!WillEat) {
                    Convincing = Utility.Roll("2d6", Stomach) + Leader.StatMod("Ego") - 6 > Stats.GetCombatMA(Companion);
                    if (Convincing) {
                        WillEat = true;
                    } else {
                        // chance to agree regardless
                        WillEat = Utility.Roll("1d6", Stomach) >= 6;
                    }
                }

                string DisgustingName(GameObject gameObject) {
                    var existingAdjectives = gameObject.GetPartDescendedFrom<DisplayNameAdjectives>();
                    bool alreadyDisgusting = existingAdjectives?.AdjectiveList.Contains("disgusting") == true;
                    (existingAdjectives ?? gameObject.RequirePart<DisplayNameAdjectives>()).RequireAdjective("disgusting");

                    var result = gameObject.one(NoStacker: true);

                    if (existingAdjectives == null) {
                        gameObject.RemovePart<DisplayNameAdjectives>();
                    } else if (!alreadyDisgusting) {
                        existingAdjectives.RemoveAdjective("disgusting");
                    }
                    return result;
                }

                if (!WillEat) {
                    Popup.Show(Companion.One() + Companion.GetVerb("refuse") + " to eat " + DisgustingName(Item) + "!");
                    return true;
                }

                bool WasIll = Companion.HasEffect<Ill>();
                // fake hunger so they'll eat whatever they're fed
                Stomach.HungerLevel = 2;
                GetInventoryActionsEvent GetInventoryActionsEvent = GetInventoryActionsEvent.FromPool(Companion, Item, null);
                _ = Item.HandleEvent(GetInventoryActionsEvent);
                if (!GetInventoryActionsEvent.Actions.ContainsKey("Eat")) {
                    // can't eat it?
                    return false;
                }
                _ = GetInventoryActionsEvent.Actions["Eat"].Process(Item, Companion);

                string Message = Utility.AdjustSubject(Food.Message, Companion);
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
                if (!WasIll && Companion.HasEffect<Ill>()) {
                    Message += " " + Companion.It + Companion.GetVerb("look") + " sick.";
                }
                if (Convincing) {
                    Popup.Show(Leader.It + Leader.GetVerb("convince") + " " + Companion.one() + " to eat " + DisgustingName(Item) + Message);
                } else {
                    string Adverb = Gross ? " begrudgingly" : " hungrily";
                    Popup.Show(Companion.One() + Adverb + Companion.GetVerb("eat") + " " + Item.one(NoStacker: true) + Message);
                }
                return true;
            };
        }
    }
}
