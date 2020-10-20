using XRL; // to abbreviate XRL.PlayerMutator and XRL.IPlayerMutator
using XRL.World; // to abbreviate XRL.World.GameObject

[PlayerMutator]
public class CleverGirl_PlayerMutator : IPlayerMutator
{
    public void mutate(GameObject player)
    {
        // modify the player object when a New Game begins
        // for example, add a custom part to the player:
        player.AddPart<XRL.World.Parts.CleverGirl_InteractListener>();
    }
}