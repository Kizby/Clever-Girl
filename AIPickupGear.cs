using System;
using System.Collections.Generic;

namespace XRL.World.Parts.CleverGirl
{
    using System.Linq;
    using AI.GoalHandlers.CleverGirl;
    using Qud.API;
    using XRL.Rules;
    using XRL.World.AI.GoalHandlers;
    using XRL.World.CleverGirl;

    [Serializable]
    public class AIPickupGear : IPart {
        public const string ENABLE_COMMAND = "CleverGirl_EnableGearPickup";
        public const string DISABLE_COMMAND = "CleverGirl_DisableGearPickup";

        public bool Enabled = false;
        public string ActionName => Enabled ? "Clever Girl - Disable Gear Pickup" : "Clever Girl - Enable Gear Pickup";
        public string ActionDisplay => Enabled ? "disable gear {{inventoryhotkey|p}}ickup" : "enable gear {{inventoryhotkey|p}}ickup";
        public string ActionCommand => Enabled ? DISABLE_COMMAND : ENABLE_COMMAND;
        public char ActionKey => 'p';

        public override bool WantTurnTick() => Enabled;

        public override void TurnTick(long TurnNumber)
        {
            if (ParentObject.IsBusy()) {
                return;
            }
            
            if (ParentObject.IsPlayer()) {
                return;
            }

            Utility.MaybeLog("Turn " + TurnNumber);

            // Primary weapon
            if (findBetterThing("MeleeWeapon",
                                go => go.HasTag("MeleeWeapon"),
                                new Brain.WeaponSorter(ParentObject),
                                (part, thing) => part.Primary && part.Type == thing.GetPart<MeleeWeapon>().Slot)) {
                return;
            }

            var currentShield = ParentObject.Body.GetShield();
            if (ParentObject.HasSkill("Shield")) {
                Utility.MaybeLog("Considering shields");
                // manually compare to our current best shield since the WornOn's might not match
                if (findBetterThing("Shield",
                                    go => go.HasTag("Shield") && Brain.CompareShields(go, currentShield, ParentObject) < 0,
                                    new ShieldSorter(ParentObject),
                                    (part, thing) => !part.Primary && part.Type == thing.GetPart<Shield>().WornOn)) {
                    // hack because the game's reequip logic doesn't consider better shields
                    if (null != currentShield) {
                        if (currentShield.TryUnequip()) {
                            EquipmentAPI.DropObject(currentShield);
                        };
                    }
                    return;
                }
            }

            // Armor
            if (findBetterThing("Armor",
                                _ => true,
                                new Brain.GearSorter(ParentObject),
                                (part, thing) => part.Type == thing.GetPart<Armor>().WornOn)) {
                return;
            }

            // Additional weapons
            if (findBetterThing("MeleeWeapon",
                                go => go.HasTag("MeleeWeapon"),
                                new Brain.WeaponSorter(ParentObject),
                                (part, thing) => !part.Primary &&
                                                 part.Equipped != currentShield &&
                                                 part.Type == thing.GetPart<MeleeWeapon>().Slot)) {
                return;
            }
        }

        private bool findBetterThing(string SearchPart,
                                     Func<GameObject, bool> whichThings,
                                     Comparer<GameObject> thingComparer,
                                     Func<BodyPart, GameObject, bool> whichBodyParts) {
            var allBodyParts = ParentObject.Body.GetParts();
            var currentCell = ParentObject.CurrentCell;

            // total carry capacity if we dropped everything in our inventory
            var capacity = Stats.GetMaxWeight(ParentObject) - ParentObject.Body.GetWeight();
            // if we're already overburdened by just our equipment, nothing to do
            if (capacity < 0) {
                return false;
            }

            var things =  currentCell.ParentZone
                .FastFloodVisibility(currentCell.X, currentCell.Y, 30, SearchPart, ParentObject)
                .Where(whichThings)
                .Where(go => ParentObject.HasLOSTo(go))
                .ToList();
            if (0 == things.Count) {
                Utility.MaybeLog("No " + SearchPart + "s");
                return false;
            }

            things.Sort(thingComparer);

            var noEquip = ParentObject.GetPropertyOrTag("NoEquip");
            var noEquipList = string.IsNullOrEmpty(noEquip) ? null : new List<string>(noEquip.CachedCommaExpansion());

            foreach (var thing in things) {
                if (noEquipList?.Contains(thing.Blueprint) ?? false) {
                    continue;
                }
                if (thing.HasPropertyOrTag("NoAIEquip")) {
                    continue;
                }
                foreach (var bodyPart in allBodyParts) {
                    if (!whichBodyParts(bodyPart, thing)) {
                        continue;
                    }
                    if (!(bodyPart.Equipped?.FireEvent("CanBeUnequipped") ?? true)) {
                        Utility.MaybeLog("Can't unequip the " + bodyPart.Equipped.DisplayNameOnlyStripped);
                        continue;
                    }
                    if (thing.WeightEach - (bodyPart.Equipped?.Weight ?? 0) > capacity) {
                        Utility.MaybeLog("No way to equip " + thing.DisplayNameOnlyStripped + " on " + bodyPart.Name + " without being overburdened");
                        continue;
                    }
                    if (thingComparer.Compare(thing, bodyPart.Equipped) < 0) {
                        GoGet(thing);
                        return true;
                    }
                }
            }
            return false;
        }

        void GoGet(GameObject item) {
            ParentObject.pBrain.Think("I want that " + item.DisplayNameOnlyStripped);
            ParentObject.pBrain.PushGoal(new GoPickupGear(item));
            ParentObject.pBrain.PushGoal(new MoveTo(item.CurrentCell));
        }

        // why doesn't Brain have this? 😭
        class ShieldSorter : Comparer<GameObject> {
            private GameObject POV;
            private bool Reverse;

            public ShieldSorter(GameObject POV) => this.POV = POV;

            public ShieldSorter(GameObject POV, bool Reverse)
                : this(POV)
                => this.Reverse = Reverse;

            public override int Compare(GameObject o1, GameObject o2) => Brain.CompareShields(o1, o2, this.POV) * (this.Reverse ? -1 : 1);
        }
    }
}