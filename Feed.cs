namespace XRL.World.CleverGirl {
    using System;
    using System.Collections.Generic;
    using ConsoleLib.Console;
    using HarmonyLib;
    using Qud.API;
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
            var options = new List<string> {
                "Feed everyone!",
                ""
            };
            var icons = new List<IRenderable> { null, null };
            var companions = CollectFeedableCompanions(Leader);
            if (companions.Count == 1) {
                EnergyCost = 100;
                return DoFeed(Leader, companions);
            }
            foreach (var companion in companions) {
                options.Add("[ ]   " + companion.one(WithIndefiniteArticle: true));
                icons.Add(companion.RenderForUI());
            }
            const string check = "{{y|[{{G|X}}]}}";
            var feedCount = 0;
            var last = 0;
            while (true) {
                options[1] = (feedCount == 0 ? "&K" : "") + "Feed " + feedCount + " selected companion" + (feedCount == 1 ? "" : "s") + ".";
                var index = Popup.ShowOptionList("{{W|Your companions {{watery|salivate}} expectantly.}}",
                                                Options: options.ToArray(),
                                                Icons: icons.ToArray(),
                                                iconPosition: 6,
                                                centerIntro: true,
                                                defaultSelected: last,
                                                AllowEscape: true);
                if (index == -1) {
                    return false;
                }
                if (index > 1) {
                    last = index;
                    if (options[index].StartsWith(check)) {
                        options[index] = "[ ]" + options[index].Substring(check.Length);
                        --feedCount;
                    } else {
                        options[index] = check + options[index].Substring("[ ]".Length);
                        ++feedCount;
                    }
                } else {
                    var toFeed = new List<GameObject>();
                    if (index == 0) {
                        toFeed = companions;
                    } else {
                        for (int i = 2; i < options.Count; ++i) {
                            if (options[i].StartsWith(check)) {
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
                    actions.Add((leader, companions) => FeedFromIngredients(leader, companions, campfires[0].GetPart<Campfire>()));

                    options.Add("Cook from a recipe.");
                    actions.Add(FeedFromRecipe);
                }
            }
            // only hand-feed one at a time from inventories
            IRenderable introIcon = null;
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
                    introIcon = Companion.RenderForUI();
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
                var index = Popup.ShowOptionList("What's for dinner, boss?",
                                                Options: options.ToArray(),
                                                Hotkeys: keys.ToArray(),
                                                IntroIcon: introIcon,
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
                bool fakeTarget = false;
                string targetName = Companions[0].One();
                if (Companions.Count > 1) {
                    // make a fake object so we pluralize
                    target = GameObject.create("Bones");
                    fakeTarget = true;
                    targetName = "Your companions";
                }
                Popup.Show(targetName + target.GetVerb("start") + " to metabolize the meal, gaining the following effect for the rest of the day:\n\n&W" + Campfire.ProcessEffectDescription(description, target));
                if (fakeTarget) {
                    _ = target.Destroy(Silent: true, Obliterate: true);
                }
                return true;
            };
        }

        public static bool DoFeed(GameObject Leader, GameObject Companion) {
            return DoFeed(Leader, new List<GameObject> { Companion });
        }

        private class Ingredient {
            public string Name;
            public int Count;
            public IRenderable Icon;
            public List<GameObject> Objects;
        }
        private static bool FeedFromIngredients(GameObject Leader, List<GameObject> Companions, Campfire Campfire) {
            var ingredients = Campfire.GetValidCookingIngredients(Leader);
            var anyCarnivorous = false;
            foreach (var companion in Companions) {
                ingredients.AddRange(Campfire.GetValidCookingIngredients(companion));
                if (companion.HasPart(nameof(Carnivorous))) {
                    anyCarnivorous = true;
                }
            }
            ingredients.Sort(Campfire.IngredientSort);

            var edibleIngredients = new Dictionary<string, Ingredient>();
            foreach (var ingredient in ingredients) {
                if (anyCarnivorous && (ingredient.HasTag("Plant") || ingredient.HasTag("Fungus"))) {
                    continue;
                }
                string name;
                int count;
                IRenderable icon = ingredient.RenderForUI();
                if (ingredient.LiquidVolume != null) {
                    name = ingredient.LiquidVolume.GetPreparedCookingIngredient();
                    count = ingredient.LiquidVolume.Volume;
                } else {
                    name = ingredient.GetCachedDisplayNameStripped();
                    count = ingredient.Count;
                }
                if (!edibleIngredients.ContainsKey(name)) {
                    edibleIngredients[name] = new Ingredient { Name = name, Count = 0, Icon = icon, Objects = new List<GameObject>() };
                }
                edibleIngredients[name].Count += count;
                edibleIngredients[name].Objects.Add(ingredient);
            }

            var finalIngredients = new List<Ingredient>();
            foreach (var ingredient in edibleIngredients.Values) {
                if (ingredient.Count >= Companions.Count) {
                    finalIngredients.Add(ingredient);
                }
            }
            finalIngredients.Sort((a, b) => ColorUtility.CompareExceptFormattingAndCase(a.Objects[0].GetDisplayName(NoColor: true), b.Objects[0].GetDisplayName(NoColor: true)));
            var maxIngredients = Leader.HasSkill("CookingAndGathering_Spicer") ? 3 : 2;

            var options = new List<string> { "" };
            var icons = new List<IRenderable> { null };
            foreach (var ingredient in finalIngredients) {
                options.Add("[ ]   " + ingredient.Objects[0].GetDisplayName(1120) + " x" + Companions.Count);
                icons.Add(ingredient.Objects[0].RenderForUI());
            }
            const string check = "{{y|[{{G|X}}]}}";
            var countIngredients = 0;
            var last = 0;
            while (true) {
                var countString = "{{" + (countIngredients > maxIngredients ? "R" : "C") + "|" + countIngredients + "}}";
                options[0] = "{{W|Cook with the " + countString + " selected ingredients.}}";
                var index = Popup.ShowOptionList("Choose ingredients to cook with.",
                                                 options.ToArray(),
                                                 Intro: "Selected " + countString + " of " + maxIngredients + " possible ingredients.",
                                                 AllowEscape: true,
                                                 defaultSelected: last,
                                                 Icons: icons.ToArray(),
                                                 iconPosition: 6
                                                 );
                if (index == -1) {
                    return false;
                }
                if (index > 0) {
                    last = index;
                    if (options[index].StartsWith(check)) {
                        options[index] = "[ ]" + options[index].Substring(check.Length);
                        --countIngredients;
                    } else {
                        options[index] = check + options[index].Substring("[ ]".Length);
                        ++countIngredients;
                    }
                }
                if (index == 0 && countIngredients <= maxIngredients) {
                    break;
                }
            }

            var ingredientTypes = new List<string>();
            var mealIngredients = new List<Ingredient>();
            var mealObjects = Event.NewGameObjectList();
            var mealEffectiveIngredients = Event.NewGameObjectList();
            for (int i = 0; i < finalIngredients.Count; ++i) {
                if (!options[i + 1].StartsWith(check)) {
                    continue;
                }
                var realObj = finalIngredients[i].Objects[0];
                var effectiveObj = realObj;
                string type;
                if (realObj.HasPart(nameof(PreparedCookingIngredient))) {
                    var part = realObj.GetPart<PreparedCookingIngredient>();
                    type = part.GetTypeInstance();
                    if (part.type == "random") {
                        while (ingredientTypes.Contains(type)) {
                            type = part.GetTypeInstance();
                        }
                        effectiveObj = EncountersAPI.GetAnObjectNoExclusions(obj =>
                            (obj.HasPart(nameof(PreparedCookingIngredient)) && obj.GetPartParameter(nameof(PreparedCookingIngredient), "type").Contains(type)) ||
                            (obj.HasTag("LiquidCookingIngredient") && obj.createSample().LiquidVolume.GetPreparedCookingIngredient().Contains(type)));
                    }
                } else {
                    type = realObj.LiquidVolume.GetPreparedCookingIngredient().Split(',').GetRandomElement();
                }
                if (!ingredientTypes.Contains(type)) {
                    ingredientTypes.Add(type);
                }
                mealIngredients.Add(finalIngredients[i]);
                mealObjects.Add(realObj);
                mealEffectiveIngredients.Add(effectiveObj);
            }

            GameObject target = Companions[0];
            bool fakeTarget = false;
            string targetName = Companions[0].One();
            if (Companions.Count > 1) {
                // make a fake object so we pluralize
                target = GameObject.create("Bones");
                fakeTarget = true;
                targetName = "Your companions";
            }

            Popup.Show(Campfire.DescribeMeal(ingredientTypes, mealObjects));
            Popup.Show(targetName + target.GetVerb("eat") + " the meal.");
            if (ingredientTypes.Count > 0) {
                ProceduralCookingEffect actualEffect;
                if (Leader.HasEffect<Inspired>()) {
                    var recipeOptions = Campfire.GenerateEffectsFromTypeList(ingredientTypes, 3);
                    var index = Popup.ShowOptionList("You let inspiration guide you toward a mouthwatering dish.",
                        recipeOptions.ConvertAll(effect => Campfire.ProcessEffectDescription(effect.GetTemplatedProceduralEffectDescription(), target)).ToArray(),
                        Spacing: 1);

                    actualEffect = recipeOptions[index];
                    var newRecipe = CookingRecipe.FromIngredients(mealEffectiveIngredients, actualEffect, Companions[0].BaseDisplayName);
                    _ = CookingGamestate.LearnRecipe(newRecipe);
                    Popup.Show("You create a new recipe for {{|" + newRecipe.GetDisplayName() + "}}!");
                    var allCompanions = Companions[0].an();
                    if (Companions.Count == 2) {
                        allCompanions += " and " + Companions[1].an();
                    } else {
                        for (int i = 1; i < Companions.Count; ++i) {
                            allCompanions += ", ";
                            if (i == Companions.Count - 1) {
                                allCompanions += "and ";
                            }
                            allCompanions += Companions[i].an();
                        }
                    }
                    JournalAPI.AddAccomplishment("Your companions, " + allCompanions + ", inspired you to invent a mouthwatering dish called {{|" + newRecipe.GetDisplayName() + "}}.",
                        "Surrounded by friends, the Carbide Chef =name= immortalized the memor" + (Companions.Count == 1 ? "y" : "ies") + " of " + allCompanions + " in the mouthwatering dish called {{|" + newRecipe.GetDisplayName() + "}}.",
                        muralCategory: JournalAccomplishment.MuralCategory.CreatesSomething,
                        muralWeight: JournalAccomplishment.MuralWeight.Low);
                    Campfire.IncrementRecipeAchievement();
                    _ = Leader.RemoveEffect(nameof(Inspired));
                } else {
                    actualEffect = Campfire.GenerateEffectFromTypeList(ingredientTypes);
                }
                _ = Leader.FireEvent(Event.New("CookedAt", "Object", Campfire.ParentObject));
                foreach (var companion in Companions) {
                    _ = companion.FireEvent("ClearFoodEffects");
                    _ = companion.CleanEffects();

                    var tasty = Campfire.ForceTastyBasedOnIngredients(mealEffectiveIngredients) || 10.in100();
                    if (tasty) {
                        _ = companion.ApplyEffect(AccessTools.Method(typeof(Campfire), "RandomTastyEffect").Invoke(null, new object[] { "" }) as Effect);
                    }

                    var oneEffect = actualEffect.DeepCopy(null) as ProceduralCookingEffect;
                    oneEffect.Init(companion);
                    oneEffect.Duration = 1;
                    _ = companion.ApplyEffect(oneEffect);
                }
                Popup.Show(targetName + target.GetVerb("start") + " to metabolize the meal, gaining the following effect for the rest of the day:\n\n&W" + Campfire.ProcessEffectDescription(actualEffect.GetProceduralEffectDescription(), target));
            } else {
                foreach (var companion in Companions) {
                    _ = companion.FireEvent("ClearFoodEffects");
                    _ = companion.CleanEffects();
                    var tasty = 10.in100();
                    if (tasty) {
                        _ = companion.ApplyEffect(AccessTools.Method(typeof(Campfire), "RandomTastyEffect").Invoke(null, new object[] { "" }) as Effect);
                    }
                }
            }

            if (fakeTarget) {
                _ = target.Destroy(Silent: true, Obliterate: true);
            }

            var usedEvent = Event.New("UsedAsIngredient", "Actor", Leader);
            foreach (var ingredient in mealIngredients) {
                var remaining = Companions.Count;
                for (int i = 0; remaining > 0 && i < ingredient.Objects.Count; ++i) {
                    var obj = ingredient.Objects[i];
                    _ = obj.FireEvent(usedEvent);
                    if (obj.HasPart(nameof(PreparedCookingIngredient))) {
                        var part = obj.GetPart<PreparedCookingIngredient>();
                        if (part.HasTag("AlwaysStack")) {
                            // GameObject.Destroy only destroys one of Stackers
                            for (int j = 0; j < remaining; ++j) {
                                _ = obj.Destroy();
                            }
                        } else if (part.charges <= remaining) {
                            remaining -= part.charges;
                            _ = obj.Destroy();
                        } else {
                            part.charges -= remaining;
                            _ = obj.SplitStack(remaining);
                            remaining = 0;
                            obj.CheckStack();
                        }
                    } else if (obj.LiquidVolume != null) {
                        if (obj.LiquidVolume.Volume >= remaining) {
                            _ = obj.LiquidVolume.UseDrams(remaining);
                            remaining = 0;
                        } else {
                            remaining -= obj.LiquidVolume.Volume;
                            _ = obj.LiquidVolume.UseDrams(obj.LiquidVolume.Volume);
                        }
                    }
                }
            }
            return true;
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
