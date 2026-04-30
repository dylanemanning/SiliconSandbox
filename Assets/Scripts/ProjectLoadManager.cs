using UnityEngine;
using System.IO;
using TMPro;
using UnityEngine.UI; // ADD THIS for the Button component
using UnityEngine.SceneManagement; // ADD THIS to switch scenes

public class ProjectLoadManager : MonoBehaviour
{
    public GameObject buttonPrefab;
    public Transform contentArea;
    public string gameplaySceneName = "Environment"; // Match your teammate's scene name

    void OnEnable()
    {
        RefreshList();
    }

    public void RefreshList()
    {
        foreach (Transform child in contentArea) {
            Destroy(child.gameObject);
        }

        string path = Path.Combine(Application.persistentDataPath, "SavedProjects");

        if (Directory.Exists(path))
        {
            string[] files = Directory.GetFiles(path, "*.json");
            foreach (string file in files)
            {
                GameObject newButton = Instantiate(buttonPrefab, contentArea);
                string projectName = Path.GetFileNameWithoutExtension(file);
                newButton.GetComponentInChildren<TMP_Text>().text = projectName;

                Button btn = newButton.GetComponent<Button>();
                btn.onClick.AddListener(() => LoadThisProject(projectName));

                Transform deleteBtnTransform = newButton.transform.Find("DeleteButton");
                if (deleteBtnTransform != null)
                {
                    Button deleteBtn = deleteBtnTransform.GetComponent<Button>();
                    deleteBtn.onClick.AddListener(() => DeleteProject(projectName));
                }
            }
        }
    }

    public void DeleteProject(string projectName)
    {
        string path = Path.Combine(Application.persistentDataPath, "SavedProjects", projectName + ".json");
        
        if (File.Exists(path))
        {
            File.Delete(path);
            Debug.Log("Deleted project: " + projectName);
            RefreshList(); // Refresh the UI so the button disappears
        }
    }

    void LoadThisProject(string name)
    {
        Debug.Log("Attempting to load project: " + name);
        
        // 1. You can save the name to PlayerPrefs so the next scene knows which file to open
        PlayerPrefs.SetString("CurrentProjectToLoad", name);

        // 2. Use your teammate's scene switching logic
        SceneManager.LoadScene(gameplaySceneName);
    }
}