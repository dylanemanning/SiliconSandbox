using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class UserControlTests : InputTestFixture
{
    private const string GameplayScene = "SampleScene";

    private Keyboard keyboard;
    private Mouse mouse;

    public override void Setup()
    {
        base.Setup();

        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();

        PlayerPrefs.DeleteKey("Keybind.MoveForward");
        PlayerPrefs.DeleteKey("Keybind.MoveBackward");
        PlayerPrefs.DeleteKey("Keybind.MoveLeft");
        PlayerPrefs.DeleteKey("Keybind.MoveRight");
        PlayerPrefs.DeleteKey("Keybind.Pause");
    }

    public override void TearDown()
    {
        if (keyboard != null) InputSystem.ResetDevice(keyboard);

        if (mouse != null) InputSystem.ResetDevice(mouse);

        Time.timeScale = 1f;

        base.TearDown();
    }

    private IEnumerator LoadGame()
    {
        WorldSaveSystem.PendingWorldName =
            "TC_ControlTest";

        SceneManager.LoadScene(GameplayScene);

        yield return null;
        yield return null;
    }

    // ---------------------------------------------------------
    // TC-8.1.2
    // WASD movement and camera look
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_8_1_2_PlayerMovesWithWASD()
    {
        yield return LoadGame();

        Player player =
            Object.FindFirstObjectByType<Player>();

        Assert.IsNotNull(player);

        Rigidbody rb =
            player.GetComponent<Rigidbody>();

        Assert.IsNotNull(rb);

        Press(keyboard.wKey);

        yield return null;
        yield return new WaitForFixedUpdate();

        Vector3 forwardVelocity =
            rb.linearVelocity;

        Release(keyboard.wKey);

        Assert.Greater(
            forwardVelocity.magnitude,
            0.01f,
            "W did not cause player movement."
        );

        Press(keyboard.sKey);

        yield return null;
        yield return new WaitForFixedUpdate();

        Vector3 backwardVelocity =
            rb.linearVelocity;

        Release(keyboard.sKey);

        Assert.Greater(
            backwardVelocity.magnitude,
            0.01f,
            "S did not cause player movement."
        );

        Press(keyboard.aKey);

        yield return null;
        yield return new WaitForFixedUpdate();

        Vector3 leftVelocity =
            rb.linearVelocity;

        Release(keyboard.aKey);

        Assert.Greater(
            leftVelocity.magnitude,
            0.01f,
            "A did not cause player movement."
        );

        Press(keyboard.dKey);

        yield return null;
        yield return new WaitForFixedUpdate();

        Vector3 rightVelocity =
            rb.linearVelocity;

        Release(keyboard.dKey);

        Assert.Greater(
            rightVelocity.magnitude,
            0.01f,
            "D did not cause player movement."
        );
    }

    // ---------------------------------------------------------
    // TC-8.3.1
    // Mouse movement corresponds to camera movement
    // ---------------------------------------------------------

    [UnityTest]
    public IEnumerator TC_8_3_1_MouseMovesCamera()
    {
        yield return LoadGame();

        Player player =
            Object.FindFirstObjectByType<Player>();

        Assert.IsNotNull(player);
        Assert.IsNotNull(player.cameraSettings.camera);

        Quaternion initialPlayerRotation =
            player.transform.rotation;

        Quaternion initialCameraRotation =
            player.cameraSettings.camera.transform.localRotation;

        InputSystem.QueueStateEvent(
            mouse,
            new MouseState
            {
                position = new Vector2(500, 500),
                delta = new Vector2(100, 50)
            }
        );

        InputSystem.Update();

        yield return null;

        bool playerRotated =
            Quaternion.Angle(
                initialPlayerRotation,
                player.transform.rotation
            ) > 0.01f;

        bool cameraRotated =
            Quaternion.Angle(
                initialCameraRotation,
                player.cameraSettings.camera.transform.localRotation
            ) > 0.01f;

        Assert.IsTrue(
            playerRotated || cameraRotated,
            "Mouse movement did not change camera/player rotation."
        );
    }
}