using HarmonyLib;
using Road.StateMachines;
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