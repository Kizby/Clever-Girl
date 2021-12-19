namespace XRL.World.Parts {
    using System;
    using XRL.World.CleverGirl;
    using Qud.API;
    using System.Linq;

    [Serializable]
    public class CleverGirl_AIUnburden : CleverGirl_INoSavePart {

        public static string PROPERTY => "CleverGirl_AIUnburden";
        public override void Register(GameObject Object) {
            _ = Object.SetIntProperty(PROPERTY, 1);
        }
        public override void Remove() {
            ParentObject.RemoveIntProperty(PROPERTY);
        }

        public override bool WantTurnTick() => true;

        public override void TurnTick(long TurnNumber) {
            var excess = ParentObject.Body.GetWeight() + ParentObject.Inventory.GetWeight() - ParentObject.GetMaxCarriedWeight();
            if (excess <= 0) {
                return;
            }

            Utility.MaybeLog("Overburdened by " + excess + "#");
            var objects = ParentObject.Inventory.GetObjects(obj => obj.WeightEach > 0);
            if (objects.Count == 0) {
                // nothing to drop that would improve anything
                return;
            }

            // by default, drop heaviest things first
            Func<GameObject, double> metric = obj => obj.WeightEach;
            // if they're smart though, use weight/value metric
            if (ParentObject.GetStatValue("Intelligence") >= 16) {
                // add a fudge factor so we don't have to compare infinities
                metric = obj => obj.WeightEach / (obj.ValueEach + 0.01);
            }

            foreach (var obj in objects.OrderByDescending(metric)) {
                // how many would we need to drop to fix the whole excess?
                var toDrop = ((excess - 1) / obj.WeightEach) + 1; // ceil(excess / obj.WeightEach)
                if (toDrop < obj.Count) {
                    // only drop what we need to
                    _ = obj.SplitStack(toDrop, ParentObject);
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
