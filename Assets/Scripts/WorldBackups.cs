// WorldBackups.cs — file-level backups of world saves (PR-2, SR-2.1 to SR-2.3).
//
// A backup is a byte-for-byte copy of a world's save file at one moment, kept in
//   <Worlds>/Backups/<sanitized world name>/<UTC yyyyMMdd-HHmmss-fff>_<kind>.json
// Because it is the same format as the save, restoring is a copy back.
//
// Every operation returns a BackupResult with a readable Error instead of
// throwing, so menus can show the message directly (TC-2.2.3).
//
// This class only touches files. It never reads or changes the open scene, so it
// is safe to call from the main menu and from EditMode tests. Restore refuses to
// run while the same world is open, since the open world's next autosave would
// overwrite the restored file.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

public enum BackupKind
{
    Manual,   // Create Backup button
    Open,     // taken automatically when a world finishes loading
    Restore   // the current save, taken automatically just before a restore
}

public sealed class BackupInfo
{
    public string WorldName { get; internal set; }
    public string Id { get; internal set; }
    public BackupKind Kind { get; internal set; }
    public DateTime CreatedUtc { get; internal set; }
    public DateTime CreatedLocal => CreatedUtc.ToLocalTime();
    public long SizeBytes { get; internal set; }
    public string FilePath { get; internal set; }

    // Filled in by List(): a backup that can't be read is still listed, with
    // IsValid false and Error saying why, so the player can see and delete it.
    public bool IsValid { get; internal set; }
    public int BlockCount { get; internal set; }
    public int SaveVersion { get; internal set; }
    public string Error { get; internal set; }
}

public sealed class BackupResult
{
    public bool Success { get; private set; }
    public string Error { get; private set; }

    // The backup that was created, restored or deleted. For Restore, the backup
    // of the save that was replaced is in SafetyBackup. Both can be null.
    public BackupInfo Backup { get; private set; }
    public BackupInfo SafetyBackup { get; private set; }

    // True when an automatic open backup was not needed (no save yet, or an
    // identical backup already exists). Still counts as Success.
    public bool Skipped { get; private set; }

    internal static BackupResult Ok(BackupInfo backup = null, BackupInfo safetyBackup = null) =>
        new BackupResult { Success = true, Backup = backup, SafetyBackup = safetyBackup };

    internal static BackupResult NotNeeded() => new BackupResult { Success = true, Skipped = true };

    internal static BackupResult Fail(string error) => new BackupResult { Success = false, Error = error };
}

public static class WorldBackups
{
    // Automatic open backups beyond this many are deleted, oldest first.
    // Manual and restore backups are never deleted automatically.
    public const int MaxOpenBackups = 10;

    private const string BackupFolderName = "Backups";
    private const string TimestampFormat = "yyyyMMdd-HHmmss-fff";
    private static readonly Regex IdPattern =
        new Regex(@"^(\d{8}-\d{6}-\d{3})_(manual|open|restore)$", RegexOptions.CultureInvariant);

    private static string worldsRootOverride;

    // Folder holding the world saves. Defaults to the folder WorldSaveSystem
    // uses; tests point it at a temporary folder. Set to null for the default.
    public static string WorldsRoot
    {
        get => worldsRootOverride ?? Path.Combine(Application.persistentDataPath, "Worlds");
        set => worldsRootOverride = value;
    }

    // Source of backup timestamps. Tests replace it to control ordering.
    public static Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

    // ---- Paths ---------------------------------------------------------------

    public static string GetSavePath(string worldName) =>
        Path.Combine(WorldsRoot, SanitizeFileName(worldName) + ".json");

    public static string GetBackupFolder(string worldName) =>
        Path.Combine(WorldsRoot, BackupFolderName, SanitizeFileName(worldName));

    // ---- Create --------------------------------------------------------------

    public static BackupResult Create(string worldName, BackupKind kind = BackupKind.Manual)
    {
        if (string.IsNullOrWhiteSpace(worldName)) return BackupResult.Fail("No world name was given.");

        string savePath = GetSavePath(worldName);
        if (!File.Exists(savePath))
            return BackupResult.Fail($"'{worldName}' has no save file yet, so there is nothing to back up.");

        try
        {
            string folder = GetBackupFolder(worldName);
            Directory.CreateDirectory(folder);

            // Ids are timestamps, so two backups in the same millisecond would
            // clash; move the later one forward until its name is free.
            DateTime created = TruncateToMilliseconds(UtcNow());
            string backupPath = Path.Combine(folder, MakeId(created, kind) + ".json");
            while (File.Exists(backupPath))
            {
                created = created.AddMilliseconds(1);
                backupPath = Path.Combine(folder, MakeId(created, kind) + ".json");
            }

            File.Copy(savePath, backupPath, false);
            return BackupResult.Ok(Describe(worldName, backupPath, readContents: false));
        }
        catch (Exception exception)
        {
            return BackupResult.Fail($"Could not back up '{worldName}': {exception.Message}");
        }
    }

    // Called once a world has finished loading successfully. Copies the save as
    // it is on disk, unless an identical backup already exists (opening a world
    // without changing it, or reopening right after a restore), then prunes old
    // open backups. A world with no save file yet gets none.
    public static BackupResult CreateOpenBackup(string worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName)) return BackupResult.Fail("No world name was given.");

        string savePath = GetSavePath(worldName);
        if (!File.Exists(savePath)) return BackupResult.NotNeeded();

        try
        {
            if (MatchesExistingBackup(worldName, savePath)) return BackupResult.NotNeeded();
        }
        catch (Exception exception)
        {
            return BackupResult.Fail($"Could not check the backups of '{worldName}': {exception.Message}");
        }

        BackupResult result = Create(worldName, BackupKind.Open);
        if (result.Success) PruneOpenBackups(worldName);
        return result;
    }

    // ---- List ----------------------------------------------------------------

    // Every backup of the world, newest first. Unreadable backups are included
    // with IsValid false. A world with no backups gives an empty list.
    public static List<BackupInfo> List(string worldName)
    {
        var backups = new List<BackupInfo>();
        if (string.IsNullOrWhiteSpace(worldName)) return backups;

        string folder = GetBackupFolder(worldName);
        if (!Directory.Exists(folder)) return backups;

        foreach (string path in Directory.GetFiles(folder, "*.json"))
        {
            if (!TryParseId(Path.GetFileNameWithoutExtension(path), out _, out _)) continue;
            backups.Add(Describe(worldName, path, readContents: true));
        }

        backups.Sort((a, b) => string.CompareOrdinal(b.Id, a.Id));
        return backups;
    }

    // ---- Restore -------------------------------------------------------------

    // Replaces the world's save with the backup. Checks the backup can be loaded
    // before touching anything, and backs up the current save first (kind
    // Restore) so a restore can itself be undone.
    public static BackupResult Restore(string worldName, string backupId)
    {
        if (string.IsNullOrWhiteSpace(worldName)) return BackupResult.Fail("No world name was given.");
        if (!TryParseId(backupId, out _, out _)) return BackupResult.Fail($"'{backupId}' is not a valid backup id.");

        if (IsWorldOpen(worldName))
            return BackupResult.Fail($"Close '{worldName}' before restoring one of its backups.");

        string backupPath = Path.Combine(GetBackupFolder(worldName), backupId + ".json");
        if (!File.Exists(backupPath))
            return BackupResult.Fail($"The backup from {DescribeTime(backupId)} could not be found. It may have been deleted.");

        string problem = CheckSaveFile(backupPath, out _, out _);
        if (problem != null)
            return BackupResult.Fail($"The backup from {DescribeTime(backupId)} can't be restored: {problem}");

        BackupInfo safetyBackup = null;
        string savePath = GetSavePath(worldName);
        if (File.Exists(savePath))
        {
            BackupResult safety = Create(worldName, BackupKind.Restore);
            if (!safety.Success)
                return BackupResult.Fail($"Restore cancelled, because the current world could not be backed up first. {safety.Error}");
            safetyBackup = safety.Backup;
        }

        try
        {
            Directory.CreateDirectory(WorldsRoot);
            File.Copy(backupPath, savePath, true);
        }
        catch (Exception exception)
        {
            return BackupResult.Fail($"Could not restore '{worldName}': {exception.Message}");
        }

        return BackupResult.Ok(Describe(worldName, backupPath, readContents: true), safetyBackup);
    }

    // ---- Delete --------------------------------------------------------------

    public static BackupResult Delete(string worldName, string backupId)
    {
        if (string.IsNullOrWhiteSpace(worldName)) return BackupResult.Fail("No world name was given.");
        if (!TryParseId(backupId, out _, out _)) return BackupResult.Fail($"'{backupId}' is not a valid backup id.");

        string backupPath = Path.Combine(GetBackupFolder(worldName), backupId + ".json");
        if (!File.Exists(backupPath))
            return BackupResult.Fail($"The backup from {DescribeTime(backupId)} could not be found. It may already be deleted.");

        try
        {
            BackupInfo info = Describe(worldName, backupPath, readContents: false);
            File.Delete(backupPath);
            return BackupResult.Ok(info);
        }
        catch (Exception exception)
        {
            return BackupResult.Fail($"Could not delete the backup from {DescribeTime(backupId)}: {exception.Message}");
        }
    }

    // Removes every backup of the world. For deleting the world itself.
    public static BackupResult DeleteAll(string worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName)) return BackupResult.Fail("No world name was given.");

        string folder = GetBackupFolder(worldName);
        if (!Directory.Exists(folder)) return BackupResult.Ok();

        try
        {
            Directory.Delete(folder, true);
            return BackupResult.Ok();
        }
        catch (Exception exception)
        {
            return BackupResult.Fail($"Could not delete the backups of '{worldName}': {exception.Message}");
        }
    }

    // ---- Helpers -------------------------------------------------------------

    // Same rule as WorldSaveSystem, so a world's save and its backup folder
    // always get the same name.
    public static string SanitizeFileName(string value)
    {
        string result = string.IsNullOrWhiteSpace(value) ? "World" : value.Trim();
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            result = result.Replace(invalidCharacter.ToString(), "_");
        }

        return result;
    }

    public static bool TryParseId(string id, out DateTime createdUtc, out BackupKind kind)
    {
        createdUtc = default;
        kind = BackupKind.Manual;
        if (string.IsNullOrEmpty(id)) return false;

        Match match = IdPattern.Match(id);
        if (!match.Success) return false;
        if (!DateTime.TryParseExact(match.Groups[1].Value, TimestampFormat, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out createdUtc))
            return false;

        switch (match.Groups[2].Value)
        {
            case "open": kind = BackupKind.Open; break;
            case "restore": kind = BackupKind.Restore; break;
            default: kind = BackupKind.Manual; break;
        }
        return true;
    }

    private static string MakeId(DateTime createdUtc, BackupKind kind) =>
        createdUtc.ToString(TimestampFormat, CultureInfo.InvariantCulture) + "_" + kind.ToString().ToLowerInvariant();

    private static DateTime TruncateToMilliseconds(DateTime time) =>
        new DateTime(time.Ticks - time.Ticks % TimeSpan.TicksPerMillisecond, DateTimeKind.Utc);

    private static string DescribeTime(string backupId) =>
        TryParseId(backupId, out DateTime createdUtc, out _)
            ? createdUtc.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)
            : backupId;

    private static BackupInfo Describe(string worldName, string backupPath, bool readContents)
    {
        string id = Path.GetFileNameWithoutExtension(backupPath);
        TryParseId(id, out DateTime createdUtc, out BackupKind kind);

        var info = new BackupInfo
        {
            WorldName = worldName,
            Id = id,
            Kind = kind,
            CreatedUtc = createdUtc,
            FilePath = backupPath,
            SizeBytes = new FileInfo(backupPath).Length,
            IsValid = true
        };

        if (readContents)
        {
            info.Error = CheckSaveFile(backupPath, out int blockCount, out int version);
            info.IsValid = info.Error == null;
            info.BlockCount = blockCount;
            info.SaveVersion = version;
        }

        return info;
    }

    // Header of a world save: just enough to check it will load. Mirrors the
    // fields WorldSaveSystem writes.
    // Fields are filled in by JsonUtility, which the compiler can't see.
#pragma warning disable 0649
    [Serializable]
    private class SaveHeader
    {
        public int version;
        public string worldName;
        public List<WorldSaveSystem.SavedBlock> blocks;
    }
#pragma warning restore 0649

    // Null when the file is a save this version of the game can load, otherwise
    // a short reason. Uses the same checks WorldSaveSystem applies on load.
    private static string CheckSaveFile(string path, out int blockCount, out int version)
    {
        blockCount = 0;
        version = 0;

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception exception)
        {
            return $"the file could not be read ({exception.Message}).";
        }

        if (string.IsNullOrWhiteSpace(json)) return "the file is empty.";

        SaveHeader header;
        try
        {
            header = JsonUtility.FromJson<SaveHeader>(json);
        }
        catch (Exception)
        {
            return "the file is damaged and is not a readable world save.";
        }

        if (header == null || string.IsNullOrWhiteSpace(header.worldName) || header.blocks == null)
            return "the file is not a world save.";

        version = header.version;
        blockCount = header.blocks.Count;

        if (header.version > WorldSaveSystem.CurrentSaveVersion)
            return $"it was made by a newer version of the game (save format {header.version}).";
        if (header.version < 0) return "the file is damaged and is not a readable world save.";

        return null;
    }

    private static bool MatchesExistingBackup(string worldName, string savePath)
    {
        string folder = GetBackupFolder(worldName);
        if (!Directory.Exists(folder)) return false;

        long saveLength = new FileInfo(savePath).Length;
        byte[] saveBytes = null;
        foreach (string path in Directory.GetFiles(folder, "*.json"))
        {
            if (!TryParseId(Path.GetFileNameWithoutExtension(path), out _, out _)) continue;
            if (new FileInfo(path).Length != saveLength) continue;

            if (saveBytes == null) saveBytes = File.ReadAllBytes(savePath);
            if (BytesEqual(saveBytes, File.ReadAllBytes(path))) return true;
        }

        return false;
    }

    private static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int index = 0; index < a.Length; index++)
        {
            if (a[index] != b[index]) return false;
        }

        return true;
    }

    private static void PruneOpenBackups(string worldName)
    {
        int openSeen = 0;
        foreach (BackupInfo backup in List(worldName))
        {
            if (backup.Kind != BackupKind.Open) continue;
            if (++openSeen <= MaxOpenBackups) continue;

            try
            {
                File.Delete(backup.FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Could not remove old backup '{backup.Id}' of '{worldName}': {exception.Message}");
            }
        }
    }

    private static bool IsWorldOpen(string worldName)
    {
        WorldSaveSystem open = WorldSaveSystem.Instance;
        return open != null
            && string.Equals(SanitizeFileName(open.WorldName), SanitizeFileName(worldName), StringComparison.Ordinal);
    }
}
