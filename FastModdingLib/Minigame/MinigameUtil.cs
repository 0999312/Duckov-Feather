using System;
using System.Runtime.CompilerServices;

using Duckov.MiniGames;

using FeatherMod.Register;
using FeatherMod.Utils;

using UnityEngine;

namespace FeatherMod.Minigame;

public class MinigameUtil: Singleton<MinigameUtil>
{
    public SimpleRegistry<GameObject> MinigameRegistry = new();

    private static RenderTexture _renderTexture;

    public static RenderTexture renderTexture
    {
        get
        {
            if (_renderTexture == null)
            {
                var allRenderTextures = Resources.FindObjectsOfTypeAll<RenderTexture>();
                foreach (var rt in allRenderTextures)
                {
                    if (rt != null && rt.name == "GamingConsoleRT")
                    {
                        _renderTexture = rt;
                        break;
                    }
                }
            }
            return _renderTexture;
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void RegisterMinigame(Identifier id, GameObject obj)
    {
        MiniGame g = obj.GetComponentInChildren<MiniGame>();
        if (g == null) throw new ArgumentException("The provided GameObject doesn't have active MiniGame component");
        Instance.MinigameRegistry.Set(id, obj);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static GameObject NewMinigameBase(Identifier id, out Camera camera, out Camera uiCamera)
    {
        GameObject baseObject = new GameObject(id.ToString());
        baseObject.SetActive(false);
        baseObject.layer = 30;
        UnityEngine.Object.DontDestroyOnLoad(baseObject);

        baseObject.transform.position = Vector3.zero;
        baseObject.transform.rotation = Quaternion.identity;
        baseObject.transform.localScale = Vector3.one;

        MiniGame miniGame = baseObject.AddComponent<MiniGame>();
        miniGame.id = id.ToString();
        miniGame.renderTexture = renderTexture;

        GameObject cameramain = new GameObject(id + "_CameraMain");
        cameramain.layer = 30;
        cameramain.transform.SetParent(baseObject.transform, false);
        cameramain.transform.position = Vector3.zero;
        cameramain.transform.rotation = Quaternion.identity;
        cameramain.transform.localScale = Vector3.one;

        camera = cameramain.AddComponent<Camera>();
        camera.rect = new Rect(0, 0, 1, 1);
        camera.aspect = 480f / 320f;
        camera.cullingMask = 1 << 30;
        camera.depth = -100;
        camera.clearFlags = CameraClearFlags.Color;
        miniGame.camera = camera;

        GameObject cameraui = new GameObject(id + "_CameraUI");
        cameraui.layer = 5;
        cameraui.transform.SetParent(baseObject.transform, false);
        cameraui.transform.position = Vector3.zero;
        cameraui.transform.rotation = Quaternion.identity;
        cameraui.transform.localScale = Vector3.one;

        uiCamera = cameraui.AddComponent<Camera>();
        uiCamera.rect = new Rect(0, 0, 1, 1);
        uiCamera.orthographic = true;
        uiCamera.aspect = 480f / 320f;
        uiCamera.orthographicSize = 160f;
        uiCamera.cullingMask = 1 << 5;
        uiCamera.depth = -90;
        uiCamera.clearFlags = CameraClearFlags.Depth;
        miniGame.uiCamera = uiCamera;

        return baseObject;
    }
}
