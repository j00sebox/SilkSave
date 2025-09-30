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
using InControl;
using System.Linq;

public static class AsyncWinFormsPrompt
{
    public static void ShowDialogAsync(string text, string caption, Action<string> callback)
    {
        new Thread(() =>
        {
            string result = "";

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
    private HeroController hero = null!;
    private GameManager gameManager = null!;
    private string saveName = null!;
    private int saveSlot = 0;
    private string savePath = "";

    private SaveSelector? saveSelector = null;

    private bool saveSelectorOpen = false;

    private void Start()
    {
        saveSelector = gameObject.AddComponent<SaveSelector>();
    }

    void SaveState()
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

                if (hero != null && GameManager.instance != null)
                {
                    var transitionPoints = TransitionPoint.TransitionPoints;
                    var safeEntries = transitionPoints.Where(point => point != null && !point.isInactive).ToList();

                    Vector3 pos = hero.transform.position;
                    string filename = SafeFileName($"{saveName}.txt");
                    string fileData = $"Scene: {GameManager.instance.sceneName}\nPosition: {pos.x},{pos.y},{pos.z}\nEntryPointName: {safeEntries[0].name}";
                    File.WriteAllText(Path.Combine(savePath, filename), fileData);
                }
                else
                {
                    Logger?.LogWarning("Failed to save");
                }

                SaveGameData saveData = gameManager.CreateSaveGameData(saveSlot);
                RestorePointData restorePointData = new RestorePointData(saveData, AutoSaveName.NONE);
                restorePointData.SetVersion();
                restorePointData.SetDateString();
                string text = SaveDataUtility.SerializeSaveData<RestorePointData>(restorePointData);
                byte[] bytesForSaveJson = gameManager.GetBytesForSaveJson(text);

                string fileName = $"{saveName}.dat";
                string path = Path.Combine(savePath, fileName);

                File.WriteAllBytes(path, bytesForSaveJson);
                Logger!.LogInfo($"Saved custom save: {path}");
            }
        });
    }

    // Basically copy-paste of what the game uses except it doesn't reset silk
    public void SetLoadedGameData(SaveGameData saveData, int saveSlot)
    {
        PlayerData playerData = saveData.playerData;
		SceneData sceneData = saveData.sceneData;
		playerData.ResetNonSerializableFields();
		PlayerData.instance = playerData;
		gameManager.playerData = playerData;
		SceneData.instance = sceneData;
		gameManager.sceneData = sceneData;
		gameManager.profileID = saveSlot;
		playerData.SetupExistingPlayerData();
		gameManager.inputHandler.RefreshPlayerData();
		QuestManager.UpgradeQuests();
		if (Platform.Current)
		{
			Platform.Current.OnSetGameData(gameManager.profileID);
		}
    }

    public void LoadState(string saveStateName)
    {
        this.saveName = saveStateName;
        LoadState();
    }

    // Loads last state that was loaded or saved
    private void LoadState()
    {
        gameManager.isPaused = true;

        string filePath = Path.Combine(savePath, $"{saveName}.dat");

        if (!File.Exists(filePath))
        {
            Logger.LogError("Save file does not exist: " + filePath);
            return;
        }

        try
        {
            byte[] fileBytes = File.ReadAllBytes(filePath);

            string json = gameManager.GetJsonForSaveBytes(fileBytes);
            RestorePointData restorePointData = SaveDataUtility.DeserializeSaveData<RestorePointData>(json);
            SaveGameData loadedSave = restorePointData.saveGameData;

            SetLoadedGameData(loadedSave, saveSlot);

            StartCoroutine(RunContinueAndTeleport());

            Logger.LogInfo($"Loaded save from {filePath}");
        }
        catch (Exception e)
        {
            Logger.LogError("Failed to load save: " + e);
        }
    }

    private IEnumerator RunContinueAndTeleport()
    {
        yield return gameManager.RunContinueGame(false);

        yield return new WaitUntil(() =>
        {
            return hero != null && !gameManager.IsInSceneTransition && hero.isHeroInPosition && !hero.cState.transitioning && hero.CanInput();
        });

        // try
        // {
        //     var spawnPoint = hero.LocateSpawnPoint();

        //     if (spawnPoint == null)
        //     {
        //         Logger.LogError("No spawn point found.");
        //     }

        //     var benchFSM = FSMUtility.LocateFSM(spawnPoint.gameObject, "Bench Control");
        //     if (benchFSM == null)
        //     {
        //         Logger.LogError("Bench Control FSM not found.");
        //     }

        //     Logger.LogInfo("Sending FINISHED event to Bench FSM...");
        //     benchFSM.SendEvent("GET UP");
        // }
        // catch(Exception ex)
        // {
        //     Logger.LogInfo(ex.Message);
        // }
        
        Teleport();
    }

    public void Teleport()
    {
        string filePath = Path.Combine(savePath, $"{saveName}.txt");
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

        string entryPoint = "";
        if (lines.Length > 2) // at least 3 lines
        {
            var parts = lines[2].Split(':');
            if (parts.Length > 1) // has a value after the colon
            {
                entryPoint = parts[1].Trim();
            }
        }

        Vector3 position = new Vector3(x, y, z);

        Dictionary<string, SceneTeleportMap.SceneInfo> teleportMap = SceneTeleportMap.GetTeleportMap();
        SceneTeleportMap.SceneInfo sceneInfo = teleportMap[sceneName];

        if (entryPoint == "")
            entryPoint = sceneInfo.TransitionGates[0];

        gameManager.BeginSceneTransition(new GameManager.SceneLoadInfo {
            SceneName = sceneName,
            EntryGateName = entryPoint,
            HeroLeaveDirection = GatePosition.unknown,
            PreventCameraFadeOut = true,
            WaitForSceneTransitionCameraFade = false,
            Visualization = GameManager.SceneLoadVisualizations.Default
        });

        StartCoroutine(TeleportHeroWhenReady(position));

        Logger.LogInfo("Teleported!");
    }

    IEnumerator TeleportHeroWhenReady(Vector3 destination)
    {
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

    private void SetSavePath()
    {
        saveSlot = PlayerData.instance.profileID;
        if (saveSlot == 0) return;
        Logger.LogInfo("Active slot is: " + saveSlot);
        string saveRoot = Path.Combine(Paths.BepInExRootPath, "silksaves");
        savePath = Path.Combine(saveRoot, $"slot_{saveSlot}");

        Directory.CreateDirectory(savePath);
    }

    void Update()
    {
        if (hero == null) hero = HeroController.instance;
        if (gameManager == null) gameManager = GameManager.instance;
        if (saveSlot == 0) SetSavePath();
        
        if (Input.GetKeyDown(KeyCode.F5)) SaveState();
        if (Input.GetKeyDown(KeyCode.F6)) LoadState();
        if (Input.GetKeyDown(KeyCode.F2))
        {
            if (!saveSelectorOpen)
            {
                saveSelectorOpen = true;
                saveSelector?.Show(savePath, (selectedSave) =>
                {
                    Logger.LogInfo("User selected save: " + selectedSave);
                    saveSelectorOpen = false;

                    LoadState(selectedSave);
                });
            }
            else
            {
                saveSelector?.Close();
                saveSelectorOpen = false;
            }
        } 
    }
}