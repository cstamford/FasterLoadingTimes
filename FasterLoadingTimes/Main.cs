using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem;
using Kingmaker.EntitySystem.Persistence.Scenes;
using Kingmaker.View;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

namespace FasterLoadingTimes;

#if DEBUG
[EnableReloading]
#endif
public static class Main {
    internal static Harmony HarmonyInstance;
    internal static UnityModManager.ModEntry.ModLogger Log;

    public static bool Load(UnityModManager.ModEntry modEntry) {
        Log = modEntry.Logger;

#if DEBUG
        modEntry.OnUnload = OnUnload;
#endif
        modEntry.OnGUI = OnGUI;

        HarmonyInstance = new Harmony(modEntry.Info.Id);
        HarmonyInstance.PatchAll(Assembly.GetExecutingAssembly());
        return true;
    }

#if DEBUG
    public static bool OnUnload(UnityModManager.ModEntry modEntry) {
        HarmonyInstance.UnpatchAll(modEntry.Info.Id);
        return true;
    }
#endif

    public static void OnGUI(UnityModManager.ModEntry mod) {
        LoadTimeline.OnGUI();
    }
}

[HarmonyPatch(typeof(SceneLoader))] // Kingmaker.EntitySystem.Persistence.Scenes.SceneLoader
public static class SceneLoaderPatches {
    [HarmonyPrefix]
    [HarmonyPatch(nameof(SceneLoader.MatchStateWithSceneCoroutine))]
    private static bool MatchStateWithSceneCoroutine(SceneLoader __instance, SceneEntitiesState state, ref IEnumerator __result) {
        __result = MatchStateWithSceneCoroutineImpl(__instance, state);
        return false;
    }

    // Previously: for each entity we're trying to create, we yield one frame
    // --> this costs a single frame during loading
    // --> load time scales with fps and save complexity (in entity count)
    // --> the vast majority of time spent loading late game saves is actually doing nothing (just rendering load screen)
    //
    // Now: we yield every fixed timestep
    // --> we maintain at least 30 fps while loading, and load as fast as possible
    // --> load time scaling is gated elsewhere
    //
    // If you're updating this method for future patches, check SceneLoader.MatchStateWithSceneCoroutine.
    // This function should be a near 1:1 with the exception of explicit types and anything after END ORIGINAL and before RESUME ORIGINAL.
    //
    // Why not use a transpiler? This will take five minutes to update and a transpiler-gone-wrong might take hours (and a headache).
    private static IEnumerator MatchStateWithSceneCoroutineImpl(SceneLoader sceneLoader, SceneEntitiesState state) {
        Scene scene = SceneManager.GetSceneByName(state.SceneName);

        if (!scene.isLoaded) {
            state.IsSceneLoadedThreadSafe = false;
            yield break;
        }

        Game.Instance?.EntitySpawner?.SuppressSpawn.Retain();

        // END ORIGINAL

        IEnumerable<EntityViewBase> entities = scene.GetRootGameObjects()
            .SelectMany(obj => obj.GetComponentsInChildren<EntityViewBase>(true));

        IEnumerator entitiesIterator = state.HasEntityData ?
            sceneLoader.LoadSceneEntitiesCoroutine(state, entities) :
            sceneLoader.CreateSceneEntitiesCoroutine(state, entities);

        Stopwatch entitiesIteratorSw = Stopwatch.StartNew();

        while (entitiesIterator.MoveNext()) {
            if (entitiesIteratorSw.Elapsed >= TimeSpan.FromSeconds(1.0 / 30.0)) {
                yield return null;
                entitiesIteratorSw.Restart();
            }
        }

        // RESUME ORIGINAL

        state.IsSceneLoadedThreadSafe = true;
        state.HasEntityData = true;
        Game.Instance?.EntitySpawner?.SuppressSpawn.Release();
    }
}