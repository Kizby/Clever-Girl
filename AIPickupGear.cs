using System;
using System.Collections.Generic;

namespace XRL.World.Parts.CleverGirl
{
    using System.Linq;
    using AI.GoalHandlers.CleverGirl;
    using XRL.World.AI.GoalHandlers;

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

            var parts = ParentObject.Body.GetParts();
            var currentCell = ParentObject.CurrentCell;
            if (findBetterPrimaryWeapon(parts, currentCell)) {
                return;
            }
            if (findBetterArmor(parts, currentCell)) {
                return;
            }
            if (findBetterAlternateWeapon(parts, currentCell)) {
                return;
            }
        }

        private bool findBetterPrimaryWeapon(List<BodyPart> parts, Cell currentCell) {
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

            var primaryPart = parts.Find(part => part.Primary);
            var equipped = primaryPart?.Equipped;
            if (null != equipped && !equipped.FireEvent("CanBeUnequipped")) {
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
                betterWeapon = ParentObject.pBrain.IsNewWeaponBetter(weapon, equipped) ? weapon : null;
                break;
            }

            if (null == betterWeapon) {
                return false;
            }

            GoGet(betterWeapon);
            return true;
        }
        private bool findBetterArmor(List<BodyPart> parts, Cell currentCell) {
            var armors = currentCell.ParentZone
                .FastFloodVisibility(currentCell.X, currentCell.Y, 30, "Armor", ParentObject)
                .Where(go => ParentObject.HasLOSTo(go))
                .ToList();
            if (0 == armors.Count) {
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
        private bool findBetterAlternateWeapon(List<BodyPart> parts, Cell currentCell) {
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