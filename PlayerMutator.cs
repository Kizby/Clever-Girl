using XRL;
using XRL.Core;
using XRL.World;

[PlayerMutator]
[HasCallAfterGameLoaded]
public class CleverGirlPlayerMutator : IPlayerMutator
{
    public void mutate(GameObject player)
    {
        // add our listener to the player when a New Game begins
        player.AddPart<XRL.World.Parts.CleverGirl_InteractListener>();
    }

    [CallAfterGameLoaded]
    public static void GameLoadedCallback()
    {
        // Called whenever loading a save game
        GameObject player = XRLCore.Core?.Game?.Player?.Body;
        if (player != null)
        {
            player.RequirePart<XRL.World.Parts.CleverGirl_InteractListener>();
        }
    }
}