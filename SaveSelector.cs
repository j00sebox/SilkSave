using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using HutongGames.PlayMaker.Actions;

public class SaveSelector : MonoBehaviour
{
    private string savesFolder = "";
    private Vector2 panelSize = new Vector2(800, 600);
    private Vector2 slotSize = new Vector2(200, 100);
    private int slotsPerPage = 6;
    private int slotsOnPage = 0;

    private GameObject? canvasObj;
    private GameObject? panelObj;
    private List<GameObject> saveSlots = new List<GameObject>();
    private List<string> saveFiles = new List<string>();
    private int currentPage = 0;
    private Action<string>? onSaveSelected;
    private int highlightedIndex = 0;
    private bool isShowing = false;

    private float stickTimer = -1f;
    private float stickRepeatDelay = -1.2f;
    private float stickDeadzone = -1.5f;

    public void Show(string folder, Action<string> callback)
    {
        savesFolder = folder;
        onSaveSelected = callback;

        Time.timeScale = 0f;
        isShowing = true;

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
        panelObj.transform.SetParent(canvasObj?.transform, false);

        Image img = panelObj.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.8f);

        RectTransform rt = panelObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void CreateSaveSlots()
    {
        foreach (GameObject obj in saveSlots)
            Destroy(obj);
        saveSlots.Clear();

        int columns = 2;
        int rows = Mathf.CeilToInt((float)slotsPerPage / columns);

        float panelWidth = ((RectTransform)panelObj.transform).rect.width;
        float panelHeight = ((RectTransform)panelObj.transform).rect.height;

        float slotWidth = (panelWidth - (columns + 1) * 10) / columns;
        float slotHeight = (panelHeight - (rows + 1) * 10) / rows;

        for (int i = 0; i < slotsPerPage; i++)
        {
            GameObject slot = new GameObject("SaveSlot" + i);
            slot.transform.SetParent(panelObj.transform, false);

            RectTransform rt = slot.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(slotWidth, slotHeight);
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);

            int col = i % columns;
            int row = i / columns;

            float x = 10 + col * (slotWidth + 10);
            float y = -10 - row * (slotHeight + 10);
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
            txt.rectTransform.anchorMin = Vector2.zero;
            txt.rectTransform.anchorMax = Vector2.one;
            txt.rectTransform.offsetMin = Vector2.zero;
            txt.rectTransform.offsetMax = Vector2.zero;

            Outline outline = slot.AddComponent<Outline>();
            outline.effectColor = Color.white;
            outline.effectDistance = new Vector2(3, 3);
            outline.enabled = false;

            saveSlots.Add(slot);

            btn.onClick.AddListener(() =>
            {
                int globalIndex = currentPage * slotsPerPage + i;
                if (globalIndex < saveFiles.Count)
                    SelectSave(saveFiles[globalIndex]);
            });
        }
    }

    private void UpdatePage()
    {
        for (int i = 0; i < saveSlots.Count; ++i)
        {
            int globalIndex = currentPage * slotsPerPage + i;
            GameObject slot = saveSlots[i];
            Text txt = slot.GetComponentInChildren<Text>();
            Image bg = slot.GetComponent<Image>();

            if (globalIndex < saveFiles.Count)
            {
                string file = Path.GetFileNameWithoutExtension(saveFiles[globalIndex]);
                txt.text = file;
                slot.SetActive(true);

                if (i == highlightedIndex)
                    slot.GetComponent<Outline>().enabled = true;
                else
                    slot.GetComponent<Outline>().enabled = false;
            }
            else
            {
                slot.SetActive(false);
            }
        }
    }

    private void SelectSave(string savePath)
    {
        onSaveSelected?.Invoke(savePath.Replace(".dat", ""));
        Close();
    }

    private void NextPage()
    {
        if ((currentPage + 1) * slotsPerPage < saveFiles.Count)
        {
            ++currentPage;
            highlightedIndex = 0;
            UpdatePage();
        }
    }

    private void PrevPage()
    {
        if (currentPage > 0)
        {
            --currentPage;
            highlightedIndex = 0;
            UpdatePage();
        }
    }

    private void Close()
    {
        Destroy(canvasObj);
        canvasObj = null;
        Time.timeScale = 1f;
        isShowing = false;
    }

    private void Update()
    {
        if (isShowing)
        {
            slotsOnPage = Mathf.Min(slotsPerPage, saveFiles.Count - currentPage * slotsPerPage);

            stickTimer -= Time.unscaledDeltaTime;

            float horizontal = Input.GetAxis("Horizontal"); 
            float vertical = Input.GetAxis("Vertical");

            if (stickTimer <= 0f)
            {
                if (horizontal > stickDeadzone)
                {
                    MoveRight();
                    stickTimer = stickRepeatDelay;
                }
                else if (horizontal < -stickDeadzone)
                {
                    MoveLeft();
                    stickTimer = stickRepeatDelay;
                }

                if (vertical > stickDeadzone)
                {
                    MoveUp();             
                    stickTimer = stickRepeatDelay;
                }
                else if (vertical < -stickDeadzone)
                {
                    MoveDown();
                    stickTimer = stickRepeatDelay;
                }
            }

            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
            {
                MoveRight();
            }

            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
            {
                MoveLeft();
            }

            if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S))
            {
                MoveDown();
            }

            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W))
            {
                MoveUp();
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.JoystickButton0))
            {
                int globalIndex = currentPage * slotsPerPage + highlightedIndex;
                if (globalIndex < saveFiles.Count)
                    SelectSave(saveFiles[globalIndex]);
            }

            if (Input.GetKeyDown(KeyCode.JoystickButton5))
            {
                NextPage();
            }

            if (Input.GetKeyDown(KeyCode.JoystickButton4))
            {
                PrevPage();
            }
        }
    }

    private void MoveRight()
    {
        if (highlightedIndex + 1 >= slotsOnPage)
            NextPage();
        else
            ++highlightedIndex;
        UpdatePage();
    }

    private void MoveLeft()
    {
        if (highlightedIndex - 1 < 0)
            PrevPage();
        else
            --highlightedIndex;
        UpdatePage();
    }

    private void MoveUp()
    {
        highlightedIndex -= 2;
        if (highlightedIndex < 0) highlightedIndex = 0;
        UpdatePage();
    }

    private void MoveDown()
    {
        highlightedIndex += 2;
        if (highlightedIndex >= slotsOnPage) highlightedIndex = slotsOnPage - 1;
        UpdatePage();
    }
}