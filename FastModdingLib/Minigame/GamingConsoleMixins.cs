using System;

using Duckov.MiniGames;

using FeatherMod.Utils;

using HarmonyLib;

using UnityEngine;

namespace FeatherMod.Minigame;

[HarmonyPatch(typeof(GamingConsole))]
public class GamingConsoleMixins
{
    [HarmonyPatch("SelectedGame", MethodType.Getter)]
    [HarmonyPostfix]
    static void PostfixSelectedGame(ref MiniGame __result, GamingConsole __instance)
    {
        if (__instance.CatridgeGameID == null) return;
        Identifier id;
        try
        {
            id = new Identifier(__instance.CatridgeGameID);
        }
        catch (ArgumentException)
        {
            return;
        }

        if (MinigameUtil.Instance.MinigameRegistry.TryGet(id, out GameObject game))
        {
            __result = game.GetComponentInChildren<MiniGame>();
        }
    }

    [HarmonyPatch("CreateGame")]
    [HarmonyPostfix]
    static void PostfixCreateGame(GamingConsole __instance, MiniGame prefab)
    {
        if (__instance.isBeingDestroyed) return;
        if (__instance.game != null)
        {
            __instance.game.gameObject.SetActive(true);
        }
    }
}
