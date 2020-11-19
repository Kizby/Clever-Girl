using System;

namespace XRL.World.AI.GoalHandlers
{
    [Serializable]
    public class CleverGirl_GoPickupGear : GoalHandler
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

        public CleverGirl_GoPickupGear(GameObject gear) {
            this.Gear = gear;
        }
    }
}