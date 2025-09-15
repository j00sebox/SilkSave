using BepInEx;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using System.Globalization;
using GlobalEnums;   
using TeamCherry.GameCore;  
using System.Windows.Forms;

public static class AsyncWinFormsPrompt
{
    public static void ShowDialogAsync(string text, string caption, Action<string> callback)
    {
        new Thread(() =>
        {
            string result = null;

            Form prompt = new Form()
            {
                Width = 400,
                Height = 150,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                Text = caption,
                StartPosition = FormStartPosition.CenterScreen
            };

            Label textLabel = new Label() { Left = 20, Top = 20, Text = text, AutoSize = true };
            TextBox inputBox = new TextBox() { Left = 20, Top = 50, Width = 340 };

            System.Windows.Forms.Button confirmation = new System.Windows.Forms.Button() { Text = "Ok", Left = 280, Width = 80, Top = 80, DialogResult = DialogResult.OK };
            confirmation.Click += (sender, e) =>
            {
                result = inputBox.Text;
                prompt.Invoke((MethodInvoker)(() => prompt.Close()));
            };

            prompt.Controls.Add(textLabel);
            prompt.Controls.Add(inputBox);
            prompt.Controls.Add(confirmation);
            prompt.AcceptButton = confirmation;

            prompt.ShowDialog();
            callback?.Invoke(result);

        }).Start();
    }
}

[BepInPlugin("com.example.SilkSave", "SilkSave Mod", "1.0.0")]
public class SilkSave : BaseUnityPlugin
{
    private string saveName;

    void SavePosition()
    {
        AsyncWinFormsPrompt.ShowDialogAsync("Enter a save name:", "Custom Save", (saveName) =>
        {
            if (!string.IsNullOrEmpty(saveName))
            {
                this.saveName = saveName;

                string SafeFileName(string name)
                {
                    foreach (char c in Path.GetInvalidFileNameChars())
                        name = name.Replace(c, '_');
                    return name;
                }

                if (HeroController.instance != null && GameManager.instance != null)
                {
                    Vector3 pos = HeroController.instance.transform.position;
                    string filename = SafeFileName($"{saveName}.txt");
                    string fileData = $"Scene: {GameManager.instance.sceneName}\nPosition: {pos.x},{pos.y},{pos.z}";
                    File.WriteAllText(Path.Combine(Paths.ConfigPath, filename), fileData);
                }
                else
                {
                    Logger?.LogWarning("Failed to save");
                }

                SaveGameData saveData = GameManager.instance.CreateSaveGameData(1);
                RestorePointData restorePointData = new RestorePointData(saveData, AutoSaveName.NONE);
                restorePointData.SetVersion();
                restorePointData.SetDateString();
                string text = SaveDataUtility.SerializeSaveData<RestorePointData>(restorePointData);
                byte[] bytesForSaveJson = GameManager.instance.GetBytesForSaveJson(text);

                string fileName = $"{saveName}.dat";
                string savePath = Path.Combine(Paths.ConfigPath, fileName);

                File.WriteAllBytes(savePath, bytesForSaveJson);
                Logger.LogInfo($"Saved custom save: {savePath}");
            }
        });
    }

    void LoadPosition()
    {
        string filePath = Path.Combine(Paths.ConfigPath, $"{saveName}.dat");

        if (!File.Exists(filePath))
        {
            Logger.LogError("Save file does not exist: " + filePath);
            return;
        }

        try
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);

            string json = GameManager.instance.GetJsonForSaveBytes(fileBytes);
            RestorePointData restorePointData = SaveDataUtility.DeserializeSaveData<RestorePointData>(json);
            SaveGameData loadedSave = restorePointData.saveGameData;

            GameManager.instance.playerData = loadedSave.playerData;
            PlayerData.instance = loadedSave.playerData;
            GameManager.instance.sceneData = loadedSave.sceneData;

            HeroController hc = HeroController.instance;

            hc.gameObject.SetActive(false);
            hc.gameObject.SetActive(true);

            Logger.LogInfo($"Loaded save from {filePath}");
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to load save: " + e);
        }

        filePath = Path.Combine(Paths.ConfigPath, $"{saveName}.txt");
        if (!File.Exists(filePath)) return;

        string[] lines = File.ReadAllLines(filePath);

        if (lines.Length < 2)
        {
            Logger.LogError("File format invalid: " + filePath);
            return;
        }

        string sceneName = lines[0].Split(':')[1].Trim();

        string[] posParts = lines[1].Split(':')[1].Trim().Split(',');
        float x = float.Parse(posParts[0], CultureInfo.InvariantCulture);
        float y = float.Parse(posParts[1], CultureInfo.InvariantCulture);
        float z = float.Parse(posParts[2], CultureInfo.InvariantCulture);

        Vector3 position = new Vector3(x, y, z);

        Dictionary<string, SceneTeleportMap.SceneInfo> teleportMap = SceneTeleportMap.GetTeleportMap();
        SceneTeleportMap.SceneInfo sceneInfo = teleportMap[sceneName];

        GameManager.instance.BeginSceneTransition(new GameManager.SceneLoadInfo {
            SceneName = sceneName,
            EntryGateName = sceneInfo.TransitionGates[0], 
            HeroLeaveDirection = GatePosition.unknown,
            PreventCameraFadeOut = true,
            WaitForSceneTransitionCameraFade = false,
            Visualization = GameManager.SceneLoadVisualizations.Default
        });

        StartCoroutine(TeleportHeroWhenReady(position));
    }

    IEnumerator TeleportHeroWhenReady(Vector3 destination)
    {
        var hero = HeroController.instance;
        yield return new WaitUntil(() =>
        {
            return hero != null && hero.CanInput();
        });

        hero.transform.position = destination;

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