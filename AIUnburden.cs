using System;

namespace XRL.World.Parts.CleverGirl
{
    using XRL.Rules;
    using XRL.World.CleverGirl;
    using Qud.API;
    using System.Linq;

    [Serializable]
    public class AIUnburden : IPart {
        public bool Enabled = false;
        public override bool WantTurnTick() => Enabled;

        public override void TurnTick(long TurnNumber)
        {
            var excess = ParentObject.Body.GetWeight() + ParentObject.Inventory.GetWeight() - Stats.GetMaxWeight(ParentObject);
            if (excess <= 0) {
                return;
            }

            Utility.MaybeLog("Overburdened by " + excess + "#");
            var objects = ParentObject.Inventory.GetObjects(obj => obj.WeightEach > 0);
            if (0 == objects.Count) {
                // nothing to drop that would improve anything
                return;
            }

            // no consideration of value here, just drop heaviest things first
            foreach (var obj in objects.OrderByDescending(obj => obj.WeightEach)) {
                // how many would we need to drop to fix the whole excess?
                var toDrop = (excess - 1) / obj.WeightEach + 1; // ceil(excess / obj.WeightEach)
                if (toDrop < obj.Count) {
                    // only drop what we need to
                    obj.SplitStack(toDrop, ParentObject);
                } // else drop the whole stack

                excess -= obj.Weight;
                EquipmentAPI.DropObject(obj);
                if (excess <= 0) {
                    // done
                    break;
                }
            }
        }
    }
}