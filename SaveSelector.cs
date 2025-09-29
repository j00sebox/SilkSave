using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ModSavePicker : MonoBehaviour
{
    public string savesFolder; // Folder where saves are stored
    public Vector2 panelSize = new Vector2(800, 600);
    public Vector2 slotSize = new Vector2(200, 100);
    public int slotsPerPage = 6;

    private GameObject canvasObj;
    private GameObject panelObj;
    private List<GameObject> saveSlots = new List<GameObject>();
    private List<string> saveFiles = new List<string>();
    private int currentPage = 0;
    private Action<string> onSaveSelected;

    public void Show(string folder, Action<string> callback)
    {
        savesFolder = folder;
        onSaveSelected = callback;

        LoadSaveFiles();

        CreateCanvas();
        CreatePanel();
        CreateSaveSlots();
        UpdatePage();
    }

    private void LoadSaveFiles()
    {
        if (!Directory.Exists(savesFolder))
        {
            Debug.LogError($"Save folder does not exist: {savesFolder}");
            saveFiles.Clear();
            return;
        }

        saveFiles = Directory.GetFiles(savesFolder, "*.dat").ToList();
    }

    private void CreateCanvas()
    {
        if (canvasObj != null) return;

        canvasObj = new GameObject("ModSaveCanvas");
        Canvas c = canvasObj.AddComponent<Canvas>();
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
    }

private void CreatePanel()
{
    panelObj = new GameObject("Panel");
    panelObj.transform.SetParent(canvasObj.transform, false);

    Image img = panelObj.AddComponent<Image>();
    img.color = new Color(0f, 0f, 0f, 0.8f); // semi-transparent background

    RectTransform rt = panelObj.GetComponent<RectTransform>();
    rt.anchorMin = new Vector2(0f, 0f); // bottom-left
    rt.anchorMax = new Vector2(1f, 1f); // top-right
    rt.pivot = new Vector2(0.5f, 0.5f);
    rt.offsetMin = Vector2.zero; // left-bottom offset
    rt.offsetMax = Vector2.zero; // right-top offset
}

    private void CreateSaveSlots()
    {
        foreach (GameObject obj in saveSlots)
            Destroy(obj);
        saveSlots.Clear();

        for (int i = 0; i < slotsPerPage; i++)
        {
            GameObject slot = new GameObject("SaveSlot" + i);
            slot.transform.SetParent(panelObj.transform, false);

            RectTransform rt = slot.AddComponent<RectTransform>();
            rt.sizeDelta = slotSize;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);

            float x = 10 + (i % 2) * (slotSize.x + 10);
            float y = -10 - (i / 2) * (slotSize.y + 10);
            rt.anchoredPosition = new Vector2(x, y);

            Button btn = slot.AddComponent<Button>();
            Image bg = slot.AddComponent<Image>();
            bg.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(slot.transform, false);
            Text txt = textObj.AddComponent<Text>();
            txt.alignment = TextAnchor.MiddleCenter;
            txt.color = Color.white;
            txt.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            txt.rectTransform.sizeDelta = slotSize;

            saveSlots.Add(slot);

            int index = i;
            btn.onClick.AddListener(() =>
            {
                int globalIndex = currentPage * slotsPerPage + index;
                if (globalIndex < saveFiles.Count)
                    SelectSave(saveFiles[globalIndex]);
            });
        }
    }

    private void UpdatePage()
    {
        for (int i = 0; i < saveSlots.Count; i++)
        {
            int globalIndex = currentPage * slotsPerPage + i;
            GameObject slot = saveSlots[i];
            Text txt = slot.GetComponentInChildren<Text>();

            if (globalIndex < saveFiles.Count)
            {
                string file = Path.GetFileNameWithoutExtension(saveFiles[globalIndex]);
                txt.text = file;
                slot.SetActive(true);
            }
            else
            {
                slot.SetActive(false);
            }
        }
    }

    private void SelectSave(string savePath)
    {
        onSaveSelected?.Invoke(savePath);
        Close();
    }

    public void NextPage()
    {
        if ((currentPage + 1) * slotsPerPage < saveFiles.Count)
        {
            currentPage++;
            UpdatePage();
        }
    }

    public void PrevPage()
    {
        if (currentPage > 0)
        {
            currentPage--;
            UpdatePage();
        }
    }

    public void Close()
    {
        Destroy(canvasObj);
        canvasObj = null;
    }
}