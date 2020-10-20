using XRL;
using XRL.World;

[PlayerMutator]
public class CleverGirlPlayerMutator : IPlayerMutator
{
    public void mutate(GameObject player)
    {
        // add our listener to the player when a New Game begins
        player.AddPart<XRL.World.Parts.CleverGirl.InteractListener>();
    }
}