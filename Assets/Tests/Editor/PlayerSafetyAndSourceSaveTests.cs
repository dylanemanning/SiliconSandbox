using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

public class PlayerSafetyTests
{
    private GameObject playerObject;
    private GameObject obstacle;
    private Player player;
    private Rigidbody body;

    [TearDown]
    public void TearDown()
    {
        if (playerObject != null) Object.DestroyImmediate(playerObject);
        if (obstacle != null) Object.DestroyImmediate(obstacle);
    }

    [Test]
    public void RespawnMovesToTheNearestClearPositionWhenSpawnIsBlocked()
    {
        CreatePlayer();
        obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
        obstacle.transform.position = Vector3.up;
        Physics.SyncTransforms();

        Vector3 safePosition = FindSafePosition(Vector3.zero);

        Assert.That(safePosition, Is.Not.EqualTo(Vector3.zero));
        Assert.That((safePosition - Vector3.zero).magnitude, Is.LessThanOrEqualTo(1f));
        Assert.That(IsClear(safePosition), Is.True);

        player.fallThreshold = 0f;
        playerObject.transform.position = Vector3.down * 10f;
        typeof(Player).GetField("spawnPosition", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(player, Vector3.zero);
        typeof(Player).GetMethod("CheckFall", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(player, null);

        Assert.That(body.position, Is.EqualTo(safePosition));
        Assert.That(IsClear(body.position), Is.True);
    }

    [Test]
    public void FlightDoesNotChangeGlobalGravityAndRestoresPlayerGravity()
    {
        CreatePlayer();
        Vector3 originalGravity = Physics.gravity;
        MethodInfo toggleFlight = typeof(Player).GetMethod("ToggleFlight", BindingFlags.Instance | BindingFlags.NonPublic);

        toggleFlight.Invoke(player, null);

        Assert.That(body.useGravity, Is.False);
        Assert.That(Physics.gravity, Is.EqualTo(originalGravity));

        toggleFlight.Invoke(player, null);

        Assert.That(body.useGravity, Is.True);
        Assert.That(Physics.gravity, Is.EqualTo(originalGravity));

        toggleFlight.Invoke(player, null);
        typeof(Player).GetMethod("ResetFlightForDisable", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(player, null);

        Assert.That(body.useGravity, Is.True);
        Assert.That(Physics.gravity, Is.EqualTo(originalGravity));
    }

    private void CreatePlayer()
    {
        playerObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        playerObject.name = "Player safety test";
        CapsuleCollider capsule = playerObject.GetComponent<CapsuleCollider>();
        capsule.center = Vector3.up;
        body = playerObject.AddComponent<Rigidbody>();
        player = playerObject.AddComponent<Player>();
        typeof(Player).GetField("rb", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(player, body);
        typeof(Player).GetField("spawnRotation", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(player, playerObject.transform.rotation);
        typeof(Player).GetField("useGravityBeforeFlight", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(player, body.useGravity);
    }

    private Vector3 FindSafePosition(Vector3 requestedPosition)
    {
        object[] arguments = { requestedPosition, Vector3.zero };
        bool found = (bool)typeof(Player)
            .GetMethod("TryFindSafeSpawnPosition", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(player, arguments);
        Assert.That(found, Is.True);
        return (Vector3)arguments[1];
    }

    private bool IsClear(Vector3 position)
    {
        object[] arguments = { position };
        return (bool)typeof(Player)
            .GetMethod("IsSpawnPositionClear", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(player, arguments);
    }
}

public class WorldSourceSaveTests
{
    [Test]
    public void SavedWorldPreservesAndRestoresSourceOutputState()
    {
        var sourceObject = new GameObject("Source save test");
        try
        {
            Block block = sourceObject.AddComponent<Block>();
            LogicSignalSource source = sourceObject.AddComponent<LogicSignalSource>();
            source.SetState(1);

            WorldSaveSystem.SavedBlock saved = (WorldSaveSystem.SavedBlock)typeof(WorldSaveSystem)
                .GetMethod("CaptureBlock", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { block });
            WorldSaveSystem.LegacySavedBlock loaded = JsonUtility
                .FromJson<WorldSaveSystem.LegacySavedBlock>(JsonUtility.ToJson(saved));

            Assert.That(WorldSaveSystem.CurrentSaveVersion, Is.EqualTo(4));
            Assert.That(loaded.hasSourceState, Is.True);
            Assert.That(loaded.sourceState, Is.EqualTo(1));

            source.SetState(0);
            typeof(WorldSaveSystem)
                .GetMethod("RestoreSourceState", BindingFlags.Static | BindingFlags.NonPublic)
                .Invoke(null, new object[] { source, loaded });

            Assert.That(source.SignalState, Is.EqualTo(1));
        }
        finally
        {
            Object.DestroyImmediate(sourceObject);
        }
    }
}
