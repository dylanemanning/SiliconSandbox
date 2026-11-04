using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public class WireRoutingIntegrationTests : InputTestFixture
{
    private static readonly Vector3Int Origin = new Vector3Int(16000, 16000, 16000);
    private readonly List<GameObject> objects = new List<GameObject>();
    private Player player;
    private Block prefab;
    private Keyboard keyboard;
    private Mouse mouse;
    private WorldSaveSystem previousSaveSystem;
    private bool previousSaveEnabled;
    private bool replacedSaveSystem;
    private bool previousPaused;
    private float previousTimeScale;
    private readonly Dictionary<string, int> intPreferences = new Dictionary<string, int>();
    private readonly Dictionary<string, string> stringPreferences = new Dictionary<string, string>();
    private const string Prefix = "Routing regression ";

    public override void Setup()
    {
        base.Setup();
        replacedSaveSystem = false;
        keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
        previousPaused = PauseManager.IsPaused; previousTimeScale = Time.timeScale;
        typeof(PauseManager).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
        Time.timeScale = 1;
        InventoryGrid.Close();
        foreach (string key in new[] { "Keybind.PlaceMouseButton", "Keybind.BreakMouseButton", "Keybind.Pause" })
        { if (PlayerPrefs.HasKey(key)) intPreferences.Add(key, PlayerPrefs.GetInt(key)); PlayerPrefs.DeleteKey(key); }
        foreach (string key in new[] { KeybindSettings.ManualWirePreference, KeybindSettings.DeleteWireRunPreference })
        { if (PlayerPrefs.HasKey(key)) stringPreferences.Add(key, PlayerPrefs.GetString(key)); PlayerPrefs.DeleteKey(key); }
        var playerObject = Own(new GameObject(Prefix + "player")); playerObject.SetActive(false);
        player = playerObject.AddComponent<Player>();
        var template = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); template.name = Prefix + "wire";
        template.SetActive(false); prefab = template.AddComponent<Block>(); template.AddComponent<LogicWireBlock>();
    }

    public override void TearDown()
    {
        foreach (var block in Object.FindObjectsByType<Block>(FindObjectsSortMode.None))
            if (block != null && block.name.StartsWith(Prefix, StringComparison.Ordinal)) Object.DestroyImmediate(block.gameObject);
        foreach (var value in objects) if (value != null) Object.DestroyImmediate(value);
        objects.Clear();
        if (replacedSaveSystem)
        {
            typeof(WorldSaveSystem).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousSaveSystem);
            if (previousSaveSystem != null) previousSaveSystem.enabled = previousSaveEnabled;
        }
        typeof(PauseManager).GetField("<IsPaused>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, previousPaused);
        Time.timeScale = previousTimeScale;
        foreach (var pair in intPreferences) PlayerPrefs.SetInt(pair.Key, pair.Value);
        foreach (var pair in stringPreferences) PlayerPrefs.SetString(pair.Key, pair.Value);
        intPreferences.Clear(); stringPreferences.Clear();
        base.TearDown();
    }

    private GameObject Own(GameObject value) { objects.Add(value); return value; }
    private Block Surface(Vector3Int cell)
    {
        var value = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); value.name = Prefix + "surface";
        value.transform.position = cell; return value.AddComponent<Block>();
    }
    private LogicWireBlock Wire(Vector3Int cell, int mask = 0)
    {
        var value = Own(GameObject.CreatePrimitive(PrimitiveType.Cube)); value.name = Prefix + "existing";
        value.transform.position = cell; value.AddComponent<Block>();
        var wire = value.AddComponent<LogicWireBlock>(); wire.SetEndpointMask(mask); return wire;
    }
    private void Aim(Block block, Vector3Int normal)
    {
        Physics.SyncTransforms();
        var center = block.GetComponent<Collider>().bounds.center;
        Assert.That(block.GetComponent<Collider>().Raycast(new Ray(center + (Vector3)normal * 4, -normal), out var hit, 8), Is.True);
        Set("targetBlock", block); Set("targetRaycastHit", hit);
    }
    private void Set(string name, object value) => typeof(Player).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, value);
    private T Get<T>(string name) => (T)typeof(Player).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(player);
    private object Call(string name, params object[] args) => typeof(Player).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, args);
    private int ActiveWires() => Object.FindObjectsByType<LogicWireBlock>(FindObjectsSortMode.None).Count(w => w.name.StartsWith(Prefix, StringComparison.Ordinal));
    private IEnumerator AwaitRoute()
    {
        for (int frame = 0; frame < 180 && player.IsWireSearchPending; frame++)
        { Call("PollWireSearch"); yield return null; }
        Assert.That(player.IsWireSearchPending, Is.False, "Bounded route search did not finish within 180 frames.");
    }

    private void ActivatePlayer(Vector3 position)
    {
        // Isolate persistence without bypassing Player.Start or Update. A disabled
        // test save service records dirty notifications but never writes a world.
        previousSaveSystem = WorldSaveSystem.Instance;
        if (previousSaveSystem != null) { previousSaveEnabled = previousSaveSystem.enabled; previousSaveSystem.enabled = false; }
        typeof(WorldSaveSystem).GetField("<Instance>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, null);
        replacedSaveSystem = true;
        string previousPending = WorldSaveSystem.PendingWorldName;
        WorldSaveSystem.PendingWorldName = "WireInputRegression_" + Guid.NewGuid().ToString("N");
        var save = WorldSaveSystem.GetOrCreate(new[] { prefab }, null);
        Own(save.gameObject); save.enabled = false;
        WorldSaveSystem.PendingWorldName = previousPending;
        var body = player.gameObject.AddComponent<Rigidbody>(); body.useGravity = false; body.constraints = RigidbodyConstraints.FreezePosition;
        var cameraObject = Own(new GameObject(Prefix + "camera")); cameraObject.transform.SetParent(player.transform, false);
        var camera = cameraObject.AddComponent<Camera>();
        player.cameraSettings = new Player.CameraSettings { camera = camera };
        player.reachDistance = 10; player.transform.position = position;
        Set("xRotation", 90f);
        var hotbarObject = Own(new GameObject(Prefix + "hotbar")); hotbarObject.SetActive(false);
        var hotbar = hotbarObject.AddComponent<HotbarManager>();
        var slot = hotbarObject.AddComponent<HotbarSlot>();
        typeof(HotbarSlot).GetField("blockPrefab", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(slot, prefab);
        typeof(HotbarManager).GetField("slots", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(hotbar, new[] { slot });
        player.hotbar = hotbar;
        player.gameObject.SetActive(true);
        Physics.SyncTransforms();
    }

    private IEnumerator WaitForGesture(bool pending = false)
    {
        for (int frame = 0; frame < 5 && !(pending ? player.IsWireSearchPending : player.HasWireGesture); frame++) yield return null;
        Assert.That(pending ? player.IsWireSearchPending : player.HasWireGesture, Is.True,
            $"Gesture missing: paused={PauseManager.IsPaused}, inventory={InventoryGrid.IsOpen}, target={Get<Block>("targetBlock")}, status={player.WireStatus}, button={KeybindSettings.PlaceMouseButton}");
    }

    [UnityTest]
    public IEnumerator ActivePlayerMouseClicksAndEscapeCancelBeforeCompletedResultPublishes()
    {
        Surface(Origin); Surface(Origin + Vector3Int.right * 4);
        // Seal the start cell so the worker must reject or exhaust its bounded
        // search while normal Update/input frames continue.
        foreach (var direction in new[] { Vector3Int.left, Vector3Int.right, Vector3Int.forward, Vector3Int.back, Vector3Int.up })
            Surface(Origin + Vector3Int.up + direction);
        ActivatePlayer(Origin + Vector3.up * 1.2f);
        yield return null; yield return null;
        Press(mouse.rightButton); yield return WaitForGesture();
        Assert.That(player.HasWireGesture, Is.True);
        Assert.That(ActiveWires(), Is.Zero, "First actual click must not publish a wire.");
        Release(mouse.rightButton); yield return null;
        player.transform.position = Origin + Vector3.right * 4 + Vector3.up * 5;
        yield return null;
        Press(mouse.rightButton); yield return WaitForGesture(true);
        Assert.That(player.IsWireSearchPending, Is.True);
        Press(keyboard.escapeKey); yield return null; yield return null;
        Assert.That(player.HasWireGesture, Is.False);
        Assert.That(PauseManager.IsPaused, Is.False, "Escape is consumed by cancellation before pause.");
        Assert.That(ActiveWires(), Is.Zero);
        Release(mouse.rightButton); Release(keyboard.escapeKey);
        yield return null;
        Assert.That(ActiveWires(), Is.Zero, "A late worker completion cannot publish after Escape.");
    }

    [UnityTest]
    public IEnumerator ActivePlayerManualKeyPressAndReleasePlacesOneCell()
    {
        Surface(Origin); ActivatePlayer(Origin + Vector3.up * 5);
        yield return null; yield return null;
        Press(keyboard.rKey); yield return WaitForGesture();
        Assert.That(player.HasWireGesture, Is.True);
        Assert.That(ActiveWires(), Is.Zero, "Held manual gesture stays provisional.");
        Release(keyboard.rKey); yield return null; yield return null;
        Assert.That(player.HasWireGesture, Is.False);
        Assert.That(ActiveWires(), Is.EqualTo(1));
    }

    [Test]
    public void FirstClickIsProvisionalAndCancellationPublishesNoWire()
    {
        Aim(Surface(Origin), Vector3Int.up);
        Call("HandleAutomaticWireClick", prefab);
        Assert.That(player.HasWireGesture, Is.True);
        Assert.That(ActiveWires(), Is.Zero);
        Assert.That(player.WireStatus, Does.Contain("selected"));
        Call("CancelWireRoute");
        Assert.That(player.HasWireGesture, Is.False);
        Assert.That(ActiveWires(), Is.Zero);
    }

    [UnityTest]
    public IEnumerator SecondClickFindsAndPublishesOnePhysicallyConnectedRoute()
    {
        var first = Surface(Origin); var second = Surface(Origin + Vector3Int.right * 4);
        Aim(first, Vector3Int.up); Call("HandleAutomaticWireClick", prefab);
        Aim(second, Vector3Int.up); Call("HandleAutomaticWireClick", prefab);
        Assert.That(player.IsWireSearchPending, Is.True);
        yield return AwaitRoute();
        Assert.That(ActiveWires(), Is.EqualTo(5));
        var a = LogicSignalBlock.At(Origin + Vector3Int.up) as LogicWireBlock;
        var b = LogicSignalBlock.At(Origin + Vector3Int.up + Vector3Int.right) as LogicWireBlock;
        Assert.That(a.ConnectsTo(b, b.GridPosition), Is.True);
        Assert.That(player.WireStatus, Does.Contain("connected"));
    }

    [UnityTest]
    public IEnumerator CancelledBackgroundResultCannotPublishLater()
    {
        var first = Surface(Origin); var second = Surface(Origin + Vector3Int.right * 4);
        Aim(first, Vector3Int.up); Call("HandleAutomaticWireClick", prefab);
        Aim(second, Vector3Int.up); Call("HandleAutomaticWireClick", prefab);
        Call("CancelWireRoute");
        for (int i = 0; i < 5; i++) { Call("PollWireSearch"); yield return null; }
        Assert.That(ActiveWires(), Is.Zero);
        Assert.That(player.HasWireGesture, Is.False);
    }

    [UnityTest]
    public IEnumerator WorldChangeRejectsCompletedBackgroundResultWithoutPartialWires()
    {
        var first = Surface(Origin); var second = Surface(Origin + Vector3Int.right * 4);
        Aim(first, Vector3Int.up); Call("HandleAutomaticWireClick", prefab);
        Aim(second, Vector3Int.up); Call("HandleAutomaticWireClick", prefab);
        Surface(Origin + Vector3Int.up + Vector3Int.right * 2);
        yield return AwaitRoute();
        Assert.That(ActiveWires(), Is.Zero);
        Assert.That(player.WireStatus, Does.Contain("World changed"));
    }

    [Test]
    public void QuickManualReleasePlacesExactlyOneWholeCell()
    {
        Aim(Surface(Origin), Vector3Int.up);
        Call("BeginManualWire", prefab); Call("FinishManualWire");
        Assert.That(ActiveWires(), Is.EqualTo(1));
        var wire = LogicSignalBlock.At(Origin + Vector3Int.up) as LogicWireBlock;
        Assert.That(wire, Is.Not.Null);
        Assert.That(wire.EndpointMask, Is.EqualTo(LogicWireBlock.GetDirectionBit(Vector3Int.up) | LogicWireBlock.GetDirectionBit(Vector3Int.down)));
        Assert.That(wire.ConnectsTo(null, Origin), Is.False, "A construction surface supplies an open end.");
    }

    [Test]
    public void ManualAxisUsesClickedSignedFaceAndNeverReverses()
    {
        var start = Surface(Origin); var backwards = Surface(Origin - Vector3Int.up * 4);
        Aim(start, Vector3Int.up); Call("BeginManualWire", prefab);
        Aim(backwards, Vector3Int.right); Call("UpdateManualWire");
        Assert.That(Get<Vector3Int>("manualAxis"), Is.EqualTo(Vector3Int.up));
        Assert.That(Get<int>("manualSteps"), Is.EqualTo(1));
        Set("targetBlock", null); Call("UpdateManualWire");
        Assert.That(Get<int>("manualSteps"), Is.EqualTo(1), "No-hit aiming retains the previous length.");
    }

    [Test]
    public void ManualHalfwayRoundingGoesOutwardAndObstructionRejectsWholeRow()
    {
        var start = Surface(Origin); var endSurface = Surface(Origin + Vector3Int.up * 4);
        Surface(Origin + Vector3Int.up * 2);
        Aim(start, Vector3Int.up); Call("BeginManualWire", prefab);
        Aim(endSurface, Vector3Int.down); Call("UpdateManualWire");
        Assert.That(Get<int>("manualSteps"), Is.EqualTo(3));
        Call("FinishManualWire");
        Assert.That(ActiveWires(), Is.Zero);
        Assert.That(player.WireStatus, Does.Contain("obstructed"));
    }

    [Test]
    public void ManualExtensionBranchesAtTheClickedCellOnly()
    {
        var clicked = Wire(Origin, LogicWireBlock.GetDirectionBit(Vector3Int.left) | LogicWireBlock.GetDirectionBit(Vector3Int.right));
        var neighbor = Wire(Origin + Vector3Int.right);
        WireTopology.Join(clicked, neighbor);
        Aim(clicked.GetComponent<Block>(), Vector3Int.up);
        Call("BeginManualWire", prefab); Call("FinishManualWire");
        var branch = LogicSignalBlock.At(Origin + Vector3Int.up) as LogicWireBlock;
        Assert.That(branch, Is.Not.Null);
        Assert.That(clicked.ConnectsTo(branch, branch.GridPosition), Is.True);
        Assert.That(LogicSignalBlock.At(neighbor.GridPosition + Vector3Int.up), Is.Null);
    }

    [Test]
    public void AutomaticRouteCannotTraverseUnrelatedWireCells()
    {
        var obstacle = Wire(Origin + Vector3Int.right * 2);
        var start = new WireEndpoint(Origin); var end = new WireEndpoint(Origin + Vector3Int.right * 4);
        Assert.That(WireWorldSnapshot.TryCapture(start, end, out var snapshot, out _), Is.True);
        var result = WireRouteSolver.Find(snapshot.Request, default);
        Assert.That(result.Status, Is.EqualTo(WireRouteStatus.Success));
        CollectionAssert.DoesNotContain(result.Path, obstacle.GridPosition);
        Assert.That(WireRouteCommit.TryCommit(snapshot, result.Path, prefab, start, end, out _), Is.True);
        Assert.That(obstacle.EndpointMask, Is.Zero, "Routing must not grow arms on an unrelated wire.");
    }

    [Test]
    public void PinAttachmentUsesFacingGeometryAndStaleSnapshotRejectsRotation()
    {
        var sourceBlock = Surface(Origin); sourceBlock.transform.rotation = Quaternion.Euler(0, 90, 0);
        var source = sourceBlock.gameObject.AddComponent<LogicSignalSource>();
        var start = new WireEndpoint(source, Origin, Vector3Int.right, false);
        var end = new WireEndpoint(Origin + Vector3Int.right * 3);
        Assert.That(WireWorldSnapshot.TryCapture(start, end, out var snapshot, out _), Is.True);
        var result = WireRouteSolver.Find(snapshot.Request, default);
        sourceBlock.transform.rotation = Quaternion.identity;
        Assert.That(WireRouteCommit.TryCommit(snapshot, result.Path, prefab, start, end, out var error), Is.False);
        Assert.That(error, Does.Contain("World changed"));
        Assert.That(ActiveWires(), Is.Zero);
        sourceBlock.transform.rotation = Quaternion.Euler(0, 90, 0);
        Assert.That(WireWorldSnapshot.TryCapture(start, end, out snapshot, out _), Is.True);
        result = WireRouteSolver.Find(snapshot.Request, default);
        Assert.That(WireRouteCommit.TryCommit(snapshot, result.Path, prefab, start, end, out _), Is.True);
        var wire = LogicSignalBlock.At(start.Cell) as LogicWireBlock;
        Assert.That(wire.HasEndpointTowards(Origin), Is.True);
        Assert.That(wire.ConnectsTo(source, Origin), Is.True);
    }

    [Test]
    public void NonPinComponentFaceSelectsOpenSurfaceInsteadOfRemotePin()
    {
        var sourceBlock = Surface(Origin); sourceBlock.gameObject.AddComponent<LogicSignalSource>();
        Aim(sourceBlock, Vector3Int.up); Call("HandleAutomaticWireClick", prefab);
        var endpoint = Get<WireEndpoint>("wireStart");
        Assert.That(endpoint.IsPin, Is.False);
        Assert.That(endpoint.Cell, Is.EqualTo(Origin + Vector3Int.up));
    }

    [UnityTest]
    public IEnumerator SelectingFreePinBesideExistingWireAddsTheMissingVisibleArmOnCommit()
    {
        int z = LogicWireBlock.GetDirectionBit(Vector3Int.forward) | LogicWireBlock.GetDirectionBit(Vector3Int.back);
        var wire = Wire(Origin, z);
        var sourceBlock = Surface(Origin + Vector3Int.right);
        sourceBlock.transform.rotation = Quaternion.Euler(0, 90, 0);
        var source = sourceBlock.gameObject.AddComponent<LogicSignalSource>();
        Aim(wire.GetComponent<Block>(), Vector3Int.up);
        Call("HandleAutomaticWireClick", prefab);
        Assert.That(wire.ConnectsTo(source, source.GridPosition), Is.False);
        Assert.That(wire.EndpointMask, Is.EqualTo(z), "First selection must leave the visible shape unchanged.");
        Aim(sourceBlock, Vector3Int.left);
        Call("HandleAutomaticWireClick", prefab);
        yield return AwaitRoute();
        Assert.That(ActiveWires(), Is.EqualTo(1), "One wire structure still occupies the cell.");
        Assert.That(wire.HasEndpointTowards(source.GridPosition), Is.True);
        Assert.That(wire.ConnectsTo(source, source.GridPosition), Is.True);
    }

    [Test]
    public void ManualExtensionFromAnOpenArmRemainsAvailable()
    {
        var first = Wire(Origin, LogicWireBlock.GetDirectionBit(Vector3Int.right));
        Aim(first.GetComponent<Block>(), Vector3Int.right);
        Call("BeginManualWire", prefab);
        Assert.That(player.HasWireGesture, Is.True, "An open arm is extendable even though its geometry already faces outward.");
        Call("FinishManualWire");
        var next = LogicSignalBlock.At(Origin + Vector3Int.right) as LogicWireBlock;
        Assert.That(next, Is.Not.Null);
        Assert.That(first.ConnectsTo(next, next.GridPosition), Is.True);
    }

    [UnityTest]
    public IEnumerator ComponentReplacementAutomaticallyReconnectsToThePreservedTouchingArm()
    {
        var wire = Wire(Origin + Vector3Int.right, LogicWireBlock.GetDirectionBit(Vector3Int.left));
        var oldBlock = Surface(Origin); oldBlock.transform.rotation = Quaternion.Euler(0, 90, 0);
        var oldSource = oldBlock.gameObject.AddComponent<LogicSignalSource>();
        yield return null;
        Assert.That(wire.ConnectsTo(oldSource, Origin), Is.True, "Facing geometry connects without a separate join action.");
        oldBlock.Break();
        yield return null;
        Assert.That(wire.HasEndpointTowards(Origin), Is.True, "Deleting the component leaves the wire's open arm intact.");
        var replacement = Surface(Origin); replacement.transform.rotation = Quaternion.Euler(0, 90, 0);
        var newSource = replacement.gameObject.AddComponent<LogicSignalSource>();
        yield return null;
        Assert.That(wire.ConnectsTo(newSource, Origin), Is.True);
        replacement.transform.rotation = Quaternion.identity;
        yield return null;
        Assert.That(wire.ConnectsTo(newSource, Origin), Is.False, "A component face without a pin must not connect.");
    }

    [UnityTest]
    public IEnumerator RunDeletionFollowsAutomaticTouchingContactsAndPreservesParallelWire()
    {
        int x = LogicWireBlock.GetDirectionBit(Vector3Int.right) | LogicWireBlock.GetDirectionBit(Vector3Int.left);
        var first = Wire(Origin, x);
        var middle = Wire(Origin + Vector3Int.right, x);
        var last = Wire(Origin + Vector3Int.right * 2, x);
        var parallel = Wire(Origin + Vector3Int.forward, x);
        yield return null;
        Assert.That(first.ConnectsTo(middle, middle.GridPosition), Is.True);
        Assert.That(middle.ConnectsTo(last, last.GridPosition), Is.True);
        Assert.That(first.ConnectsTo(parallel, parallel.GridPosition), Is.False);
        Assert.That(WireTopology.DeleteRun(middle), Is.EqualTo(3), "Automatic physical contacts form one deletable run.");
        yield return null;
        Assert.That(parallel != null && parallel.isActiveAndEnabled, Is.True);
        Assert.That(ActiveWires(), Is.EqualTo(1));
    }

    [Test]
    public void ManualEndCanJoinTheExactExistingWireCell()
    {
        var first = Wire(Origin); var last = Wire(Origin + Vector3Int.right * 3);
        Aim(first.GetComponent<Block>(), Vector3Int.right); Call("BeginManualWire", prefab);
        Set("manualSteps", 3); Call("FinishManualWire");
        Assert.That(ActiveWires(), Is.EqualTo(4));
        var beforeLast = LogicSignalBlock.At(last.GridPosition + Vector3Int.left) as LogicWireBlock;
        Assert.That(last.ConnectsTo(beforeLast, beforeLast.GridPosition), Is.True);
    }
}
