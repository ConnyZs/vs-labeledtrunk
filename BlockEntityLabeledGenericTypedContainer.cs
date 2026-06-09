using Vintagestory.API.Config;
using Vintagestory.GameContent;

namespace labeledtrunk;

// Inherits all label rendering from BlockEntityLabeledChest.
// Only override: use the trunk's dialog title lang code instead of defaulting to "Chest Contents".
public class BlockEntityLabeledGenericTypedContainer : BlockEntityLabeledChest
{
    public override string DialogTitle
    {
        get
        {
            var baseTitle = base.DialogTitle;
            if (baseTitle == Lang.Get("Chest Contents"))
                return Lang.Get(dialogTitleLangCode);
            return baseTitle;
        }
    }
}
