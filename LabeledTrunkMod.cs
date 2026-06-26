using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace labeledtrunk;

public class LabeledTrunkMod : ModSystem
{
    Harmony? harmony;

    public override void Start(ICoreAPI api)
    {
        api.RegisterBlockClass("BlockLabeledTrunk", typeof(BlockLabeledTrunk));
        api.RegisterBlockEntityClass("LabeledGenericTypedContainer", typeof(BlockEntityLabeledGenericTypedContainer));

        if (!Harmony.HasAnyPatches(Mod.Info.ModID))
        {
            harmony = new Harmony(Mod.Info.ModID);
            harmony.PatchAll();
        }
    }

    public override void Dispose()
    {
        harmony?.UnpatchAll(Mod.Info.ModID);
    }
}

[HarmonyPatch(typeof(ChestLabelRenderer), nameof(ChestLabelRenderer.OnRenderFrame))]
public class ChestLabelRendererPatch
{
    // Only intercept when the block at this position is a labeled trunk.
    // For everything else (vanilla labeled chests, etc.) let the original method run.
    // When loadedTexture is null we also fall through so vanilla's lazy RenderText() path runs first.
    static bool Prefix(ChestLabelRenderer __instance, float deltaTime, EnumRenderStage stage)
    {
        var t    = Traverse.Create(__instance);
        var capi = t.Field("api").GetValue() as ICoreClientAPI;
        var pos  = t.Field("pos").GetValue() as BlockPos;

        if (capi == null || pos == null) return true;

        var block = capi.World.BlockAccessor.GetBlock(pos);
        if (block == null || !block.Code.Path.Contains("labeledtrunk")) return true;

        if (stage != EnumRenderStage.Opaque) return false;

        // Texture not yet generated — fall through so vanilla generates it
        var loadedTexture = t.Field("loadedTexture").GetValue() as LoadedTexture;
        if (loadedTexture == null) return true;

        var modelMat = t.Field("ModelMat").GetValue() as Matrixf;
        var quadRef  = t.Field("quadModelRef").GetValue() as MeshRef;
        float rotY   = (float)t.Field("rotY").GetValue();
        float quadW  = (float)t.Field("QuadWidth").GetValue();
        float quadH  = (float)t.Field("QuadHeight").GetValue();

        if (modelMat == null || quadRef == null) return true;

        var camPos = capi.World.Player.Entity.CameraPos;
        if (camPos.SquareDistanceTo(pos.X, pos.Y, pos.Z) > 400f) return false;

        float slabOffset = 0f;
        var blockBelow = capi.World.BlockAccessor.GetBlock(pos.AddCopy(0, -1, 0));
        if (blockBelow?.Shape?.Base?.Path?.Contains("slab-down") == true)
            slabOffset = -0.5f;

        var rpi = capi.Render;
        rpi.GlDisableCullFace();
        rpi.GlToggleBlend(true, EnumBlendMode.PremultipliedAlpha);

        var prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);
        prog.RgbaLightIn      = capi.World.BlockAccessor.GetLightRGBs(pos);
        prog.Tex2D            = loadedTexture.TextureId;
        prog.NormalShaded     = 0;
        prog.ExtraGodray      = 0f;
        prog.SsaoAttn         = 0f;
        prog.AlphaTest        = 0.05f;
        prog.OverlayOpacity   = 0f;
        prog.AddRenderFlags   = 0;
        prog.ModelMatrix = modelMat
            .Identity()
            .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
            .Translate(0.5f, 0.5f, 0.5f)
            .RotateY(rotY + GameMath.PI)
            .Translate(-0.5, -0.5, -0.5)
            // trunk sign panel: x shifted -0.5 relative to a regular chest
            .Translate(0.0f, 0.35f + slabOffset, 0.0925f)
            .Scale(0.45f * quadW, 0.45f * quadH, 0.45f * quadW)
            .Values;
        prog.ViewMatrix       = rpi.CameraMatrixOriginf;
        prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

        rpi.RenderMesh(quadRef);
        ((IShaderProgram)prog).Stop();
        rpi.GlToggleBlend(true, EnumBlendMode.Standard);

        return false;
    }
}

[HarmonyPatch(typeof(ItemPileable), nameof(ItemPileable.OnHeldInteractStart))]
public class ItemPileablePatch
{
    // Prevent piling items onto labeled chests, signs, and other interactive blocks.
    static bool Prefix(ItemPileable __instance, ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
    {
        if (!__instance.IsPileable) return false;
        if (blockSel == null || byEntity?.World == null || !byEntity.Controls.ShiftKey) return false;

        if (byEntity is not EntityPlayer ep) return false;
        var player = byEntity.World.PlayerByUid(ep.PlayerUID);
        if (player == null) return false;

        var pos = blockSel.Position;
        if (!byEntity.World.Claims.TryAccess(player, pos, EnumBlockAccessFlags.Use))
        {
            byEntity.World.BlockAccessor.MarkBlockEntityDirty(pos.AddCopy(blockSel.Face));
            byEntity.World.BlockAccessor.MarkBlockDirty(pos.AddCopy(blockSel.Face));
            return false;
        }

        var block = byEntity.World.BlockAccessor.GetBlock(pos);
        var be    = byEntity.World.BlockAccessor.GetBlockEntity(pos);

        // Resolve multiblock to main position
        if (block is BlockMultiblock mb)
            be = byEntity.World.BlockAccessor.GetBlockEntity(pos.AddCopy(mb.OffsetInv));

        if (be is BlockEntityLabeledChest
            || be is BlockEntitySignPost
            || be is BlockEntitySign
            || be is BlockEntityBloomery
            || be is BlockEntityFirepit
            || be is BlockEntityForge
            || be is BlockEntityCrate
            || block.HasBehavior<BlockBehaviorJonasGasifier>())
            return false;

        return true;
    }
}
