using System;

namespace XRL.World.AI.GoalHandlers.CleverGirl
{
    [Serializable]
    public class GoPickupGear : GoalHandler
    {
        public readonly GameObject Gear;

        public override bool Finished() => false;

        public override void TakeAction() {
            Pop();
            var currentCell = ParentBrain.pPhysics.CurrentCell;
            if (null == currentCell) {
                return;
            }
            if (currentCell != Gear.CurrentCell) {
                return;
            }
            if (Gear.IsTakeable()) {
                ParentBrain.ParentObject.TakeObject(Gear);
                ParentBrain.PerformReequip();
            }
        }

        public GoPickupGear(GameObject gear) {
            this.Gear = gear;
        }
    }
}