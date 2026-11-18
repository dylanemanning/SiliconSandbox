using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

// Post-UAT player changes: Minecraft-like movement, crouch, flight speed,
// floor targeting and wire hit shapes.
public class PlayerMovementAndTargetingTests : InputTestFixture
{
    private Keyboard keyboard;
    private Mouse mouse;
    private Player player;
    private Rigidbody body;

    public override void Setup()
    {
        base.Setup();
        keyboard = InputSystem.AddDevice<Keyboard>();
        mouse = InputSystem.AddDevice<Mouse>();
        foreach (string key in new[] { "Keybind.MoveForward", "Keybind.Jump", "Keybind.FlyDown", "Keybind.Sprint", "Keybind.Pause" })
            PlayerPrefs.DeleteKey(key);
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        Time.timeScale = 1;
        WorldSaveSystem.PendingWorldName = null;
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        yield return null;
    }

    private IEnumerator LoadGameplay()
    {
        WorldSaveSystem.PendingWorldName = "TC_PlayerMovement_" + System.Guid.NewGuid().ToString("N");
        yield return SceneManager.LoadSceneAsync("SampleScene");
        yield return null;
        yield return null;
        player = Object.FindFirstObjectByType<Player>();
        body = player.GetComponent<Rigidbody>();
        Set(mouse.delta, Vector2.zero);
    }

    private IEnumerator Settle()
    {
        body.position = new Vector3(5.5f, 1f, 5.5f);
        body.linearVelocity = Vector3.zero;
        yield return new WaitForSeconds(1f);
    }

    private static void SetField(object target, string name, object value) =>
        typeof(Player).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

    [UnityTest]
    public IEnumerator WalkingIsFiftyPercentFasterAndJumpsRiseAboutOneBlock()
    {
        yield return LoadGameplay();
        Assert.That(player.speed, Is.EqualTo(7.5f), "Walking is 50% faster than the previous 5 blocks/s.");
        Assert.That(player.reachDistance, Is.EqualTo(15f));
        yield return Settle();

        Press(keyboard.wKey);
        float until = Time.time + .6f;
        while (Time.time < until) yield return null;
        Vector3 velocity = body.linearVelocity;
        Release(keyboard.wKey);
        Assert.That(new Vector2(velocity.x, velocity.z).magnitude, Is.EqualTo(7.5f).Within(.3f));
        until = Time.time + .5f;
        while (Time.time < until) yield return null;

        float startY = body.position.y, peak = startY;
        Press(keyboard.spaceKey); yield return null; yield return null;
        Release(keyboard.spaceKey);
        float jumpStart = Time.time;
        bool rose = false;
        float landed = -1f;
        while (Time.time - jumpStart < 1.5f)
        {
            yield return null;
            peak = Mathf.Max(peak, body.position.y);
            if (body.position.y > startY + .2f) rose = true;
            if (rose && body.position.y <= startY + .02f) { landed = Time.time - jumpStart; break; }
        }
        Assert.That(peak - startY, Is.EqualTo(player.jumpHeight).Within(.25f), "Jump height is about 1.25 blocks.");
        Assert.That(landed, Is.GreaterThan(0f).And.LessThan(.8f), "A jump lands quickly instead of gliding.");
    }

    [UnityTest]
    public IEnumerator CtrlScrollSetsFlightSpeedBetweenWalkingPaceAndTripleTheDefault()
    {
        yield return LoadGameplay();
        Assert.That(player.flightSpeed, Is.EqualTo(player.speed * 2f), "Default flight is twice walking speed (200% of the old flight speed).");
        Assert.That(player.flightStep, Is.EqualTo(4.5f), "Vertical flight is 350% faster than the previous 1 block/s.");
        typeof(Player).GetMethod("ToggleFlight", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
        Assert.That(player.IsFlying, Is.True);
        int selected = player.hotbar.SelectedSlotIndex;
        Press(keyboard.leftCtrlKey); yield return null;
        for (int i = 0; i < 40; i++)
        {
            Set(mouse.scroll, new Vector2(0, 120)); yield return null;
            Set(mouse.scroll, Vector2.zero); yield return null;
        }
        Assert.That(player.CurrentFlightSpeed, Is.EqualTo(player.flightSpeed * 3f).Within(.01f));
        for (int i = 0; i < 40; i++)
        {
            Set(mouse.scroll, new Vector2(0, -120)); yield return null;
            Set(mouse.scroll, Vector2.zero); yield return null;
        }
        Assert.That(player.CurrentFlightSpeed, Is.EqualTo(player.speed).Within(.01f));
        Assert.That(player.hotbar.SelectedSlotIndex, Is.EqualTo(selected), "Ctrl + scroll leaves the hotbar alone.");
        Release(keyboard.leftCtrlKey); yield return null;

        // macOS reports Shift + wheel as horizontal scrolling: it still moves the hotbar while descending.
        Press(keyboard.leftShiftKey); yield return null;
        Set(mouse.scroll, new Vector2(-120, 0)); yield return null;
        Set(mouse.scroll, Vector2.zero); yield return null;
        Assert.That(player.hotbar.SelectedSlotIndex, Is.EqualTo((selected + 1) % player.hotbar.SlotCount));
        Release(keyboard.leftShiftKey); yield return null;
    }

    [UnityTest]
    public IEnumerator CrouchLowersTheViewToHalfHeight()
    {
        yield return LoadGameplay();
        yield return Settle();
        var view = player.cameraSettings.camera.transform;
        float standing = view.localPosition.y;
        Press(keyboard.leftShiftKey);
        float until = Time.time + .4f;
        while (Time.time < until) yield return null;
        Assert.That(player.IsCrouching, Is.True);
        Assert.That(view.localPosition.y, Is.EqualTo(standing * .5f).Within(.01f));
        Release(keyboard.leftShiftKey);
        until = Time.time + .4f;
        while (Time.time < until) yield return null;
        Assert.That(view.localPosition.y, Is.EqualTo(standing).Within(.01f));
    }

    [UnityTest]
    public IEnumerator FloorHighlightOutlinesTheTargetedGridCell()
    {
        yield return LoadGameplay();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.None;
        body.position = new Vector3(3.3f, 4f, -2.2f);
        player.transform.position = body.position;
        SetField(player, "xRotation", 90f);
        yield return new WaitForSeconds(.1f);
        Physics.SyncTransforms();
        yield return null;
        Assert.That(player.transform.position.x, Is.EqualTo(3.3f).Within(.01f));
        Assert.That(player.blockHighlighter.activeSelf, Is.True);
        Assert.That(player.blockHighlighter.transform.position, Is.EqualTo(new Vector3(3, 0, -2)));
        Assert.That(player.blockHighlighter.transform.localScale.x, Is.EqualTo(1.02f).Within(.001f));
    }

    [UnityTest]
    public IEnumerator WireHitShapeIsOnlyTheVisibleWire()
    {
        yield return SceneManager.LoadSceneAsync("EmptyScene");
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.transform.position = new Vector3(500, 500, 500);
        var wireObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wireObject.transform.position = new Vector3(500, 501, 500);
        wireObject.AddComponent<Block>();
        var wire = wireObject.AddComponent<LogicWireBlock>();
        wire.SetEndpointMask(LogicWireBlock.GetDirectionBit(Vector3Int.left) | LogicWireBlock.GetDirectionBit(Vector3Int.right));
        yield return null;
        Physics.SyncTransforms();
        try
        {
            Assert.That(Physics.Raycast(new Vector3(500, 510, 500), Vector3.down, out var center, 20), Is.True);
            Assert.That(center.collider.GetComponentInParent<LogicWireBlock>(), Is.EqualTo(wire));
            Assert.That(Physics.Raycast(new Vector3(500.4f, 510, 500), Vector3.down, out var arm, 20), Is.True);
            Assert.That(arm.collider.GetComponentInParent<LogicWireBlock>(), Is.EqualTo(wire), "The arm along X is solid.");
            Assert.That(Physics.Raycast(new Vector3(500, 510, 500.4f), Vector3.down, out var gap, 20), Is.True);
            Assert.That(gap.collider.gameObject, Is.EqualTo(floor), "The empty part of the wire cell is see-through.");
        }
        finally { Object.Destroy(floor); Object.Destroy(wireObject); }
    }
}
