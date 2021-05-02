namespace XRL.World.CleverGirl {
    public class WaitUntilPartyHealedEvent : MinEvent {
        public static new readonly int ID = AllocateID();

        public WaitUntilPartyHealedEvent() {
            base.ID = ID;
        }

        /// <summary>
        /// This is necessary for custom events that need the HandleEvent to be reflected.
        /// </summary>
        public override bool WantInvokeDispatch() {
            return true;
        }

        /// <summary>
        /// A static helper method to fire our event on a GameObject.
        /// If it's a high-frequency event this should implement event pooling, rather than create a new MyCustomMinEvent each time.
        /// </summary>
        public static void Send(GameObject Object) {
            if (Object.WantEvent(ID, CascadeLevel)) {
                _ = Object.HandleEvent(new WaitUntilPartyHealedEvent());
            }
        }
    }
}
