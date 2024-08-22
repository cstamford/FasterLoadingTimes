using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Area;
using Kingmaker.EntitySystem.Persistence;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;

namespace FasterLoadingTimes;

[HarmonyPatch]
public static class LoadTimeline_Patches {
    [HarmonyPrefix]
    [HarmonyPatch(typeof(LoadingProcess), nameof(LoadingProcess.StartLoadingProcessInternal))]
    private static void LoadingProcess__StartLoadingProcessInternal(IEnumerator process, Action callback, LoadingProcessTag processTag) {
        string processName = process.GetType().Name;
        int start = processName.IndexOf('<');
        int end = processName.IndexOf('>');

        if (start != -1 && end != -1) {
            processName = processName.Substring(start + 1, end - start - 1);
        }

        LoadTimeline.LoadingProcess_PushEvent(processName);
    }

    [HarmonyTranspiler]
    [HarmonyPatch(typeof(LoadingProcess), nameof(LoadingProcess.TickLoading))]
    private static IEnumerable<CodeInstruction> LoadingProcess__TickLoading(IEnumerable<CodeInstruction> instructions) {
        // We're looking for this:
        // IL_003B: ldfld     class [System]System.Diagnostics.Stopwatch Kingmaker.EntitySystem.Persistence.LoadingProcess::m_OneTaskStopwatch
        // IL_0040: callvirt  instance void [System]System.Diagnostics.Stopwatch::Stop()
        // After the callvirt, we're in the "our current task is done" state

        List<CodeInstruction> insts = [.. instructions];

        for (int i = 0; i < insts.Count; ++i) {
            CodeInstruction inst = insts[i];
            yield return inst;

            if (i >= insts.Count - 1) {
                break;
            }

            CodeInstruction nextInst = insts[i + 1];
            bool loadsField = inst.LoadsField(AccessTools.Field(typeof(LoadingProcess), nameof(LoadingProcess.m_OneTaskStopwatch)));
            bool callsStop = nextInst.Calls(AccessTools.Method(typeof(Stopwatch), nameof(Stopwatch.Stop)));

            if (loadsField && callsStop) {
                yield return nextInst;
                yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(LoadTimeline_Patches), nameof(LoadingProcess__TickLoading_OnFinished)));
                ++i; // skip the next instruction
            }
        }
    }

    private static void LoadingProcess__TickLoading_OnFinished() {
        LoadTimeline.LoadingProcess_PopEvent();
    }

    [HarmonyPrefix]
    [HarmonyPatch(typeof(Game), nameof(Game.LoadArea), [typeof(BlueprintArea), typeof(BlueprintAreaEnterPoint), typeof(AutoSaveMode), typeof(SaveInfo), typeof(Action)])]
    private static void Game__LoadArea() {
        LoadTimeline.LoadingProcess_ClearEvents();
    }
}
