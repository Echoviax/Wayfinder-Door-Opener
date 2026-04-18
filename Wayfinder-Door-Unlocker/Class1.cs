using Bang.Entities;
using Bang.StateMachines;
using HarmonyLib;
using Murder.Components;
using Murder.Services;
using Road.Components;
using Road.StateMachines;
using System.Reflection;
using Wayfinder.Core;
using Wayfinder.API;

public class ModEntry : IWayfinderMod
{
    public string Name => "Door Unlocker";
    public string Description => "Forces a lot of doors to be open always";
    public string Version => "1.1.0";
    public string Author => "Echoviax";

    private Harmony _harmony;

    public void Start()
    {
        try
        {
            _harmony = new Harmony("com.echoviax.dooropener");
            _harmony.PatchAll();
        }
        catch (Exception ex)
        {
            LoaderCore.LogError("Failed to inject: " + ex);
        }
        
    }

    public void Stop()
    {
        _harmony?.UnpatchAll(_harmony.Id);
    }
}

[HarmonyPatch]
public static class AllNpcDoorsOpen_Patch
{
    private static readonly MethodInfo AddPassageMethod = AccessTools.Method(typeof(NpcDoorStateMachine), "AddPassageWhenOpened");
    private static readonly FieldInfo SetToFloorField = AccessTools.Field(typeof(NpcDoorStateMachine), "_setToFloorWhenOpened");

    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(NpcDoorStateMachine), nameof(NpcDoorStateMachine.Start));
        yield return AccessTools.Method(typeof(NpcDoorStateMachine), nameof(NpcDoorStateMachine.Closed));
        yield return AccessTools.Method(typeof(NpcDoorStateMachine), nameof(NpcDoorStateMachine.Close));
        yield return AccessTools.Method(typeof(NpcDoorStateMachine), nameof(NpcDoorStateMachine.Open));
        yield return AccessTools.Method(typeof(NpcDoorStateMachine), nameof(NpcDoorStateMachine.Opened));
    }

    static bool Prefix(NpcDoorStateMachine __instance, ref IEnumerator<Wait> __result)
    {
        __result = ForceNpcOpenForever(__instance);
        return false;
    }

    static IEnumerator<Wait> ForceNpcOpenForever(NpcDoorStateMachine instance)
    {
        Entity entity = Traverse.Create(instance).Property("Entity").GetValue<Entity>();

        if (entity != null)
        {
            AddPassageMethod?.Invoke(instance, null);
            
            SpriteComponent? spriteComponent = entity.PlaySpriteAnimation("opened");
            bool setToFloor = (bool)(SetToFloorField?.GetValue(instance) ?? true);
            
            if (setToFloor && spriteComponent.HasValue)
                spriteComponent = spriteComponent.Value.SetBatch(1);

            if (spriteComponent.HasValue)
                entity.SetSprite(spriteComponent.Value);
        }

        yield return Wait.Stop;
    }
}

// ughhh going insane
[HarmonyPatch]
public static class AllDoorsOpen_Patch
{
    private static readonly FieldInfo ClosedField = AccessTools.Field(typeof(DoorStateMachine), "_closed");
    private static readonly MethodInfo AddPassageMethod = AccessTools.Method(typeof(DoorStateMachine), "AddPassageWhenOpened");
    private static readonly MethodInfo PlayAnimationsMethod = AccessTools.Method(typeof(DoorStateMachine), "PlayDoorAnimations");

    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(DoorStateMachine), nameof(DoorStateMachine.Start));
        yield return AccessTools.Method(typeof(DoorStateMachine), nameof(DoorStateMachine.Closed));
        yield return AccessTools.Method(typeof(DoorStateMachine), nameof(DoorStateMachine.Close));
    }

    static bool Prefix(DoorStateMachine __instance, ref IEnumerator<Wait> __result)
    {
        __result = ForceOpenForever(__instance);
        return false; // Don't call original
    }

    static IEnumerator<Wait> ForceOpenForever(DoorStateMachine instance)
    {
        ClosedField?.SetValue(instance, false);
        AddPassageMethod?.Invoke(instance, null);
        PlayAnimationsMethod?.Invoke(instance, new object[] { true, new string[] { "opened" } });

        // Don't call original
        yield return Wait.Stop;
    }
}
[HarmonyPatch]
public static class AllBuildingDoorsOpen_Patch
{
    private static readonly FieldInfo ClosedField = AccessTools.Field(typeof(BuildingDoorStateMachine), "_closed");

    static IEnumerable<MethodBase> TargetMethods()
    {
        yield return AccessTools.Method(typeof(BuildingDoorStateMachine), nameof(BuildingDoorStateMachine.WaitUntilOpened));
        yield return AccessTools.Method(typeof(BuildingDoorStateMachine), nameof(BuildingDoorStateMachine.Closed));
        yield return AccessTools.Method(typeof(BuildingDoorStateMachine), nameof(BuildingDoorStateMachine.Close));
        yield return AccessTools.Method(typeof(BuildingDoorStateMachine), nameof(BuildingDoorStateMachine.Open));
        yield return AccessTools.Method(typeof(BuildingDoorStateMachine), nameof(BuildingDoorStateMachine.Opened));
    }

    static bool Prefix(BuildingDoorStateMachine __instance, ref IEnumerator<Wait> __result)
    {
        __result = ForceBuildingOpenForever(__instance);
        return false;
    }

    static IEnumerator<Wait> ForceBuildingOpenForever(BuildingDoorStateMachine instance)
    {
        Entity entity = Traverse.Create(instance).Property("Entity").GetValue<Entity>();

        if (entity != null)
        {
            ClosedField?.SetValue(instance, false);
            entity.SetBuildingStatus(BuildingStatus.Opened);

            SpriteComponent? spriteComponent = entity.PlaySpriteAnimation("opened");

            if (instance.Flags.HasFlag(DoorFlags.SetToFloorWhenOpened) && spriteComponent.HasValue)
            {
                spriteComponent = spriteComponent.Value.SetBatch(1);
            }

            if (spriteComponent.HasValue)
            {
                entity.SetSprite(spriteComponent.Value);
            }

            ColliderComponent? colliderComponent = entity.TryGetComponent<ColliderComponent>();
            if (colliderComponent.HasValue)
            {
                ColliderComponent valueOrDefault = colliderComponent.GetValueOrDefault();
                entity.SetCollider(valueOrDefault.SetLayer(0));
            }

            entity.TryFetchChild("Sign")?.Deactivate();
            entity.TryFetchChild("Block")?.Deactivate();
            entity.TryFetchChild("Npc passage")?.Deactivate();
            entity.TryFetchChild("Close on player")?.Deactivate();
        }

        yield return Wait.Stop;
    }
}