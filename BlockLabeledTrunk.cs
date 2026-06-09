using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace labeledtrunk;

public class BlockLabeledTrunk : BlockGenericTypedContainerTrunk
{
    WorldInteraction[]? interactions;

    public override void OnLoaded(ICoreAPI api)
    {
        base.OnLoaded(api);
        PlacedPriorityInteract = true;

        if (api.Side != EnumAppSide.Client) return;

        interactions = ObjectCacheUtil.GetOrCreate(api, "labeledTrunkSignInteractions", () =>
        {
            var stacks = new List<ItemStack>();
            foreach (var collectible in api.World.Collectibles)
            {
                if (collectible.Attributes?["pigment"].Exists == true)
                    stacks.Add(new ItemStack(collectible));
            }
            return new WorldInteraction[]
            {
                new()
                {
                    ActionLangCode = "blockhelp-sign-write",
                    HotKeyCode = "shift",
                    MouseButton = EnumMouseButton.Right,
                    Itemstacks = stacks.ToArray()
                }
            };
        });
    }

    public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer)
        => interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
}
