using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class ProjectSelectionTests
{
    private const string MainMenuScene = "MainMenu";
    private const string GameplayScene = "SampleScene";

    private string worldsDirectory;

    [SetUp]
    public void SetUp()
    {
        worldsDirectory = Path.Combine(
            Application.persistentDataPath,
            "Worlds"
        );

        Directory.CreateDirectory(worldsDirectory);

        DeleteTestWorlds();

        WorldSaveSystem.PendingWorldName = null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        DeleteTestWorlds();

        WorldSaveSystem.PendingWorldName = null;

        SceneManager.LoadScene("EmptyScene");
        yield return null;
        yield return null;
    }

    private void DeleteTestWorlds()
    {
        if (!Directory.Exists(worldsDirectory))
            return;

        string[] files = Directory.GetFiles(
            worldsDirectory,
            "TC_*.json"
        );

        foreach (string file in files)
        {
            File.Delete(file);
        }
    }

    private IEnumerator LoadMainMenu()
    {
        SceneManager.LoadScene(MainMenuScene);

        yield return null;
        yield return null;
    }

    private IEnumerator LoadGameplay()
    {
        SceneManager.LoadScene(GameplayScene);

        yield return null;
        yield return null;
    }

    private MenuTraversal GetMenu()
    {
        MenuTraversal menu =
            Object.FindFirstObjectByType<MenuTraversal>();

        Assert.IsNotNull(
            menu,
            "MenuTraversal was not found in MainMenu."
        );

        return menu;
    }

    // ---------------------------------------------------------
    // TC-1.1.1
    // Create a project
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_1_1_CreateProject()
    {
        yield return LoadMainMenu();

        MenuTraversal menu = GetMenu();

        Assert.IsNotNull(menu.worldNameInput);

        menu.worldNameInput.text = "TC_CreateProject";

        menu.createWorld();

        yield return null;

        Assert.AreEqual(
            GameplayScene,
            SceneManager.GetActiveScene().name
        );
    }

    // ---------------------------------------------------------
    // TC-1.1.2
    // Create multiple projects
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_1_2_CreateMultipleProjects()
    {
        string[] projectNames =
        {
            "TC_Project_A",
            "TC_Project_B",
            "TC_Project_C"
        };

        foreach (string projectName in projectNames)
        {
            WorldSaveSystem.PendingWorldName = projectName;

            yield return LoadGameplay();

            WorldSaveSystem saveSystem =
                Object.FindFirstObjectByType<WorldSaveSystem>();

            Assert.IsNotNull(saveSystem);

            saveSystem.SaveNow();

            yield return null;
        }

        string[] savedWorlds =
            WorldSaveSystem.GetSavedWorldNames();

        foreach (string projectName in projectNames)
        {
            Assert.Contains(
                projectName,
                savedWorlds
            );
        }
    }

    // ---------------------------------------------------------
    // TC-1.1.3
    // New projects enter the same environment
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_1_3_NewProjectsUseSameEnvironment()
    {
        WorldSaveSystem.PendingWorldName = "TC_Environment_A";

        yield return LoadGameplay();

        string firstScene =
            SceneManager.GetActiveScene().name;

        WorldSaveSystem.PendingWorldName = "TC_Environment_B";

        yield return LoadGameplay();

        string secondScene =
            SceneManager.GetActiveScene().name;

        Assert.AreEqual(GameplayScene, firstScene);
        Assert.AreEqual(GameplayScene, secondScene);
        Assert.AreEqual(firstScene, secondScene);
    }

    // ---------------------------------------------------------
    // TC-1.1 Integration
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_1_CanCreateNewProject()
    {
        WorldSaveSystem.PendingWorldName =
            "TC_IntegrationCreate";

        yield return LoadGameplay();

        WorldSaveSystem saveSystem =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        Assert.IsNotNull(saveSystem);

        Assert.AreEqual(
            "TC_IntegrationCreate",
            saveSystem.WorldName
        );

        Assert.AreEqual(
            GameplayScene,
            SceneManager.GetActiveScene().name
        );
    }

    // ---------------------------------------------------------
    // TC-1.2.1
    // Save a project
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_2_1_SaveProject()
    {
        WorldSaveSystem.PendingWorldName =
            "TC_SaveProject";

        yield return LoadGameplay();

        WorldSaveSystem saveSystem =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        Assert.IsNotNull(saveSystem);

        saveSystem.SaveNow();

        yield return null;

        Assert.Contains(
            "TC_SaveProject",
            WorldSaveSystem.GetSavedWorldNames()
        );
    }

    // ---------------------------------------------------------
    // TC-1.2.2
    // Name and save a project
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_2_2_NameAndSaveProject()
    {
        const string expectedName =
            "TC_CustomWorldName";

        WorldSaveSystem.PendingWorldName =
            expectedName;

        yield return LoadGameplay();

        WorldSaveSystem saveSystem =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        Assert.AreEqual(
            expectedName,
            saveSystem.WorldName
        );

        saveSystem.SaveNow();

        yield return null;

        Assert.Contains(
            expectedName,
            WorldSaveSystem.GetSavedWorldNames()
        );
    }

    // ---------------------------------------------------------
    // TC-1.2.3
    // View existing project list
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_2_3_ViewExistingProjects()
    {
        WorldSaveSystem.PendingWorldName =
            "TC_ListProject";

        yield return LoadGameplay();

        WorldSaveSystem saveSystem =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        saveSystem.SaveNow();

        yield return LoadMainMenu();

        MenuTraversal menu = GetMenu();

        menu.openLoad();

        yield return null;

        Assert.IsTrue(menu.LoadPanel.activeSelf);

        string[] saved =
            WorldSaveSystem.GetSavedWorldNames();

        Assert.Contains(
            "TC_ListProject",
            saved
        );
    }

    // ---------------------------------------------------------
    // TC-1.2.4
    // Saved project exists in filesystem
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_2_4_SaveExistsInFilesystem()
    {
        WorldSaveSystem.PendingWorldName =
            "TC_FileSystem";

        yield return LoadGameplay();

        WorldSaveSystem saveSystem =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        saveSystem.SaveNow();

        yield return null;

        string expectedPath = Path.Combine(
            worldsDirectory,
            "TC_FileSystem.json"
        );

        Assert.IsTrue(
            File.Exists(expectedPath),
            "Expected save file was not found: "
            + expectedPath
        );
    }

    // ---------------------------------------------------------
    // TC-1.2.5
    // Duplicate names must be rejected
    //
    // IMPORTANT:
    // TODO: Not Yet Implemented
    // ---------------------------------------------------------

    /*[UnityTest]
    public IEnumerator TC_1_2_5_DuplicateNameRejected()
    {
        const string name = "TC_Duplicate";

        WorldSaveSystem.PendingWorldName = name;

        yield return LoadGameplay();

        WorldSaveSystem first =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        first.SaveNow();

        yield return null;

        int before =
            WorldSaveSystem.GetSavedWorldNames()
                .Count(x => x == name);

        Assert.AreEqual(1, before);

        yield return LoadMainMenu();

        MenuTraversal menu = GetMenu();

        menu.worldNameInput.text = name;

        // Requirement:
        // duplicate creation should be rejected.
        menu.createWorld();

        yield return null;

        Assert.AreEqual(
            MainMenuScene,
            SceneManager.GetActiveScene().name,
            "Duplicate world name was accepted. " +
            "SR-1.2 requires duplicate names to be rejected."
        );
    }*/

    // ---------------------------------------------------------
    // TC-1.2 Integration
    // Multiple projects
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_2_CanHoldMultipleProjects()
    {
        string[] names =
        {
            "TC_Multiple_1",
            "TC_Multiple_2",
            "TC_Multiple_3"
        };

        foreach (string name in names)
        {
            WorldSaveSystem.PendingWorldName = name;

            yield return LoadGameplay();

            WorldSaveSystem saveSystem =
                Object.FindFirstObjectByType<WorldSaveSystem>();

            saveSystem.SaveNow();

            yield return null;
        }

        string[] saved =
            WorldSaveSystem.GetSavedWorldNames();

        foreach (string name in names)
        {
            Assert.Contains(name, saved);
        }
    }

    // ---------------------------------------------------------
    // TC-1.4.1
    // Blank save loads default world
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_4_1_BlankSaveLoadsDefaultWorld()
    {
        WorldSaveSystem.PendingWorldName =
            "TC_BlankWorld";

        yield return LoadGameplay();

        Assert.AreEqual(
            GameplayScene,
            SceneManager.GetActiveScene().name
        );

        WorldSaveSystem saveSystem =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        Assert.IsNotNull(saveSystem);

        Assert.AreEqual(
            "TC_BlankWorld",
            saveSystem.WorldName
        );
    }

    // ---------------------------------------------------------
    // TC-1.4 Integration
    // Existing save can open
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_4_OpenExistingSave()
    {
        const string name =
            "TC_OpenExisting";

        WorldSaveSystem.PendingWorldName = name;

        yield return LoadGameplay();

        WorldSaveSystem saveSystem =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        saveSystem.SaveNow();

        yield return LoadMainMenu();

        WorldSaveSystem.PendingWorldName = name;

        yield return LoadGameplay();

        WorldSaveSystem loaded =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        Assert.IsNotNull(loaded);
        Assert.AreEqual(name, loaded.WorldName);
    }

    // ---------------------------------------------------------
    // TC-1 System
    // Project Selection
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_1_ProjectSelectionSystem()
    {
        const string name =
            "TC_SystemProject";

        WorldSaveSystem.PendingWorldName = name;

        yield return LoadGameplay();

        WorldSaveSystem saveSystem =
            Object.FindFirstObjectByType<WorldSaveSystem>();

        Assert.IsNotNull(saveSystem);

        saveSystem.SaveNow();

        yield return null;

        Assert.Contains(
            name,
            WorldSaveSystem.GetSavedWorldNames()
        );

        yield return LoadMainMenu();

        MenuTraversal menu = GetMenu();

        menu.openLoad();

        yield return null;

        Assert.IsTrue(menu.LoadPanel.activeSelf);
    }
}