namespace CleverGirl {
    using XRL;
    using XRL.Core;
    using XRL.World;
    using XRL.World.Parts;

    [PlayerMutator]
    [HasCallAfterGameLoaded]
    public class CleverGirlPlayerMutator : IPlayerMutator {
        public void mutate(GameObject player) {
            // add our listener to the player when a New Game begins
            _ = player.AddPart<CleverGirl_EventListener>();
        }

        [CallAfterGameLoaded]
        public static void GameLoadedCallback() {
            // Called whenever loading a save game
            var player = XRLCore.Core?.Game?.Player?.Body;
            _ = (player?.RequirePart<CleverGirl_EventListener>());
        }
    }
}
