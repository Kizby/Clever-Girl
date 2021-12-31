namespace XRL.World.CleverGirl {
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Linq;
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
            Display = "Feed companions",
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

        public static bool DoFeed(GameObject Leader, ref int EnergyCost) {
            var options = new List<string> {
                "Feed everyone!",
                ""
            };
            var icons = new List<IRenderable> { null, null };
            var companions = Utility.CollectNearbyCompanions(Leader).Where(c => c.HasPart(nameof(Stomach))).ToList();
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

                options.Add(Campfire.EnabledDisplay(Campfire.hasSkill, "Choose ingredients to cook with."));
                actions.Add((leader, companions) => FeedFromIngredients(leader, companions, campfires[0].GetPart<Campfire>()));

                options.Add(Campfire.EnabledDisplay(Campfire.hasSkill && CookingGamestate.instance.knownRecipies.Any(r => !r.Hidden), "Cook from a recipe."));
                actions.Add((leader, companions) => FeedFromRecipe(leader, companions, campfires[0].GetPart<Campfire>()));
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

        private static List<Ingredient> CollectIngredients(GameObject Leader, List<GameObject> Companions) {
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
                    name = ingredient.LiquidVolume.GetLiquidName().Strip();
                    count = ingredient.LiquidVolume.Volume;
                } else {
                    name = ingredient.Blueprint;
                    count = ingredient.Count;
                }
                Utility.MaybeLog(name);
                if (!edibleIngredients.ContainsKey(name)) {
                    edibleIngredients[name] = new Ingredient { Name = name, Count = 0, Icon = icon, Objects = new List<GameObject>() };
                }
                edibleIngredients[name].Count += count;
                edibleIngredients[name].Objects.Add(ingredient);
            }

            var finalIngredients = edibleIngredients.Values.ToList();
            finalIngredients.Sort((a, b) => ColorUtility.CompareExceptFormattingAndCase(a.Objects[0].GetDisplayName(NoColor: true), b.Objects[0].GetDisplayName(NoColor: true)));

            return finalIngredients;
        }

        private static void UseIngredients(GameObject Leader, List<Ingredient> mealIngredients, int servings) {
            var usedEvent = Event.New("UsedAsIngredient", "Actor", Leader);
            foreach (var ingredient in mealIngredients) {
                var remaining = servings;
                for (int i = 0; remaining > 0 && i < ingredient.Objects.Count; ++i) {
                    var obj = ingredient.Objects[i];
                    _ = obj.FireEvent(usedEvent);
                    if (obj.HasPart(nameof(PreparedCookingIngredient))) {
                        var part = obj.GetPart<PreparedCookingIngredient>();
                        if (part.HasTag("AlwaysStack")) {
                            // GameObject.Destroy only destroys one of Stackers
                            var toDestroy = Math.Min(remaining, part.charges);
                            for (int j = 0; j < toDestroy; ++j) {
                                _ = obj.Destroy();
                                --remaining;
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
        }

        private static string GetCampfireDescription(CookingRecipe recipe, bool cookable, Dictionary<ICookingRecipeComponent, int> counts, int servings) {
            var description = "";
            if (recipe.Favorite) {
                description = "&R\x0003&W ";
            }
            if (!cookable) {
                description += "&K" + ColorUtility.StripFormatting(recipe.GetDisplayName());
            } else {
                description += recipe.GetDisplayName();
            }
            description += "\n&y";
            bool first = true;
            foreach (var component in recipe.Components) {
                if (first) {
                    first = false;
                } else {
                    description += "&y, ";
                }
                description += counts[component] >= servings ? "&C" : "&r";
                switch (component) {
                    case PreparedCookingRecipieComponentBlueprint blueprintComponent:
                        description += servings + "&y " + (servings > 1 ? "servings" : "serving") + " of " + blueprintComponent.ingredientDisplayName;
                        break;
                    case PreparedCookingRecipieComponentDomain domainComponent:
                        description += servings + "&y " + (servings > 1 ? "servings" : "serving") + " of " + domainComponent.ingredientType;
                        break;
                    case PreparedCookingRecipieComponentLiquid liquidComponent:
                        description += servings + "&y " + (servings > 1 ? "drams" : "dram") + " of " + LiquidVolume.getLiquid(liquidComponent.liquid).GetName();
                        break;
                    default:
                        break;
                }
                description += "&K(" + counts[component] + ")";
            }
            description += "&y";
            return description + "\n\n" + recipe.GetDescription();
        }


        private static bool FeedFromIngredients(GameObject Leader, List<GameObject> Companions, Campfire Campfire) {
            if (!Campfire.hasSkill) {
                Popup.Show("You don't have the Cooking and Gathering skill.");
                return false;
            }
            var ingredients = new List<Ingredient>();
            foreach (var ingredient in CollectIngredients(Leader, Companions)) {
                if (ingredient.Count >= Companions.Count) {
                    ingredients.Add(ingredient);
                }
            }
            var maxIngredients = Leader.HasSkill("CookingAndGathering_Spicer") ? 3 : 2;

            var options = new List<string> { "" };
            var icons = new List<IRenderable> { null };
            foreach (var ingredient in ingredients) {
                var name = ingredient.Objects[0].GetDisplayName(1120);
                if (ingredient.Objects[0].HasPart(nameof(LiquidVolume))) {
                    name = ingredient.Objects[0].LiquidVolume.GetLiquidName();
                }
                options.Add("[ ]   " + name + (Companions.Count == 1 ? "" : " x" + Companions.Count));
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
                last = index;
                if (index > 0) {
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
            for (int i = 0; i < ingredients.Count; ++i) {
                if (!options[i + 1].StartsWith(check)) {
                    continue;
                }
                var realObj = ingredients[i].Objects[0];
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
                mealIngredients.Add(ingredients[i]);
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
                    var IncrementRecipeAchievement = AccessTools.Method(typeof(Campfire), "IncrementRecipeAchievement");
                    if (IncrementRecipeAchievement != null) {
                        // pre-deep jungle
                        _ = IncrementRecipeAchievement.Invoke(null, new Type[] { });
                    } else {
                        // post-deep jungle
                        var AchievementManager = AccessTools.TypeByName("AchievementManager");
                        _ = AccessTools.Method(AchievementManager, "IncrementAchievement", new Type[] { typeof(string), typeof(int) }).Invoke(null, new object[] { "ACH_100_RECIPES", 1 });
                    }
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

            UseIngredients(Leader, mealIngredients, Companions.Count);
            return true;
        }

        private static bool FeedFromRecipe(GameObject Leader, List<GameObject> Companions, Campfire Campfire) {
            if (!Campfire.hasSkill) {
                Popup.Show("You don't have the Cooking and Gathering skill.");
                return false;
            }

            var ingredients = CollectIngredients(Leader, Companions);

            var recipes = new List<Tuple<string, CookingRecipe>>();
            var componentCounts = new Dictionary<CookingRecipe, Dictionary<ICookingRecipeComponent, int>>();
            var recipeIngredients = new Dictionary<CookingRecipe, List<Ingredient>>();
            var uncookableMessages = new Dictionary<CookingRecipe, string>();
            foreach (var knownRecipe in CookingGamestate.instance.knownRecipies) {
                if (knownRecipe.Hidden) {
                    continue;
                }

                bool cookable = true;
                recipeIngredients[knownRecipe] = new List<Ingredient>();
                componentCounts[knownRecipe] = new Dictionary<ICookingRecipeComponent, int>();
                foreach (var component in knownRecipe.Components) {
                    // consolidate ingredients across multiple inventories
                    Ingredient gestaltIngredient = null;
                    void addToGestalt(Ingredient ingredient) {
                        if (gestaltIngredient == null) {
                            gestaltIngredient = ingredient;
                        } else {
                            gestaltIngredient.Count += ingredient.Count;
                            gestaltIngredient.Objects.AddRange(ingredient.Objects);
                        }
                    }
                    switch (component) {
                        case PreparedCookingRecipieComponentBlueprint blueprintComponent:
                            var blueprints = blueprintComponent.ingredientBlueprint.Split('|').ToImmutableHashSet();
                            foreach (var ingredient in ingredients) {
                                if (blueprints.Contains(ingredient.Objects[0].Blueprint)) {
                                    addToGestalt(ingredient);
                                }
                            }
                            break;
                        case PreparedCookingRecipieComponentDomain domainComponent:
                            foreach (var ingredient in ingredients) {
                                if (ingredient.Objects[0].GetPart<PreparedCookingIngredient>()?.type == domainComponent.ingredientType) {
                                    addToGestalt(ingredient);
                                }
                            }
                            break;
                        case PreparedCookingRecipieComponentLiquid liquidComponent:
                            foreach (var ingredient in ingredients) {
                                foreach (var obj in ingredient.Objects) {
                                    var volume = obj.GetPart<LiquidVolume>();
                                    if (volume?.IsPureLiquid(liquidComponent.liquid) == true) {
                                        if (gestaltIngredient == null) {
                                            gestaltIngredient = new Ingredient {
                                                Name = ingredient.Name,
                                                Count = 0,
                                                Icon = ingredient.Icon,
                                                Objects = new List<GameObject>()
                                            };
                                        }
                                        gestaltIngredient.Count += volume.Volume;
                                        gestaltIngredient.Objects.Add(obj);
                                    }
                                }
                            }
                            break;
                        default:
                            // ???
                            break;
                    }
                    componentCounts[knownRecipe][component] = gestaltIngredient?.Count ?? 0;
                    if (gestaltIngredient == null || gestaltIngredient.Count < Companions.Count) {
                        cookable = false;
                        uncookableMessages[knownRecipe] = component.createPlayerDoesNotHaveEnoughMessage();
                    }
                    if (gestaltIngredient != null) {
                        recipeIngredients[knownRecipe].Add(gestaltIngredient);
                    }
                }
                recipes.Add(Tuple.Create(GetCampfireDescription(knownRecipe, cookable, componentCounts[knownRecipe], Companions.Count) + "\n\n", knownRecipe));
            }
            if (recipes.Count == 0) {
                Popup.Show("You don't know any recipes.");
                return false;
            }
            recipes.Sort((a, b) => ColorUtility.CompareExceptFormattingAndCase(a.Item1, b.Item1));
            var cookableRecipes = recipes.Where(t => !uncookableMessages.Keys.Contains(t.Item2)).ToList();
            cookableRecipes.Add(Tuple.Create<string, CookingRecipe>("Show " + uncookableMessages.Count + " hidden recipes missing ingredients", null));

            var showUncookable = false;
            if (uncookableMessages.Count == recipes.Count) {
                showUncookable = true;
            }

            var relevantRecipes = showUncookable ? recipes : cookableRecipes;
            var index = 0;
            CookingRecipe recipe = null;
            bool doCook;
            do {
                doCook = false;
                index = Popup.ShowOptionList("Choose a recipe",
                                             relevantRecipes.Select(t => t.Item1).ToArray(),
                                             Spacing: 1,
                                             Intro: showUncookable ? "" : "&K< " + uncookableMessages.Count + " hidden for missing ingredients >",
                                             MaxWidth: 72,
                                             RespectOptionNewlines: true,
                                             AllowEscape: true,
                                             defaultSelected: Math.Max(index, 0),
                                             SpacingText: Popup.SPACING_DARK_LINE.Replace('=', '÷'));
                if (index == -1) {
                    return false;
                }
                recipe = relevantRecipes[index].Item2;
                if (recipe == null) {
                    showUncookable = true;
                    relevantRecipes = recipes;
                    continue;
                }
                while (!doCook) {
                    var options = new List<string> {
                        "Cook",
                        recipe.Favorite ? "Remove from favorite recipes" : "Add to favorite recipes",
                        "Forget",
                        "Back",
                    };
                    var cancelled = false;
                    switch (Popup.ShowOptionList(Options: options.ToArray(),
                                                 Intro: relevantRecipes[index].Item1,
                                                 MaxWidth: 72,
                                                 RespectOptionNewlines: true,
                                                 AllowEscape: true)) {
                        case 0:
                            doCook = true;
                            break;
                        case 1:
                            recipe.Favorite = !recipe.Favorite;
                            // update the description to show it's (not) a favorite
                            var newTuple = Tuple.Create(GetCampfireDescription(recipe, !uncookableMessages.ContainsKey(recipe), componentCounts[recipe], Companions.Count), recipe);
                            for (int i = 0; i < recipes.Count; ++i) {
                                if (recipes[i].Item2 == recipe) {
                                    recipes[i] = newTuple;
                                }
                                if (i < cookableRecipes.Count && cookableRecipes[i].Item2 == recipe) {
                                    cookableRecipes[i] = newTuple;
                                }
                            }
                            continue;
                        case 2:
                            if (Popup.ShowYesNo("Are you sure you want to forget this recipe?") == DialogResult.Yes) {
                                recipe.Hidden = true;
                                _ = recipes.Remove(recipes.First(t => t.Item2 == recipe));
                                _ = cookableRecipes.Remove(cookableRecipes.First(t => t.Item2 == recipe));
                            }
                            cancelled = true;
                            break;
                        default:
                            cancelled = true;
                            break;
                    }
                    if (cancelled) {
                        break;
                    }
                }
                if (doCook && uncookableMessages.ContainsKey(recipe)) {
                    Popup.Show(uncookableMessages[recipe]);
                }
            } while (recipe == null || !doCook || uncookableMessages.ContainsKey(relevantRecipes[index].Item2));

            _ = Leader.FireEvent(Event.New("CookedAt", "Object", Campfire.ParentObject));
            var description = "";
            foreach (var companion in Companions) {
                _ = companion.FireEvent("ClearFoodEffects");
                _ = companion.CleanEffects();

                var tasty = Campfire.ForceTastyBasedOnIngredients(recipeIngredients[recipe].ConvertAll(i => i.Objects[0])) || 10.in100();
                if (tasty) {
                    _ = companion.ApplyEffect(AccessTools.Method(typeof(Campfire), "RandomTastyEffect").Invoke(null, new object[] { "" }) as Effect);
                }

                description = "";
                foreach (var effect in recipe.Effects) {
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

            UseIngredients(Leader, recipeIngredients[recipe], Companions.Count);
            return true;
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
