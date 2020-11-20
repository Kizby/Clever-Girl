namespace CleverGirl {
    using XRL;
    using XRL.Core;
    using XRL.World;

    [PlayerMutator]
    [HasCallAfterGameLoaded]
    public class CleverGirlPlayerMutator : IPlayerMutator {
        public void mutate(GameObject player) {
            // add our listener to the player when a New Game begins
            _ = player.AddPart<XRL.World.Parts.CleverGirl_InteractListener>();
        }

        [CallAfterGameLoaded]
        public static void GameLoadedCallback() {
            // Called whenever loading a save game
            var player = XRLCore.Core?.Game?.Player?.Body;
            _ = (player?.RequirePart<XRL.World.Parts.CleverGirl_InteractListener>());
        }
    }
}
