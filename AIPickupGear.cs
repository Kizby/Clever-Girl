using System;
using System.Collections.Generic;

namespace XRL.World.Parts.CleverGirl
{
    using System.Linq;
    using AI.GoalHandlers.CleverGirl;
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

            var parts = ParentObject.Body.GetParts();
            var currentCell = ParentObject.CurrentCell;

            // total carry capacity if we dropped everything in our inventory
            var capacity = Stats.GetMaxWeight(ParentObject) - ParentObject.Body.GetWeight();

            // if we're already overburdened by just our equipment, nothing to do
            if (capacity < 0) {
                Utility.MaybeLog("Overburdened by " + capacity);
                return;
            }

            if (findBetterPrimaryWeapon(parts, currentCell, capacity)) {
                return;
            }
            if (findBetterArmor(parts, currentCell, capacity)) {
                return;
            }
            if (findBetterAlternateWeapon(parts, currentCell, capacity)) {
                return;
            }
        }

        private bool findBetterPrimaryWeapon(List<BodyPart> parts, Cell currentCell, int capacity) {
            var weapons = currentCell.ParentZone
                .FastFloodVisibility(currentCell.X, currentCell.Y, 30, "MeleeWeapon", ParentObject)
                .Where(go => go.HasTag("MeleeWeapon") && ParentObject.HasLOSTo(go))
                .ToList();
            if (0 == weapons.Count) {
                Utility.MaybeLog("No weapons");
                return false;
            }
            
            if (weapons.Count > 1) {
                weapons.Sort(new Brain.WeaponSorter(ParentObject));
            }

            var primaryPart = parts.Find(part => part.Primary);
            var equipped = primaryPart?.Equipped;
            if (null != equipped && !equipped.FireEvent("CanBeUnequipped")) {
                Utility.MaybeLog("Can't unequip my primary weapon");
                return false;
            }

            var noEquip = ParentObject.GetPropertyOrTag("NoEquip");
            var noEquipList = string.IsNullOrEmpty(noEquip) ? null : new List<string>(noEquip.CachedCommaExpansion());

            GameObject betterWeapon = null;
            foreach (var weapon in weapons) {
                if (noEquipList?.Contains(weapon.Blueprint) ?? false) {
                    continue;
                }
                if (weapon.HasPropertyOrTag("NoAIEquip")) {
                    continue;
                }
                if (weapon.WeightEach - (equipped?.Weight ?? 0) > capacity) {
                    Utility.MaybeLog("No way to equip " + weapon.DisplayNameOnlyStripped + " without being overburdened");
                    continue;
                }
                betterWeapon = ParentObject.pBrain.IsNewWeaponBetter(weapon, equipped) ? weapon : null;
                break;
            }

            if (null == betterWeapon) {
                return false;
            }

            GoGet(betterWeapon);
            return true;
        }
        private bool findBetterArmor(List<BodyPart> parts, Cell currentCell, int capacity) {
            var armors = currentCell.ParentZone
                .FastFloodVisibility(currentCell.X, currentCell.Y, 30, "Armor", ParentObject)
                .Where(go => ParentObject.HasLOSTo(go))
                .ToList();
            if (0 == armors.Count) {
                Utility.MaybeLog("No armors");
                return false;
            }
            
            if (armors.Count > 1) {
                armors.Sort(new Brain.GearSorter(ParentObject));
            }

            var noEquip = ParentObject.GetPropertyOrTag("NoEquip");
            var noEquipList = string.IsNullOrEmpty(noEquip) ? null : new List<string>(noEquip.CachedCommaExpansion());

            GameObject betterArmor = null;
            foreach (var armor in armors) {
                if (noEquipList?.Contains(armor.Blueprint) ?? false) {
                    continue;
                }
                if (armor.HasPropertyOrTag("NoAIEquip")) {
                    continue;
                }
                foreach (var part in parts) {
                    if (armor.GetPart<Armor>().WornOn != part.Type) {
                        continue;
                    }
                    if (armor.WeightEach - (part.Equipped?.Weight ?? 0) > capacity) {
                        Utility.MaybeLog("No way to equip " + armor.DisplayNameOnlyStripped + " on " + part.Name + " without being overburdened");
                        continue;
                    }
                    if (ParentObject.pBrain.IsNewArmorBetter(armor, part.Equipped)) {
                        betterArmor = armor;
                        break;
                    }
                }
                if (null != betterArmor) {
                    break;
                }
            }

            if (null == betterArmor) {
                return false;
            }

            GoGet(betterArmor);
            return true;
        }
        private bool findBetterAlternateWeapon(List<BodyPart> parts, Cell currentCell, int capacity) {
            var weapons = currentCell.ParentZone
                .FastFloodVisibility(currentCell.X, currentCell.Y, 30, "MeleeWeapon", ParentObject)
                .Where(go => go.HasTag("MeleeWeapon") && ParentObject.HasLOSTo(go))
                .ToList();
            if (0 == weapons.Count) {
                return false;
            }
            
            if (weapons.Count > 1) {
                weapons.Sort(new Brain.WeaponSorter(ParentObject));
            }

            var noEquip = ParentObject.GetPropertyOrTag("NoEquip");
            var noEquipList = string.IsNullOrEmpty(noEquip) ? null : new List<string>(noEquip.CachedCommaExpansion());

            GameObject betterWeapon = null;
            foreach (var weapon in weapons) {
                if (noEquipList?.Contains(weapon.Blueprint) ?? false) {
                    continue;
                }
                if (weapon.HasPropertyOrTag("NoAIEquip")) {
                    continue;
                }
                foreach (var part in parts) {
                    if ("Hand" != part.Type) {
                        continue;
                    }
                    if (weapon.WeightEach - (part.Equipped?.Weight ?? 0) > capacity) {
                        Utility.MaybeLog("No way to equip " + weapon.DisplayNameOnlyStripped + " on " + part.Name + " without being overburdened");
                        continue;
                    }
                    if (ParentObject.pBrain.IsNewWeaponBetter(weapon, part.Equipped)) {
                        betterWeapon = weapon;
                        break;
                    }
                }
                if (null != betterWeapon) {
                    break;
                }
            }

            if (null == betterWeapon) {
                return false;
            }

            GoGet(betterWeapon);
            return true;
        }

        void GoGet(GameObject item) {
            ParentObject.pBrain.Think("I want that " + item.DisplayNameOnlyStripped);
            ParentObject.pBrain.PushGoal(new GoPickupGear(item));
            ParentObject.pBrain.PushGoal(new MoveTo(item.CurrentCell));
        }
    }
}