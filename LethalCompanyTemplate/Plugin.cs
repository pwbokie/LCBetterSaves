using BepInEx;
using HarmonyLib;
using LCBetterSaves;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LCBetterSaves
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private Harmony _harmony = new Harmony("BetterSaves");
        public static int fileToDelete = -1;
        public static int newSaveFileNum;

        private void Awake()
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
            InitializeBetterSaves(menuManager);
        }


        public static AudioClip deleteFileSFX;
        public static TextMeshProUGUI deleteFileText;

        public static void InitializeBetterSaves(MenuManager __instance)
        {
            // Relabel the top of the save files box with the plugin name & version
            GameObject panelLabel = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/EnterAName");
            panelLabel.GetComponent<TextMeshProUGUI>().text = "BetterSaves " + PluginInfo.PLUGIN_VERSION;

            // Steal the values from the old DeleteFileButton
            GameObject deleteFileGO = GameObject.Find("Canvas/MenuContainer/DeleteFileConfirmation/Panel/Delete");
            if (deleteFileGO != null)
            {
                DeleteFileButton oldDeleteFileButton = deleteFileGO.GetComponent<DeleteFileButton>();
                
                // Steal the deleteFileSFX if it's not already set
                if (deleteFileSFX == null)
                {
                    deleteFileSFX = oldDeleteFileButton.deleteFileSFX;
                }

                // Steal the deleteFileText if it's not already set
                if (deleteFileText == null)
                {
                    deleteFileText = oldDeleteFileButton.deleteFileText;
                }

                // Get rid of the old DeleteFileButton component
                Destroy(oldDeleteFileButton);
            }

            // Create the modded DeleteFileButton component
            if (deleteFileGO.GetComponent<DeleteFileButton_BetterSaves>() == null)
            {
                DeleteFileButton_BetterSaves deleteButton = deleteFileGO.AddComponent<DeleteFileButton_BetterSaves>();
                deleteButton.deleteFileSFX = deleteFileSFX;
                deleteButton.deleteFileText = deleteFileText;

                // Update the button's onClick event
                deleteFileGO.GetComponent<Button>().onClick.RemoveAllListeners();
                deleteFileGO.GetComponent<Button>().onClick.AddListener(deleteButton.DeleteFile);
            }

            // Show the original file node
            GameObject originalFileNode = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File1");
            originalFileNode.SetActive(true);

            // Create the new file node
            NewFileUISlot_BetterSaves newFileSlot = CreateNewFileNode();

            // Refresh the save buttons
            RefreshSaveButtons();

            // Remove all the old save buttons
            DestroyOriginalSaveButtons();

            // Hide the original file node
            originalFileNode.SetActive(false);

            // Set the new file node as the selected file
            FindObjectOfType<NewFileUISlot_BetterSaves>().SetFileToThis();
        }


        public static List<string> NormalizeFileNames()
        {
            // Retrieve and filter the save files
            List<string> saveFiles = new List<string>();
            foreach (string file in ES3.GetFiles())
            {
                if (ES3.FileExists(file) && file.StartsWith("LCSaveFile"))
                {
                    Debug.Log("Found file: " + file);
                    saveFiles.Add(file);
                }
            }

            // Rename all files to temporary names
            int tempIndex = 0;
            foreach (string file in saveFiles)
            {
                string tempName = "TempFile" + tempIndex.ToString();
                ES3.RenameFile(file, tempName);
                Debug.Log($"Renamed {file} to {tempName}");
                tempIndex++;
            }

            // Rename temporary files to normalized names
            int fileIndex = 0;
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

            return newFiles;
        }

        public static NewFileUISlot_BetterSaves CreateNewFileNode()
        {
            GameObject originalFileNode = GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File1");

            if (originalFileNode == null)
            {
                Debug.LogError("Original GameObject not found.");
                return null;
            }

            Transform parent = originalFileNode.transform.parent;

            // Sanitize the original of old code
            if (originalFileNode.GetComponent<SaveFileUISlot>() != null)
            {
                Destroy(originalFileNode.GetComponent<SaveFileUISlot>());
            }

            GameObject clone = Instantiate(originalFileNode, parent);
            clone.name = "NewFile";
            clone.SetActive(true);
            clone.transform.GetChild(1).GetComponent<TMP_Text>().text = "New File";

            // Add our replacement component
            NewFileUISlot_BetterSaves newFileSlot = clone.AddComponent<NewFileUISlot_BetterSaves>();

            // Destroy the DeleteFileButton
            Destroy(clone.transform.GetChild(3).gameObject);

            return newFileSlot;
        }

        // Instantiate a Node based on the original File1 GO
        public static void CreateModdedSaveNode(int fileIndex, int listIndex)
        {
            // fileIndex is the number tracked by the save file name
            // fileNum is the display for fileIndex
            // listIndex is the # save file in the list of saves we made
            int fileNum = fileIndex + 1;

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
            clone.SetActive(true);
            clone.name = "File" + fileNum + "_BetterSaves";
            clone.transform.GetChild(1).GetComponent<TMP_Text>().text = "File " + fileNum;

            // Add our replacement component
            clone.AddComponent<SaveFileUISlot_BetterSaves>();

            // Set all the attributes of the clone
            SaveFileUISlot_BetterSaves slot = clone.GetComponent<SaveFileUISlot_BetterSaves>();
            if (slot != null)
            {
                slot.fileNum = fileIndex;
                slot.fileString = "LCSaveFile" + fileIndex;
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
                Vector2 size = rectTransform.sizeDelta;
                rectTransform.anchoredPosition += new Vector2(0, (-size.y * (listIndex + 1)));
            }

            // Replace the functionality of the delete button with the one we want
            GameObject fileDeleteButton = clone.transform.GetChild(3).gameObject;
            DeleteFileButton_BetterSaves deleteButton = GameObject.Find("Canvas/MenuContainer/DeleteFileConfirmation/Panel/Delete").GetComponent<DeleteFileButton_BetterSaves>();
            fileDeleteButton.gameObject.GetComponent<Button>().onClick.AddListener(deleteButton.UpdateFileToDelete);

            fileDeleteButton.SetActive(false);
        }

        public static void DestroyOriginalSaveButtons()
        {
            // file 1 is used as the template and enabled/disabled as needed - leave it alive
            Destroy(GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File2"));
            Destroy(GameObject.Find("Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File3"));
        }

        public static void RefreshSaveButtons()
        {
            DestroyBetterSaveButtons();

            // If save file names aren't in perfect numerical order, we remedy that here.
            List<string> saveFiles = NormalizeFileNames();

            // A new save file would be created at [0, 1, 2] - index 3 - there are 3 items
            newSaveFileNum = saveFiles.Count;

            menuManager.filesCompatible = new bool[16];

            for (int i = 0; i < menuManager.filesCompatible.Length; i++)
            {
                menuManager.filesCompatible[i] = true;
            }

            // "File N" buttons, which allow selecting existing files.
            for (int i = 0; i < saveFiles.Count; i++)
            {
                CreateModdedSaveNode(int.Parse(saveFiles[i].Replace("LCSaveFile", "")), i);
            }
        }

        public static void DestroyBetterSaveButtons()
        {
            foreach(SaveFileUISlot_BetterSaves f in FindObjectsOfType<SaveFileUISlot_BetterSaves>())
            {
                Destroy(f.gameObject);
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

    public void Awake()
    {
        buttonAnimator = GetComponent<Animator>();
        button = GetComponent<Button>();
        button.onClick.AddListener(SetFileToThis);

        fileStatsText = transform.GetChild(2).GetComponent<TextMeshProUGUI>();
        fileNotCompatibleAlert = transform.GetChild(4).GetComponent<TextMeshProUGUI>();

        deleteButton = transform.GetChild(3).gameObject;
    }

    private void OnEnable()
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

        if (!FindObjectOfType<MenuManager>().filesCompatible[fileNum])
        {
            fileNotCompatibleAlert.enabled = true;
        }
    }

    public void SetButtonColor()
    {
        buttonAnimator.SetBool("isPressed", GameNetworkManager.Instance.currentSaveFileName == fileString);
    }

    public void SetFileToThis()
    {
        Plugin.fileToDelete = fileNum;

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
        }

        NewFileUISlot_BetterSaves newFileButton = FindObjectOfType<NewFileUISlot_BetterSaves>();
        newFileButton.isSelected = false;
        newFileButton.SetButtonColor();
    }
}

public class DeleteFileButton_BetterSaves : MonoBehaviour
{
    public int fileToDelete;
    public AudioClip deleteFileSFX;
    public TextMeshProUGUI deleteFileText;

    public void UpdateFileToDelete()
    {
        fileToDelete = Plugin.fileToDelete;
        deleteFileText.text = $"Do you want to delete File {fileToDelete + 1}?";
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
            }
        }

        FindObjectOfType<NewFileUISlot_BetterSaves>().SetFileToThis();

        Destroy(GameObject.Find($"Canvas/MenuContainer/LobbyHostSettings/FilesPanel/File{fileToDelete + 1}_BetterSaves"));

        Plugin.RefreshSaveButtons();
    }
}