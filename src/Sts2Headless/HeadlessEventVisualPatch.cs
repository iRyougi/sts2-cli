using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using MegaCrit.Sts2.Core.Audio.Debug;
using MegaCrit.Sts2.Core.Models.Events;
using MegaCrit.Sts2.Core.Nodes;

namespace Sts2Headless;

/// <summary>
/// Remove only event audio and screen-shake calls when the Godot scene tree is absent.
/// The native event state machines retain all healing, damage, rewards and transitions.
/// </summary>
internal static class HeadlessEventVisualPatch
{
    internal static void Install()
    {
        var harmony = new Harmony("sts2headless.event-visuals");
        foreach (var (type, methodName) in new[]
        {
            (typeof(DenseVegetation), "Rest"),
            (typeof(PunchOff), "Nab"),
            (typeof(JungleMazeAdventure), "SafetyInNumbers"),
        })
        {
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(type.FullName, methodName);
            var stateMachine = method.GetCustomAttribute<AsyncStateMachineAttribute>()?.StateMachineType
                ?? throw new InvalidOperationException($"{type.Name}.{methodName} is no longer async.");
            var moveNext = stateMachine.GetMethod("MoveNext", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new MissingMethodException(stateMachine.FullName, "MoveNext");
            harmony.Patch(moveNext, transpiler: new HarmonyMethod(typeof(HeadlessEventVisualPatch), nameof(RemoveVisualCalls)));
        }
    }

    private static IEnumerable<CodeInstruction> RemoveVisualCalls(IEnumerable<CodeInstruction> instructions)
    {
        var replacements = 0;
        foreach (var instruction in instructions)
        {
            if (instruction.operand is not MethodInfo method || !IsVisualCall(method))
            {
                yield return instruction;
                continue;
            }

            replacements++;
            var pops = method.GetParameters().Length + (method.IsStatic ? 0 : 1);
            // Reuse the original instruction for labels and exception blocks.
            instruction.opcode = OpCodes.Pop;
            instruction.operand = null;
            yield return instruction;
            for (var i = 1; i < pops; i++)
                yield return new CodeInstruction(OpCodes.Pop);
            if (method.ReturnType == typeof(int))
                yield return new CodeInstruction(OpCodes.Ldc_I4_0);
            else if (method.ReturnType != typeof(void))
                throw new InvalidOperationException($"Unexpected visual result: {method}");
        }
        if (replacements == 0)
            throw new InvalidOperationException("No expected event visual call found; native method may have changed.");
    }

    private static bool IsVisualCall(MethodInfo method) =>
        method.DeclaringType == typeof(NDebugAudioManager) && method.Name is "Play" or "Stop"
        || method.DeclaringType == typeof(NGame) && method.Name is "ScreenRumble" or "ScreenShakeTrauma";
}
