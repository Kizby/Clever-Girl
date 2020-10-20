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

            ParentObject.pBrain.Think("I want that " + betterWeapon.DisplayNameOnlyStripped);
            ParentObject.pBrain.PushGoal(new GoPickupGear(betterWeapon));
            ParentObject.pBrain.PushGoal(new MoveTo(betterWeapon.CurrentCell));
            return true;
        }
    }
}