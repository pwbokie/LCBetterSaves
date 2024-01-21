using BepInEx;
using HarmonyLib;
using LCBetterSaves;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCBetterSaves
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony = new Harmony("BetterSaves");
        public static int fileToModify = -1;
        public static int newSaveFileNum;

        public static Sprite renameSprite;

        public void Awake()
        {
            // Plugin startup logic
            _harmony.PatchAll(typeof(Plugin));
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        public static MenuManager menuManager;

        [HarmonyPatch(typeof(MenuManager), "Start")]
        public static void Postfix(MenuManager __instance)
        {
            menuManager = __instance;

            if (renameSprite == null)
            {
                AssetBundle assetBundle = AssetBundle.LoadFromMemory(Properties.Resources.lcbettersaves);
                Texture2D renameTexture = assetBundle.LoadAsset<Texture2D>("Assets/RenameSprite.png");
                renameSprite = Sprite.Create(renameTexture, new Rect(0, 0, renameTexture.width, renameTexture.height), new Vector2(0.5f, 0.5f));
            }

            InitializeBetterSaves();
        }

        public static void InitializeBetterSaves()
        {
            try
            {
                // Destroy everything (except File1) so we can start over
                DestroyBetterSavesButtons();
                DestroyOriginalSaveButtons();

                // Update the text at the top of the window
                UpdateTopText();

                // Tweak the delete file button to work with our mod
                CreateModdedDeleteFileButton();

                // Instantiate New File button and all existing save file buttons
                CreateBetterSaveButtons();

                // Update the size of the files panel
                // Also updates the position of the weekly run button
                UpdateFilesPanelRect(CountSaveFiles() + 1);

                // Disable the original File1 node, in case it didn't happen when it was supposed to
                GameObject originalFileNode = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File1");
                originalFileNode.SetActive(false);
            }
            catch (Exception ex)
            {
                Debug.LogError("An error occurred during initialization: " + ex.Message);
            }
        }

        public static void DestroyBetterSavesButtons()
        {
            try
            {
                foreach (SaveFileUISlot_BetterSaves f in FindObjectsOfType<SaveFileUISlot_BetterSaves>())
                {
                    Destroy(f.gameObject);
                }
                foreach (NewFileUISlot_BetterSaves g in FindObjectsOfType<NewFileUISlot_BetterSaves>())
                {
                    Destroy(g.gameObject);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error occurred while destroying better saves buttons: " + ex.Message);
            }
        }

        public static void UpdateTopText()
        {
            GameObject panelLabel = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/EnterAName");
            if (panelLabel == null)
            {
                Debug.LogError("Panel label not found.");
                return;
            }
            panelLabel.GetComponent<TextMeshProUGUI>().text = "BetterSaves";
        }

        public static AudioClip deleteFileSFX;
        public static TextMeshProUGUI deleteFileText;

        public static void CreateModdedDeleteFileButton()
        {
            // Find the old DeleteFileButton
            GameObject deleteFileGO = GameObject.Find("Canvas/MenuContainer/DeleteFileConfirmation/Panel/Delete");

            if (deleteFileGO == null)
            {
                Debug.LogError("Delete file game object not found.");
                return;
            }

            // Get out if the modded DeleteFileButton_BetterSaves component already exists
            if (deleteFileGO.GetComponent<DeleteFileButton_BetterSaves>() != null)
            {
                Debug.LogWarning("DeleteFileButton_BetterSaves component already exists on deleteFileGO");
                return;
            }

            // Get the old DeleteFileButton component
            DeleteFileButton oldDeleteFileButton = deleteFileGO.GetComponent<DeleteFileButton>();
            if (oldDeleteFileButton == null)
            {
                Debug.LogError("DeleteFileButton component not found on deleteFileGO");
                return;
            }

            // Steal the values from the old DeleteFileButton
            if (deleteFileSFX == null)
            {
                deleteFileSFX = oldDeleteFileButton.deleteFileSFX;
            }

            if (deleteFileText == null)
            {
                deleteFileText = oldDeleteFileButton.deleteFileText;
            }

            // Remove the old DeleteFileButton component
            Destroy(oldDeleteFileButton);

            // Add the modded DeleteFileButton_BetterSaves component
            if (deleteFileGO.GetComponent<DeleteFileButton_BetterSaves>() == null)
            {
                DeleteFileButton_BetterSaves deleteButton = deleteFileGO.AddComponent<DeleteFileButton_BetterSaves>();
                deleteButton.deleteFileSFX = deleteFileSFX;
                deleteButton.deleteFileText = deleteFileText;

                // Update the button's onClick event
                Button deleteButtonComponent = deleteFileGO.GetComponent<Button>();
                if (deleteButtonComponent != null)
                {
                    deleteButtonComponent.onClick.RemoveAllListeners();
                    deleteButtonComponent.onClick.AddListener(deleteButton.DeleteFile);
                }
                else
                {
                    Debug.LogError("Button component not found on deleteFileGO");
                }
            }
            else
            {
                Debug.LogWarning("DeleteFileButton_BetterSaves component already exists on deleteFileGO");
            }
        }

        // Refreshes the save buttons based on the existing save files
        public static void CreateBetterSaveButtons()
        {
            try
            {
                GameObject originalFileNode = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File1");
                originalFileNode.SetActive(true);

                // Calculate the number of existing save files
                int numSaves = CountSaveFiles();

                Debug.Log("Positioning based on " + numSaves + " saves.");

                // Create the new file node
                NewFileUISlot_BetterSaves newFileSlot = CreateNewFileNode(numSaves);

                // Normalize the save file names to ensure they are in perfect numerical order
                List<string> saveFiles = NormalizeFileNames();

                // Update the number of the new save file
                newSaveFileNum = saveFiles.Count + 1;

                // Set all files as compatible
                menuManager.filesCompatible = new bool[16];
                for (int i = 0; i < menuManager.filesCompatible.Length; i++)
                {
                    menuManager.filesCompatible[i] = true;
                }

                // Create "File N" buttons for each existing save file
                // This is dependent on an existing NewFileSlot being present
                for (int i = 0; i < saveFiles.Count; i++)
                {
                    CreateModdedSaveNode(int.Parse(saveFiles[i].Replace("LCSaveFile", "")), newFileSlot.gameObject);
                }

                originalFileNode.SetActive(false);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error occurred while refreshing save buttons: " + ex.Message);
            }
        }

        public static float buttonBaseY;

        public static NewFileUISlot_BetterSaves CreateNewFileNode(int numSaves)
        {
            // Find the original file node
            GameObject originalFileNode = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File1");

            if (originalFileNode == null)
            {
                Debug.LogError("Original GameObject not found.");
                return null;
            }

            Transform parent = originalFileNode.transform.parent;

            // Remove the old SaveFileUISlot component from the original file node
            SaveFileUISlot saveFileUISlot = originalFileNode.GetComponent<SaveFileUISlot>();
            if (saveFileUISlot != null) Destroy(saveFileUISlot);

            GameObject clone = Instantiate(originalFileNode, parent);
            clone.name = "NewFile";

            // Set the text of the clone to "New File"
            TMP_Text textComponent = clone.transform.GetChild(1).GetComponent<TMP_Text>();
            if (textComponent != null)
            {
                textComponent.text = "New File";
            }
            else
            {
                Debug.LogError("Text component not found.");
                return null;
            }

            // Add the NewFileUISlot_BetterSaves component to the clone
            NewFileUISlot_BetterSaves newFileSlot = clone.AddComponent<NewFileUISlot_BetterSaves>();
            if (newFileSlot == null)
            {
                Debug.LogError("Failed to add NewFileUISlot_BetterSaves component.");
                return null;
            }

            // Destroy the DeleteFileButton from the clone
            Transform deleteButton = clone.transform.GetChild(3);
            if (deleteButton != null)
            {
                Destroy(deleteButton.gameObject);
            }
            else
            {
                Debug.LogError("Delete button not found.");
                return null;
            }

            // Reposition the clone based on the number of existing save files
            try
            {
                RectTransform rectTransform = clone.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    float x = rectTransform.anchoredPosition.x;
                    if (buttonBaseY == 0f)
                    {
                        buttonBaseY = rectTransform.anchoredPosition.y - (rectTransform.sizeDelta.y * 1.75f);
                    }
                    float y = buttonBaseY + (rectTransform.sizeDelta.y * (numSaves + 1) / 2);

                    rectTransform.anchoredPosition = new Vector2(x, y);
                }
                else
                {
                    Debug.LogError("RectTransform component not found.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error setting anchored position: " + ex.Message);
                return null;
            }

            return newFileSlot;
        }

        // Counts the number of existing save files
        private static int CountSaveFiles()
        {
            int numSaves = 0;
            foreach (string file in ES3.GetFiles())
            {
                if (ES3.FileExists(file) && Regex.IsMatch(file, @"^LCSaveFile\d+$"))
                {
                    numSaves++;
                }
            }
            return numSaves;
        }

        public static void DestroyOriginalSaveButtons()
        {
            // file 1 is used as the template and enabled/disabled as needed - leave it alive
            Destroy(GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File2"));
            Destroy(GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File3"));
        }

        public static List<string> NormalizeFileNames()
        {
            // Retrieve and filter the save files
            List<string> saveFiles = new List<string>();
            List<string> lguFiles = new List<string>();

            string lguPlaceHolder = "PH";
            string lguTemporaryFileFormat = "LGUTempFile{0}";
            string lguFileFormat = "LGU_{0}.json";

            foreach (string file in ES3.GetFiles())
            {
                if (!ES3.FileExists(file)) continue; // It doesn't exist

                if (Regex.IsMatch(file, @"^LCSaveFile\d+$"))
                {
                    Debug.Log("Found file: " + file);
                    saveFiles.Add(file);

                    // We can do this because we have LCSaveFile##, if we had LCSaveFile##whatever, this doesn't work
                    string linkedLGU = string.Format(lguFileFormat, file.Substring("LCSaveFile".Length));
                    if (ES3.FileExists(linkedLGU))
                    {
                        Debug.Log("Found LGU file: " + linkedLGU);
                        lguFiles.Add(linkedLGU);
                    }
                    else
                    {
                        // add a placeholder to maintain the index relationship between vanilla and lgu files.
                        lguFiles.Add(lguPlaceHolder);
                    }
                }
            }

            // Sort the saveFiles list in numerical order
            saveFiles.Sort((a, b) =>
            {
                int fileNumA = int.Parse(a.Substring("LCSaveFile".Length));
                int fileNumB = int.Parse(b.Substring("LCSaveFile".Length));
                return fileNumA.CompareTo(fileNumB);
            });

            // Rename all files to temporary names
            int tempIndex = 1;
            foreach (string file in saveFiles)
            {
                string tempName = "TempFile" + tempIndex.ToString();
                ES3.RenameFile(file, tempName);
                Debug.Log($"Renamed {file} to {tempName}");
                tempIndex++;
            }

            // Handle any LGU files
            tempIndex = 1;
            foreach (string file in lguFiles)
            {
                if (file == lguPlaceHolder) // if they exist
                { 
                    tempIndex++;
                    continue; 
                }
                string tempName = string.Format(lguTemporaryFileFormat, tempIndex.ToString());
                ES3.RenameFile(file, tempName);
                Debug.Log($"Renamed {file} to {tempName}");
                tempIndex++;
            }

            // Rename temporary files to normalized names
            int fileIndex = 1;
            List<string> newFiles = new List<string>();
            foreach (string file in saveFiles)
            {
                string oldTempName = "TempFile" + fileIndex.ToString();
                string newName = "LCSaveFile" + fileIndex.ToString();

                if (ES3.FileExists(oldTempName))
                {
                    ES3.RenameFile(oldTempName, newName); // Rename the file to the new format
                    newFiles.Add(newName);
                    Debug.Log($"Renamed {oldTempName} to {newName}");
                }
                else
                {
                    Debug.Log($"Temporary file {oldTempName} not found. It might have been moved or deleted.");
                }

                fileIndex++;
            }

            // Rename LGU tempfiles to normalized names
            fileIndex = 1;
            foreach (string file in lguFiles)
            {
                string oldTempName = string.Format(lguTemporaryFileFormat, fileIndex.ToString());
                string newName = string.Format(lguFileFormat, fileIndex.ToString());
                if(file == lguPlaceHolder)
                {
                    fileIndex++;
                    continue;
                }

                if (ES3.FileExists(oldTempName))
                {
                    ES3.RenameFile(oldTempName, newName); // Rename the file to the new format
                    newFiles.Add(newName);
                    Debug.Log($"Renamed {oldTempName} to {newName}");
                }
                else
                {
                    Debug.Log($"Temporary file {oldTempName} not found. It might have been moved or deleted.");
                }

                fileIndex++;
            }
            return newFiles;
        }

        // Instantiate a Node based on the original File1 GO
        public static void CreateModdedSaveNode(int fileNum, GameObject newFileButton)
        {
            // fileNum is the number at the end of the file name, not the index of the file

            // Find the original GameObject
            GameObject originalFileNode = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File1");

            if (originalFileNode == null)
            {
                Debug.LogError("Original GameObject not found.");
                return;
            }

            Transform parent = originalFileNode.transform.parent;

            // Clone the GameObject
            GameObject clone = Instantiate(originalFileNode, parent);
            clone.name = "File" + fileNum + "_BetterSaves";

            // Try and load the save file's Alias
            string alias = ES3.Load<string>("Alias_BetterSaves", "LCSaveFile" + fileNum, "");
            if (alias == "")
                clone.transform.GetChild(1).GetComponent<TMP_Text>().text = "File " + fileNum;
            else
                clone.transform.GetChild(1).GetComponent<TMP_Text>().text = alias;

            // Add our replacement component
            clone.AddComponent<SaveFileUISlot_BetterSaves>();

            // Set all the attributes of the clone
            SaveFileUISlot_BetterSaves slot = clone.GetComponent<SaveFileUISlot_BetterSaves>();
            if (slot != null)
            {
                slot.fileNum = fileNum;
                slot.fileString = "LCSaveFile" + fileNum;
            }
            else
            {
                Debug.LogError("SaveFileUISlot_BetterSaves component not found on the cloned GameObject.");
                Destroy(clone); // Clean up
                return;
            }

            // Adjust the position to make it visible and not overlap
            RectTransform rectTransform = clone.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                float x = rectTransform.anchoredPosition.x;
                float baseY = newFileButton.GetComponent<RectTransform>().anchoredPosition.y;
                float y = baseY - (rectTransform.sizeDelta.y * fileNum);
                rectTransform.anchoredPosition = new Vector2(x, y);
            }

            // Replace the functionality of the delete button with the one we want
            GameObject fileDeleteButton = clone.transform.GetChild(3).gameObject;
            DeleteFileButton_BetterSaves deleteButton = GameObject.Find("Canvas/MenuContainer/DeleteFileConfirmation/Panel/Delete").GetComponent<DeleteFileButton_BetterSaves>();
            fileDeleteButton.gameObject.GetComponent<Button>().onClick.AddListener(deleteButton.UpdateFileToDelete);

            fileDeleteButton.SetActive(false);

            // Create the rename file button
            slot.renameButton = CreateRenameFileButton(clone);
        }

        public static GameObject CreateRenameFileButton(GameObject fileNode)
        {
            try
            {
                GameObject deleteButton = fileNode.transform.GetChild(3).gameObject;

                GameObject renameButton = Instantiate(deleteButton, fileNode.transform);
                renameButton.name = "RenameButton";
                renameButton.GetComponent<Image>().sprite = renameSprite;

                // Remove the delete functionality from the button
                Button renameButtonComponent = renameButton.GetComponent<Button>();
                renameButtonComponent.onClick = new Button.ButtonClickedEvent();

                // Add the rename class to the button
                renameButton.AddComponent<RenameFileButton_BetterSaves>();
                RenameFileButton_BetterSaves renameButtonScript = renameButton.GetComponent<RenameFileButton_BetterSaves>();

                if (renameButtonScript != null)
                {
                    renameButtonComponent.onClick.AddListener(renameButtonScript.RenameFile);
                }
                else
                {
                    Debug.LogError("RenameFileButton_BetterSaves component not found on renameButton");
                }

                // Reposition the rename button
                RectTransform rectTransform = renameButton.GetComponent<RectTransform>();

                if (rectTransform != null)
                {
                    float x = rectTransform.localPosition.x + 20;
                    float y = rectTransform.localPosition.y;
                    rectTransform.localPosition = new Vector2(x, y);
                }

                renameButton.SetActive(false);

                return renameButton;
            }
            catch (Exception ex)
            {
                Debug.LogError("Error occurred while creating rename file button: " + ex.Message);
                return null;
            }
        }

        public static void UpdateFilesPanelRect(int numSaves)
        {
            try
            {
                RectTransform rect = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel")?.GetComponent<RectTransform>();

                if (rect == null)
                {
                    throw new Exception("Failed to find FilesPanel RectTransform.");
                }

                Vector2 sizeDelta = rect.sizeDelta;

                RectTransform file1Rect = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File1")?.GetComponent<RectTransform>();

                if (file1Rect == null)
                {
                    throw new Exception("Failed to find File1 RectTransform.");
                }

                float fileSlotHeight = file1Rect.sizeDelta.y;
                sizeDelta.y = fileSlotHeight * (numSaves + 3);

                rect.sizeDelta = sizeDelta;

                // Reposition the weekly run button
                GameObject weeklyRunButton = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/ChallengeMoonButton");
                RectTransform weeklyRunRect = weeklyRunButton.GetComponent<RectTransform>();
                if (weeklyRunRect != null)
                {
                    weeklyRunRect.anchorMin = new Vector2(0.5f, 0.05f);
                    weeklyRunRect.anchorMax = new Vector2(0.5f, 0.05f);
                    weeklyRunRect.pivot = new Vector2(0.5f, 0.05f);
                    weeklyRunRect.anchoredPosition = new Vector2(0f, 0f);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Error occurred while updating files panel rect: " + ex.Message);
            }
        }

        public static void RefreshNameFields()
        {
            SaveFileUISlot_BetterSaves[] saveFileSlots = GameObject.FindObjectsOfType<SaveFileUISlot_BetterSaves>();

            foreach (SaveFileUISlot_BetterSaves saveFileSlot in saveFileSlots)
            {
                string alias = ES3.Load("Alias_BetterSaves", saveFileSlot.fileString, "");
                if (alias == "")
                    saveFileSlot.transform.GetChild(1).GetComponent<TMP_Text>().text = "File " + (saveFileSlot.fileNum + 1);
                else
                    saveFileSlot.transform.GetChild(1).GetComponent<TMP_Text>().text = alias;
            }
        }
    }
}

public class NewFileUISlot_BetterSaves : MonoBehaviour
{
    public Animator buttonAnimator;
    public Button button;
    public bool isSelected;

    public void Awake()
    {
        buttonAnimator = GetComponent<Animator>();
        button = GetComponent<Button>();
        button.onClick.AddListener(SetFileToThis);
    }

    public void SetFileToThis()
    {
        string saveFileName = "LCSaveFile" + Plugin.newSaveFileNum;
        GameNetworkManager.Instance.currentSaveFileName = saveFileName;
        GameNetworkManager.Instance.saveFileNum = Plugin.newSaveFileNum;
        SetButtonColorForAllFileSlots();
        isSelected = true;
        SetButtonColor();
    }

    public void SetButtonColorForAllFileSlots()
    {
        SaveFileUISlot_BetterSaves[] saveFileSlots = FindObjectsOfType<SaveFileUISlot_BetterSaves>();
        foreach (SaveFileUISlot_BetterSaves slot in saveFileSlots)
        {
            slot.SetButtonColor();
            slot.deleteButton.SetActive(false);
            slot.renameButton.SetActive(false);
        }
    }

    public void SetButtonColor()
    {
        buttonAnimator.SetBool("isPressed", isSelected);
    }
}

public class SaveFileUISlot_BetterSaves : MonoBehaviour
{
    public Animator buttonAnimator;
    public Button button;
    public TextMeshProUGUI fileStatsText;
    public int fileNum;
    public string fileString;
    public TextMeshProUGUI fileNotCompatibleAlert;
    public GameObject deleteButton;
    public GameObject renameButton;

    public void Awake()
    {
        buttonAnimator = GetComponent<Animator>();
        button = GetComponent<Button>();
        button.onClick.AddListener(SetFileToThis);

        fileStatsText = transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        fileNotCompatibleAlert = transform.GetChild(4).GetComponent<TextMeshProUGUI>();

        deleteButton = transform.GetChild(3).gameObject;
    }

    public void Start()
    {
        UpdateStats();
    }

    private void OnEnable()
    {
        if (!FindObjectOfType<MenuManager>().filesCompatible[fileNum])
        {
            fileNotCompatibleAlert.enabled = true;
        }
    }

    public void UpdateStats()
    {
        try
        {
            if (ES3.FileExists(fileString))
            {
                int groupCredits = ES3.Load("GroupCredits", fileString, 30);
                int daysSpent = ES3.Load("Stats_DaysSpent", fileString, 0);
                fileStatsText.text = $"${groupCredits}\nDays: {daysSpent}";
            }
            else
            {
                fileStatsText.text = "";
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error updating stats: {ex.Message}");
        }
    }

    public void SetButtonColor()
    {
        buttonAnimator.SetBool("isPressed", GameNetworkManager.Instance.currentSaveFileName == fileString);
    }

    public void SetFileToThis()
    {
        Plugin.fileToModify = fileNum;

        GameNetworkManager.Instance.currentSaveFileName = fileString;
        GameNetworkManager.Instance.saveFileNum = fileNum;
        SetButtonColorForAllFileSlots();
    }

    public void SetButtonColorForAllFileSlots()
    {
        SaveFileUISlot_BetterSaves[] saveFileSlots = FindObjectsOfType<SaveFileUISlot_BetterSaves>();
        foreach (SaveFileUISlot_BetterSaves slot in saveFileSlots)
        {
            slot.SetButtonColor();
            slot.deleteButton.SetActive(slot == this);
            slot.renameButton.SetActive(slot == this);
        }

        NewFileUISlot_BetterSaves newFileButton = FindObjectOfType<NewFileUISlot_BetterSaves>();
        newFileButton.isSelected = false;
        newFileButton.SetButtonColor();
    }
}

public class RenameFileButton_BetterSaves : MonoBehaviour
{
    public void RenameFile()
    {
        string filePath = $"LCSaveFile{Plugin.fileToModify}";
        string alias = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/HostSettingsContainer/LobbyHostOptions/OptionsNormal/ServerNameField/Text Area/Text")
            .GetComponent<TMP_Text>().text;

        if (ES3.FileExists(filePath))
        {
            ES3.Save("Alias_BetterSaves", alias, filePath);
            Debug.Log("Granted alias " + alias + " to file " + filePath);
        }

        Plugin.RefreshNameFields();
    }
}

public class DeleteFileButton_BetterSaves : MonoBehaviour
{
    public int fileToDelete;
    public AudioClip deleteFileSFX;
    public TextMeshProUGUI deleteFileText;

    public void UpdateFileToDelete()
    {
        fileToDelete = Plugin.fileToModify;
        if (ES3.Load("Alias_BetterSaves", $"LCSaveFile{fileToDelete}", "") != "")
        {
            deleteFileText.text = $"Do you want to delete file ({ES3.Load("Alias_BetterSaves", $"LCSaveFile{fileToDelete}", "")})?";
        }
        else
        {
            deleteFileText.text = $"Do you want to delete File {fileToDelete + 1}?";
        }
    }

    public void DeleteFile()
    {
        string filePath = $"LCSaveFile{fileToDelete}";

        if (ES3.FileExists(filePath))
        {
            ES3.DeleteFile(filePath);
            FindObjectOfType<MenuManager>().MenuAudio.PlayOneShot(deleteFileSFX);
        }

        SaveFileUISlot_BetterSaves[] saveFileSlots = FindObjectsOfType<SaveFileUISlot_BetterSaves>(includeInactive: true);
        foreach (SaveFileUISlot_BetterSaves slot in saveFileSlots)
        {
            Debug.Log($"Deleted {fileToDelete}");
            if (slot.fileNum == fileToDelete)
            {
                slot.fileNotCompatibleAlert.enabled = false;
                FindObjectOfType<MenuManager>().filesCompatible[fileToDelete] = true;

                if (ES3.FileExists($"LGU_{fileToDelete}.json"))
                {
                    Debug.Log($"Deleting LGU file located at {filePath}");
                    ES3.DeleteFile($"LGU_{fileToDelete}.json");
                }
            }
        }

        Plugin.InitializeBetterSaves();
    }
}
