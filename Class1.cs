using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using GlobalEnums;     

[BepInPlugin("com.example.helloworld", "HelloWorld Mod", "1.0.0")]
public class HelloWorld : BaseUnityPlugin
{
    private string lastScene;

    void SavePosition()
    {
        if (HeroController.instance != null && GameManager.instance != null)
        {
            Vector3 pos = HeroController.instance.transform.position;
            lastScene = GameManager.instance.sceneName;
            File.WriteAllText(Path.Combine(Paths.ConfigPath, "playerpos.txt"), $"{pos.x},{pos.y},{pos.z}");
            Logger.LogInfo("Saved position: " + pos);
        }
        else
        {
            Logger?.LogWarning("Failed to save position");
        }
    }

    void LoadPosition()
    {
        Dictionary<string, SceneTeleportMap.SceneInfo> teleportMap = SceneTeleportMap.GetTeleportMap();
        SceneTeleportMap.SceneInfo sceneInfo = teleportMap[lastScene];

        GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo {
            SceneName = lastScene,
            EntryGateName = sceneInfo.TransitionGates[0], 
            HeroLeaveDirection = GatePosition.unknown, // dnSpy should show you enum values
            PreventCameraFadeOut = true,
            WaitForSceneTransitionCameraFade = false,
            Visualization = GameManager.SceneLoadVisualizations.Default
        });

        StartCoroutine(TeleportHeroWhenReady());
    }

    IEnumerator TeleportHeroWhenReady()
    {
        yield return new WaitUntil(() =>
        {
            var hero = HeroController.instance;
            return hero != null && hero.CanInput();
        });

        string path = Path.Combine(Paths.ConfigPath, "playerpos.txt");
        // if (!File.Exists(path)) return;

        string[] coords = File.ReadAllText(path).Split(',');
        // if (coords.Length != 3) return;

        float x = float.Parse(coords[0]);
        float y = float.Parse(coords[1]);
        float z = float.Parse(coords[2]);

        HeroController.instance.transform.position = new Vector3(x, y, z);

        var rb = HeroController.instance.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (HeroController.instance.cState != null)
        {
            HeroController.instance.cState.recoiling = false;
            HeroController.instance.cState.transitioning = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) SavePosition();
        if (Input.GetKeyDown(KeyCode.F9)) LoadPosition();
    }
}