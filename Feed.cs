namespace XRL.World.CleverGirl {
    using System;
    using System.Collections.Generic;
    using XRL.Rules;
    using XRL.UI;
    using XRL.World.Effects;
    using XRL.World.Parts;
    using XRL.World.Parts.Mutation;
    using XRL.World.Skills.Cooking;

    public static class Feed {
        public static readonly Utility.InventoryAction ACTION = new Utility.InventoryAction {
            Name = "Clever Girl - Feed",
            Display = "fee{{inventoryhotkey|d}}",
            Command = "CleverGirl_Feed",
            Key = 'd',
            Valid = CanFeed,
        };
        public static readonly Utility.InventoryAction COOKING_ACTION = new Utility.InventoryAction {
            Name = "Clever Girl - Feed Multiple",
            Display = "feed companions",
            Command = "CleverGirl_FeedMultiple",
            Key = 'd',
        };

        private static bool CanFeed(IInventoryActionsEvent E) {
            return Utility.InventoryAction.Adjacent(E) && CanEat(E.Object) &&
                (HasFood(E.Actor) || HasFood(E.Object) || NextToUsableCampfire(E.Actor));
        }

        public static bool CanEat(GameObject obj) {
            return obj.HasPartDescendedFrom<Stomach>();
        }

        private static bool HasFood(GameObject gameObject) {
            return gameObject.HasObjectInInventory(item => item.HasPart(typeof(Food)));
        }

        private static List<GameObject> CollectUsableCampfires(GameObject Leader) {
            var result = new List<GameObject>();
            Cell cell = Leader.pPhysics.CurrentCell;
            if (cell == null) {
                // how are we interacting with someone not in a cell?
                return result;
            }

            cell.ForeachLocalAdjacentCellAndSelf(adj => adj.ForeachObjectWithPart(nameof(Campfire), obj => {
                if (Campfire.hasSkill || obj.GetPart<Campfire>().presetMeals.Count > 0) {
                    result.Add(obj);
                }
            }));
            return result;
        }

        private static bool NextToUsableCampfire(GameObject Leader) {
            // haven't implemented campfires yet
            return CollectUsableCampfires(Leader).Count > 0;
        }

        public static List<GameObject> CollectFeedableCompanions(GameObject Leader) {
            var result = new List<GameObject>();

            // allow companions to be daisy-chained so long as they're adjacent to each other
            var toInspect = new List<Cell> { Leader.CurrentCell };
            for (int i = 0; i < toInspect.Count; ++i) {
                var cell = toInspect[i];
                cell.ForeachObject(obj => {
                    if (obj == Leader || obj.IsLedBy(Leader)) {
                        cell.ForeachLocalAdjacentCell(adj => {
                            if (!toInspect.Contains(adj)) {
                                toInspect.Add(adj);
                            }
                        });
                        if (obj != Leader) {
                            result.Add(obj);
                        }
                    }
                });
            }
            return result;
        }

        public static bool DoFeed(GameObject Leader, ref int EnergyCost) {
            var options = new List<string>();
            var keys = new List<char>();
            options.Add("Feed everyone!");
            options.Add("Feed selected:");
            var companions = CollectFeedableCompanions(Leader);
            if (companions.Count == 1) {
                EnergyCost = 100;
                return DoFeed(Leader, companions);
            }
            foreach (var companion in companions) {
                options.Add("- " + companion.one(WithIndefiniteArticle: true));
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
                                                Intro: "Your companions {{watery|salivate}} expectantly.",
                                                centerIntro: true,
                                                AllowEscape: true);
                if (index == -1) {
                    return false;
                }
                if (index > 1) {
                    options[index] = (options[index][0] == '-' ? "+" : "-") + options[index].Substring(1);
                } else {
                    var toFeed = new List<GameObject>();
                    if (index == 0) {
                        toFeed = companions;
                    } else {
                        for (int i = 2; i < options.Count; ++i) {
                            if (options[i][0] == '+') {
                                toFeed.Add(companions[i - 2]);
                            }
                        }
                    }
                    if (toFeed.Count > 0 && DoFeed(Leader, toFeed)) {
                        EnergyCost = 100 * toFeed.Count;
                        return true;
                    }
                }
            }
        }

        private static bool DoFeed(GameObject Leader, List<GameObject> Companions) {
            var options = new List<string>();
            var keys = new List<char>();
            var actions = new List<Func<GameObject, List<GameObject>, bool>>();
            var campfires = CollectUsableCampfires(Leader);
            if (campfires.Count > 0) {
                var presetMeals = new HashSet<CookingRecipe>();
                foreach (var campfire in campfires) {
                    foreach (var meal in campfire.GetPart<Campfire>().presetMeals) {
                        if (presetMeals.Add(meal)) {
                            options.Add(campfire.GetTag("PresetMealMessage") ?? "Eat " + meal.GetDisplayName().Replace("&W", "&Y").Replace("{{W|", "{{Y|"));
                            actions.Add(FeedPresetMeal(meal));
                        }
                    }
                }
                if (Campfire.hasSkill) {
                    options.Add("Choose ingredients to cook with.");
                    actions.Add(FeedFromIngredients);

                    options.Add("Cook from a recipe.");
                    actions.Add(FeedFromRecipe);
                }
            }
            // only hand-feed one at a time from inventories
            if (Companions.Count == 1) {
                if (Leader.Inventory != null) {
                    foreach (var item in Leader.Inventory.GetObjects(o => o.HasPart(typeof(Food)))) {
                        options.Add(item.DisplayName);
                        actions.Add((leader, companions) => FeedItem(item)(leader, companions[0]));
                    }
                }
                foreach (var Companion in Companions) {
                    if (Companion.Inventory != null) {
                        foreach (var item in Companion.Inventory.GetObjects(o => o.HasPart(typeof(Food)))) {
                            options.Add(item.DisplayName);
                            actions.Add((leader, companions) => FeedItem(item)(leader, companions[0]));
                        }
                    }
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
                if (actions[index](Leader, Companions)) {
                    return true;
                }
            }
        }

        private static Func<GameObject, List<GameObject>, bool> FeedPresetMeal(CookingRecipe Meal) {
            return (Leader, Companions) => {
                string description = "";
                foreach (var companion in Companions) {
                    _ = companion.FireEvent("ClearFoodEffects");
                    _ = companion.CleanEffects();
                    description = Meal.GetApplyMessage();
                    foreach (ICookingRecipeResult effect in Meal.Effects) {
                        description += effect.apply(companion) + "\n";
                    }
                }
                GameObject target = Companions[0];
                string targetName = Companions[0].One();
                if (Companions.Count > 1) {
                    // make a fake object so we pluralize
                    target = GameObject.create("Bones");
                    targetName = "Your companions";
                }
                Popup.Show(targetName + target.GetVerb("start") + " to metabolize the meal, gaining the following effect for the rest of the day:\n\n&W" + Campfire.ProcessEffectDescription(description, target));
                return true;
            };
        }

        public static bool DoFeed(GameObject Leader, GameObject Companion) {
            return DoFeed(Leader, new List<GameObject> { Companion });
        }

        private static bool FeedFromIngredients(GameObject Leader, List<GameObject> Companions) {
            return false;
        }

        private static bool FeedFromRecipe(GameObject Leader, List<GameObject> Companions) {
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
