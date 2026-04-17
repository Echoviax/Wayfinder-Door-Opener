using Bang.StateMachines;
using HarmonyLib;
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

[HarmonyPatch(typeof(NpcDoorStateMachine))]
[HarmonyPatch("Closed", MethodType.Enumerator)]
public static class NPCDoor_Patch
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var openPendingField = AccessTools.Field(typeof(NpcDoorStateMachine), "_openPending");
        bool foundAndReplaced = false;

        foreach (var instruction in instructions)
        {
            if (instruction.LoadsField(openPendingField))
            {
                yield return new CodeInstruction(OpCodes.Pop);
                yield return new CodeInstruction(OpCodes.Ldc_I4_1);
                foundAndReplaced = true;
            }
            else
            {
                yield return instruction;
            }
        }

        if (!foundAndReplaced)
            LoaderCore.LogError("Could not find the NPCDoorStateMachine _openPending check.");
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
        return false;
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