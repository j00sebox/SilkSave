using BepInEx;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using GlobalEnums;   
using TeamCherry.GameCore;  

[BepInPlugin("com.example.SilkSave", "SilkSave Mod", "1.0.0")]
public class SilkSave : BaseUnityPlugin
{
    private string lastScene;
    private string saveName = "testSave";

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

        SaveGameData saveData = GameManager.instance.CreateSaveGameData(2);
		RestorePointData restorePointData = new RestorePointData(saveData, AutoSaveName.NONE);
		restorePointData.SetVersion();
		restorePointData.SetDateString();
		string text = SaveDataUtility.SerializeSaveData<RestorePointData>(restorePointData);
		byte[] bytesForSaveJson = GameManager.instance.GetBytesForSaveJson(text);
		Platform.Current.CreateSaveRestorePoint(2, saveName, true, bytesForSaveJson, null);
    }

    void LoadPosition()
    {
        // Cast the lambda to the expected delegate type
        // Action<byte[]> callback = (bytes) =>
        // {
        //     Logger.LogInfo("yeet");

        //     try
        //     {
        //         RestorePointFileWrapper wrapper = SaveDataUtility.DeserializeSaveData<RestorePointFileWrapper>(GameManager.instance.GetJsonForSaveBytes(bytes));

        //         if (wrapper != null)
        //             Logger.LogInfo("Identifier: " + wrapper.identifier);
        //         else
        //             Logger.LogError("Restore point is null!");
        //     } 
        //     catch(Exception ex)
        //     {
        //         Logger.LogInfo(ex.Message);
        //     }

        //     Logger.LogInfo("yah");
        // };

        // GameCoreRuntimeManager.LoadSaveData("Restore_Points1", "NODELrestoreData45.dat", callback);

        Dictionary<string, SceneTeleportMap.SceneInfo> teleportMap = SceneTeleportMap.GetTeleportMap();
        SceneTeleportMap.SceneInfo sceneInfo = teleportMap[lastScene];

        GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo {
            SceneName = lastScene,
            EntryGateName = sceneInfo.TransitionGates[0], 
            HeroLeaveDirection = GatePosition.unknown,
            PreventCameraFadeOut = true,
            WaitForSceneTransitionCameraFade = false,
            Visualization = GameManager.SceneLoadVisualizations.Default
        });

        StartCoroutine(TeleportHeroWhenReady());
    }

    IEnumerator TeleportHeroWhenReady()
    {
        var hero = HeroController.instance;
        yield return new WaitUntil(() =>
        {
            return hero != null && hero.CanInput();
        });

        string path = Path.Combine(Paths.ConfigPath, "playerpos.txt");
        // if (!File.Exists(path)) return;

        string[] coords = File.ReadAllText(path).Split(',');
        // if (coords.Length != 3) return;

        float x = float.Parse(coords[0]);
        float y = float.Parse(coords[1]);
        float z = float.Parse(coords[2]);

        hero.transform.position = new Vector3(x, y, z);

        var rb = hero.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        if (hero.cState != null)
        {
            hero.cState.recoiling = false;
            hero.cState.transitioning = false;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F5)) SavePosition();
        if (Input.GetKeyDown(KeyCode.F9)) LoadPosition();
    }
}