using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using MegaCrit.Sts2.Core.Models.Relics;
using MegaCrit.Sts2.Core.TestSupport;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Entities.Merchant;
using MegaCrit.Sts2.Core.Rooms;

namespace Sts2Headless;

/// <summary>Keep native rewards, merchant pricing and history capture on its production branch while headless UI uses TestMode.</summary>
internal static class NativeRewardGenerationPatch
{
    internal static void Install()
    {
        var harmony = new Harmony("sts2headless.native-rewards");
        foreach (var (type, methodName) in new[] { (typeof(CallingBell), "GenerateRewards"), (typeof(Cauldron), "GenerateRewards"), (typeof(RunManager), "UpdatePlayerStatsInMapPointHistory"), (typeof(MerchantPotionEntry), "CalcCost"), (typeof(MerchantRoom), "Exit") })
        {
            var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                ?? throw new MissingMethodException(type.FullName, methodName);
            harmony.Patch(method, transpiler: new HarmonyMethod(typeof(NativeRewardGenerationPatch), nameof(ProductionBranch)));
        }
    }

    private static IEnumerable<CodeInstruction> ProductionBranch(IEnumerable<CodeInstruction> instructions)
    {
        var getter = AccessTools.PropertyGetter(typeof(TestMode), nameof(TestMode.IsOn));
        var offGetter = AccessTools.PropertyGetter(typeof(TestMode), nameof(TestMode.IsOff));
        var code = instructions.ToList();
        var checks = code.Where(i => i.Calls(getter) || i.Calls(offGetter)).ToList();
        if (checks.Count != 1)
            throw new InvalidOperationException($"Expected exactly one native TestMode branch, found {checks.Count}.");
        // Preserve labels/exception blocks and every native operation.
        checks[0].opcode = checks[0].Calls(offGetter) ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0;
        checks[0].operand = null;
        return code;
    }
}
