using Bang.Entities;
using Bang.StateMachines;
using HarmonyLib;
using Murder.Components;
using Murder.Services;
using Road.StateMachines;
using System.Reflection;
using System.Reflection.Emit;
using Wayfinder.Core;

public class ModEntry
{
    public static void Start()
    {
        try
        {
            var harmony = new Harmony("com.echoviax.doorunlocker");
            harmony.PatchAll();
            LoaderCore.LogSuccess("Successfully injected.");
        }
        catch (Exception ex)
        {
            LoaderCore.LogError("Failed to inject: " + ex);
        }
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