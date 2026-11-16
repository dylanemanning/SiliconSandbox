// WorldBackupsTests.cs — EditMode tests for world backups (PR-2).
//
// Pure file tests: every test works in its own temporary Worlds folder, so they
// never touch real saves and run on every platform, CI included. Test names
// carry the Verification Test Inventory IDs they provide evidence for.

using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;

public class WorldBackupsTests
{
    private const string World = "Test World";

    private string root;
    private DateTime clock;

    [SetUp]
    public void UseTemporaryWorldsFolder()
    {
        root = Path.Combine(Path.GetTempPath(), "SiliconSandboxBackupTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        WorldBackups.WorldsRoot = root;

        // Each backup is one second after the last, so "newest first" is predictable.
        clock = new DateTime(2026, 11, 15, 20, 0, 0, DateTimeKind.Utc);
        WorldBackups.UtcNow = () => clock = clock.AddSeconds(1);
    }

    [TearDown]
    public void RestoreDefaults()
    {
        WorldBackups.WorldsRoot = null;
        WorldBackups.UtcNow = () => DateTime.UtcNow;
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    // A world save in the format WorldSaveSystem writes, with the given number of blocks.
    private static string SaveJson(int blockCount, int version = WorldSaveSystem.CurrentSaveVersion, string worldName = World)
    {
        var json = new StringBuilder();
        json.Append("{\"version\":").Append(version).Append(",\"worldName\":\"").Append(worldName).Append("\",\"blocks\":[");
        for (int index = 0; index < blockCount; index++)
        {
            if (index > 0) json.Append(',');
            json.Append("{\"prefabName\":\"AND Gate\",\"position\":{\"x\":").Append(index)
                .Append(",\"y\":1.0,\"z\":0.0},\"rotation\":{\"x\":0.0,\"y\":0.0,\"z\":0.0,\"w\":1.0},")
                .Append("\"scale\":{\"x\":1.0,\"y\":1.0,\"z\":1.0},\"footprintSize\":{\"x\":2,\"y\":1,\"z\":1},")
                .Append("\"isGround\":false,\"hasWireEndpointMask\":false,\"wireEndpointMask\":0}");
        }
        return json.Append("]}").ToString();
    }

    private void WriteSave(string contents, string worldName = World) =>
        File.WriteAllText(WorldBackups.GetSavePath(worldName), contents);

    private string ReadSave(string worldName = World) => File.ReadAllText(WorldBackups.GetSavePath(worldName));

    private BackupInfo CreateBackup(BackupKind kind = BackupKind.Manual)
    {
        BackupResult result = WorldBackups.Create(World, kind);
        Assert.IsTrue(result.Success, result.Error);
        return result.Backup;
    }

    private void CorruptBackupFile(BackupInfo backup, string contents) => File.WriteAllText(backup.FilePath, contents);

    // ---- Create (SR-2.1) -----------------------------------------------------

    [Test]
    public void TC_2_1_1_CreateBackupOfExistingWorld_CopiesTheSave()
    {
        WriteSave(SaveJson(3));

        BackupInfo backup = CreateBackup();

        Assert.IsTrue(File.Exists(backup.FilePath));
        StringAssert.StartsWith(WorldBackups.GetBackupFolder(World), backup.FilePath);
        CollectionAssert.AreEqual(File.ReadAllBytes(WorldBackups.GetSavePath(World)), File.ReadAllBytes(backup.FilePath));
        Assert.AreEqual(BackupKind.Manual, backup.Kind);
    }

    [Test]
    public void TC_2_1_2_CreateMultipleBackups_EachKeepsItsOwnState()
    {
        WriteSave(SaveJson(1));
        BackupInfo first = CreateBackup();
        WriteSave(SaveJson(2));
        BackupInfo second = CreateBackup();
        WriteSave(SaveJson(3));
        BackupInfo third = CreateBackup();

        Assert.AreEqual(3, new[] { first.Id, second.Id, third.Id }.Distinct().Count());
        Assert.AreEqual(SaveJson(1), File.ReadAllText(first.FilePath));
        Assert.AreEqual(SaveJson(2), File.ReadAllText(second.FilePath));
        Assert.AreEqual(SaveJson(3), File.ReadAllText(third.FilePath));
    }

    [Test]
    public void TC_2_1_3_AllBackupsListedNewestFirstWithTimestamps()
    {
        WriteSave(SaveJson(1));
        BackupInfo first = CreateBackup();
        BackupInfo second = CreateBackup(BackupKind.Open);
        BackupInfo third = CreateBackup();

        var listed = WorldBackups.List(World);

        CollectionAssert.AreEqual(new[] { third.Id, second.Id, first.Id }, listed.Select(b => b.Id).ToArray());
        Assert.AreEqual(new DateTime(2026, 11, 15, 20, 0, 3, DateTimeKind.Utc), listed[0].CreatedUtc);
        Assert.AreEqual(new DateTime(2026, 11, 15, 20, 0, 1, DateTimeKind.Utc), listed[2].CreatedUtc);
        Assert.AreEqual(BackupKind.Open, listed[1].Kind);
        Assert.AreEqual(DateTimeKind.Local, listed[0].CreatedLocal.Kind);
    }

    [Test]
    public void TC_2_1_ListReportsBlockCountAndSize()
    {
        WriteSave(SaveJson(4));
        CreateBackup();

        BackupInfo listed = WorldBackups.List(World).Single();

        Assert.IsTrue(listed.IsValid, listed.Error);
        Assert.AreEqual(4, listed.BlockCount);
        Assert.AreEqual(WorldSaveSystem.CurrentSaveVersion, listed.SaveVersion);
        Assert.AreEqual(new FileInfo(WorldBackups.GetSavePath(World)).Length, listed.SizeBytes);
    }

    [Test]
    public void Create_WorldWithNoSave_ReturnsReadableError()
    {
        BackupResult result = WorldBackups.Create(World);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("no save file", result.Error);
        Assert.IsFalse(Directory.Exists(WorldBackups.GetBackupFolder(World)));
    }

    [Test]
    public void Create_SameMillisecond_GetsDistinctIds()
    {
        WriteSave(SaveJson(1));
        var fixedTime = new DateTime(2026, 11, 15, 20, 0, 0, 500, DateTimeKind.Utc);
        WorldBackups.UtcNow = () => fixedTime;

        BackupInfo first = CreateBackup();
        BackupInfo second = CreateBackup();

        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual(2, WorldBackups.List(World).Count);
    }

    [Test]
    public void List_WorldWithNoBackups_IsEmpty()
    {
        Assert.IsEmpty(WorldBackups.List(World));
        Assert.IsEmpty(WorldBackups.List("Never Saved"));
    }

    [Test]
    public void List_IgnoresFilesThatAreNotBackups()
    {
        WriteSave(SaveJson(1));
        CreateBackup();
        File.WriteAllText(Path.Combine(WorldBackups.GetBackupFolder(World), "notes.json"), "{}");

        Assert.AreEqual(1, WorldBackups.List(World).Count);
    }

    [Test]
    public void Backups_AreKeptPerWorld()
    {
        WriteSave(SaveJson(1));
        WriteSave(SaveJson(2, worldName: "Other World"), "Other World");
        CreateBackup();

        Assert.AreEqual(1, WorldBackups.List(World).Count);
        Assert.IsEmpty(WorldBackups.List("Other World"));
    }

    // ---- Restore (SR-2.2) ----------------------------------------------------

    [Test]
    public void TC_2_2_1_RestoreBackup_SaveRevertsToThatBackup()
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        WriteSave(SaveJson(7));

        BackupResult result = WorldBackups.Restore(World, backup.Id);

        Assert.IsTrue(result.Success, result.Error);
        Assert.AreEqual(SaveJson(2), ReadSave());
        Assert.AreEqual(backup.Id, result.Backup.Id);
    }

    [Test]
    public void TC_2_2_1_RestoreBackup_BacksUpTheReplacedSaveFirst()
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        WriteSave(SaveJson(7));

        BackupResult result = WorldBackups.Restore(World, backup.Id);

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsNotNull(result.SafetyBackup);
        Assert.AreEqual(BackupKind.Restore, result.SafetyBackup.Kind);
        Assert.AreEqual(SaveJson(7), File.ReadAllText(result.SafetyBackup.FilePath));
        Assert.AreEqual(2, WorldBackups.List(World).Count);
    }

    [Test]
    public void TC_2_2_1_RestoreIsItselfUndoable()
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        WriteSave(SaveJson(7));
        BackupInfo safety = WorldBackups.Restore(World, backup.Id).SafetyBackup;

        BackupResult undo = WorldBackups.Restore(World, safety.Id);

        Assert.IsTrue(undo.Success, undo.Error);
        Assert.AreEqual(SaveJson(7), ReadSave());
    }

    [Test]
    public void TC_2_2_1_RestoreWhenSaveFileWasRemoved_RecreatesIt()
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        File.Delete(WorldBackups.GetSavePath(World));

        BackupResult result = WorldBackups.Restore(World, backup.Id);

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsNull(result.SafetyBackup);
        Assert.AreEqual(SaveJson(2), ReadSave());
    }

    [Test]
    public void TC_2_2_3_RestoreMissingBackup_ShowsErrorAndLeavesSave()
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        File.Delete(backup.FilePath);
        WriteSave(SaveJson(7));

        BackupResult result = WorldBackups.Restore(World, backup.Id);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("could not be found", result.Error);
        Assert.AreEqual(SaveJson(7), ReadSave());
    }

    [TestCase("this is not json", TestName = "TC_2_2_3_RestoreCorruptBackup_NotJson")]
    [TestCase("", TestName = "TC_2_2_3_RestoreCorruptBackup_Empty")]
    [TestCase("{\"version\":3,\"worldName\":\"Test World\",\"blocks\":[{\"prefabName\":", TestName = "TC_2_2_3_RestoreCorruptBackup_Truncated")]
    [TestCase("{\"something\":\"else\"}", TestName = "TC_2_2_3_RestoreCorruptBackup_NotAWorldSave")]
    public void RestoreCorruptBackup_ShowsErrorAndLeavesSaveUntouched(string corruptContents)
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        CorruptBackupFile(backup, corruptContents);
        WriteSave(SaveJson(7));

        BackupResult result = WorldBackups.Restore(World, backup.Id);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("can't be restored", result.Error);
        Assert.AreEqual(SaveJson(7), ReadSave());
        Assert.AreEqual(1, WorldBackups.List(World).Count, "no safety backup is made when the restore is refused");
    }

    [Test]
    public void TC_2_2_3_RestoreBackupFromNewerGameVersion_IsRefused()
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        CorruptBackupFile(backup, SaveJson(2, WorldSaveSystem.CurrentSaveVersion + 1));
        WriteSave(SaveJson(7));

        BackupResult result = WorldBackups.Restore(World, backup.Id);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("newer version", result.Error);
        Assert.AreEqual(SaveJson(7), ReadSave());
    }

    [Test]
    public void TC_2_2_3_CorruptBackupIsListedWithAnError()
    {
        WriteSave(SaveJson(2));
        BackupInfo good = CreateBackup();
        BackupInfo bad = CreateBackup();
        CorruptBackupFile(bad, "garbage");

        var listed = WorldBackups.List(World);

        Assert.AreEqual(2, listed.Count);
        BackupInfo badListed = listed.Single(b => b.Id == bad.Id);
        Assert.IsFalse(badListed.IsValid);
        Assert.IsNotEmpty(badListed.Error);
        Assert.IsTrue(listed.Single(b => b.Id == good.Id).IsValid);
    }

    [TestCase("..\\..\\Test World")]
    [TestCase("../Test World")]
    [TestCase("20261115-200001-000_manual/../../x")]
    [TestCase("not-a-backup")]
    [TestCase(null)]
    public void RestoreAndDelete_RejectIdsThatAreNotBackupIds(string id)
    {
        WriteSave(SaveJson(2));

        Assert.IsFalse(WorldBackups.Restore(World, id).Success);
        Assert.IsFalse(WorldBackups.Delete(World, id).Success);
        Assert.IsTrue(File.Exists(WorldBackups.GetSavePath(World)));
    }

    // ---- Delete (SR-2.3) -----------------------------------------------------

    [Test]
    public void TC_2_3_1_DeletedBackupNoLongerListed()
    {
        WriteSave(SaveJson(2));
        BackupInfo keep = CreateBackup();
        BackupInfo remove = CreateBackup();

        BackupResult result = WorldBackups.Delete(World, remove.Id);

        Assert.IsTrue(result.Success, result.Error);
        CollectionAssert.AreEqual(new[] { keep.Id }, WorldBackups.List(World).Select(b => b.Id).ToArray());
    }

    [Test]
    public void TC_2_3_3_DeletedBackupFileIsRemoved_OthersAndSaveKept()
    {
        WriteSave(SaveJson(2));
        BackupInfo keep = CreateBackup();
        BackupInfo remove = CreateBackup();

        WorldBackups.Delete(World, remove.Id);

        Assert.IsFalse(File.Exists(remove.FilePath));
        Assert.IsTrue(File.Exists(keep.FilePath));
        Assert.IsTrue(File.Exists(WorldBackups.GetSavePath(World)));
    }

    [Test]
    public void TC_2_3_3_DeleteMissingBackup_ReturnsError()
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        File.Delete(backup.FilePath);

        BackupResult result = WorldBackups.Delete(World, backup.Id);

        Assert.IsFalse(result.Success);
        StringAssert.Contains("could not be found", result.Error);
    }

    [Test]
    public void TC_2_3_3_DeleteAll_RemovesEveryBackupOfThatWorldOnly()
    {
        WriteSave(SaveJson(2));
        WriteSave(SaveJson(1, worldName: "Other World"), "Other World");
        CreateBackup();
        CreateBackup(BackupKind.Open);
        Assert.IsTrue(WorldBackups.Create("Other World").Success);

        BackupResult result = WorldBackups.DeleteAll(World);

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsFalse(Directory.Exists(WorldBackups.GetBackupFolder(World)));
        Assert.IsTrue(File.Exists(WorldBackups.GetSavePath(World)), "DeleteAll removes backups, not the world");
        Assert.AreEqual(1, WorldBackups.List("Other World").Count);
    }

    [Test]
    public void DeleteAll_WorldWithNoBackups_Succeeds()
    {
        Assert.IsTrue(WorldBackups.DeleteAll(World).Success);
    }

    // ---- Automatic backup on open --------------------------------------------

    [Test]
    public void OpenBackup_IsTakenWhenTheSaveChanged()
    {
        WriteSave(SaveJson(2));

        BackupResult result = WorldBackups.CreateOpenBackup(World);

        Assert.IsTrue(result.Success, result.Error);
        Assert.IsFalse(result.Skipped);
        Assert.AreEqual(BackupKind.Open, result.Backup.Kind);
    }

    [Test]
    public void OpenBackup_SkippedWhenNothingChangedSinceLastBackup()
    {
        WriteSave(SaveJson(2));
        WorldBackups.CreateOpenBackup(World);

        BackupResult again = WorldBackups.CreateOpenBackup(World);

        Assert.IsTrue(again.Success);
        Assert.IsTrue(again.Skipped);
        Assert.AreEqual(1, WorldBackups.List(World).Count);
    }

    [Test]
    public void OpenBackup_SkippedWhenReopeningRightAfterARestore()
    {
        WriteSave(SaveJson(2));
        BackupInfo backup = CreateBackup();
        WriteSave(SaveJson(7));
        WorldBackups.Restore(World, backup.Id);

        BackupResult open = WorldBackups.CreateOpenBackup(World);

        Assert.IsTrue(open.Skipped, "the restored save is already a backup");
        Assert.AreEqual(2, WorldBackups.List(World).Count);
    }

    [Test]
    public void OpenBackup_NewWorldWithNoSave_TakesNone()
    {
        BackupResult result = WorldBackups.CreateOpenBackup(World);

        Assert.IsTrue(result.Success);
        Assert.IsTrue(result.Skipped);
        Assert.IsEmpty(WorldBackups.List(World));
    }

    [Test]
    public void OpenBackup_KeepsOnlyTheNewestOpenBackups_NeverPrunesManualOrRestore()
    {
        WriteSave(SaveJson(0));
        BackupInfo manual = CreateBackup();
        BackupInfo restore = CreateBackup(BackupKind.Restore);

        int opens = WorldBackups.MaxOpenBackups + 3;
        for (int index = 1; index <= opens; index++)
        {
            WriteSave(SaveJson(index));
            Assert.IsTrue(WorldBackups.CreateOpenBackup(World).Success);
        }

        var listed = WorldBackups.List(World);
        var openBackups = listed.Where(b => b.Kind == BackupKind.Open).ToList();

        Assert.AreEqual(WorldBackups.MaxOpenBackups, openBackups.Count);
        Assert.AreEqual(opens, openBackups[0].BlockCount, "newest open backup kept");
        Assert.AreEqual(opens - WorldBackups.MaxOpenBackups + 1, openBackups.Last().BlockCount, "oldest ones pruned");
        Assert.IsTrue(listed.Any(b => b.Id == manual.Id));
        Assert.IsTrue(listed.Any(b => b.Id == restore.Id));
    }

    // ---- Names ---------------------------------------------------------------

    [Test]
    public void BackupFolderUsesTheSameSanitizedNameAsTheSave()
    {
        const string awkward = "My: World?";
        WriteSave(SaveJson(1, worldName: awkward), awkward);

        BackupResult result = WorldBackups.Create(awkward);

        Assert.IsTrue(result.Success, result.Error);
        Assert.AreEqual(
            Path.GetFileNameWithoutExtension(WorldBackups.GetSavePath(awkward)),
            Path.GetFileName(WorldBackups.GetBackupFolder(awkward)));
    }

    [Test]
    public void BackupFolderIsNotAWorldSave()
    {
        WriteSave(SaveJson(1));
        CreateBackup();

        // WorldSaveSystem lists worlds from the top-level *.json files only.
        CollectionAssert.AreEqual(
            new[] { WorldBackups.GetSavePath(World) },
            Directory.GetFiles(WorldBackups.WorldsRoot, "*.json"));
    }
}
