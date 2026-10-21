using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Authored wire edges. Visible arms alone never join two wire cells.
/// All mutations happen on Unity's main thread before simulation's next update.
/// </summary>
public static class WireTopology
{
    public static bool Join(LogicWireBlock first, LogicWireBlock second)
    {
        if (first == null || second == null) return false;
        Vector3Int direction = second.GridPosition - first.GridPosition;
        int firstBit = LogicWireBlock.GetDirectionBit(direction);
        int secondBit = LogicWireBlock.GetDirectionBit(-direction);
        if (firstBit == 0) return false;

        first.SetEndpointMask(first.EndpointMask | firstBit);
        second.SetEndpointMask(second.EndpointMask | secondBit);
        first.SetJoinMask(first.JoinMask | firstBit);
        second.SetJoinMask(second.JoinMask | secondBit);
        return true;
    }

    public static void Disconnect(LogicWireBlock first, LogicWireBlock second, bool removeEndpoints = true)
    {
        if (first == null || second == null) return;
        Vector3Int direction = second.GridPosition - first.GridPosition;
        RemoveArm(first, LogicWireBlock.GetDirectionBit(direction), removeEndpoints);
        RemoveArm(second, LogicWireBlock.GetDirectionBit(-direction), removeEndpoints);
    }

    /// <summary>Intentionally attach a wire to one selected, outward-facing component pin.</summary>
    public static bool JoinPin(LogicWireBlock wire, LogicSignalBlock pin, Vector3Int pinCell, Vector3Int face)
    {
        if (wire == null || pin == null || pin is LogicWireBlock ||
            LogicWireBlock.GetDirectionBit(face) == 0 || wire.GridPosition != pinCell + face ||
            !pin.HasConnection(pinCell, face)) return false;
        int bit = LogicWireBlock.GetDirectionBit(-face);
        wire.SetEndpointMask(wire.EndpointMask | bit);
        wire.SetJoinMask(wire.JoinMask | bit);
        return true;
    }

    /// <summary>Deleting a component leaves its wires open; a replacement must be deliberately attached.</summary>
    public static void DetachPorts(LogicSignalBlock component)
    {
        if (component == null || component is LogicWireBlock) return;
        foreach (LogicSignalBlock candidate in LogicSignalBlock.All)
        {
            if (!(candidate is LogicWireBlock wire)) continue;
            foreach (Vector3Int cell in LogicSignalBlock.Neighbors(wire.GridPosition))
            {
                if (LogicSignalBlock.At(cell) != component) continue;
                RemoveArm(wire, LogicWireBlock.GetDirectionBit(cell - wire.GridPosition), false);
            }
        }
    }

    /// <summary>Remove joins into a cell before deleting that one wire.</summary>
    public static void Detach(LogicWireBlock wire)
    {
        if (wire == null) return;
        foreach (Vector3Int cell in LogicSignalBlock.Neighbors(wire.GridPosition))
        {
            if (!(LogicSignalBlock.At(cell) is LogicWireBlock neighbor)) continue;
            int bit = LogicWireBlock.GetDirectionBit(wire.GridPosition - cell);
            if ((neighbor.JoinMask & bit) != 0) RemoveArm(neighbor, bit, true);
        }
        wire.SetJoinMask(0);
    }

    /// <summary>
    /// Reconstruct the old OR rule once, after every legacy wire is loaded.
    /// Do not add endpoint arms: this preserves the authored pin contacts and
    /// avoids introducing extra connections while migrating neighboring bends.
    /// Connected visual arms also include ConnectionMask at render time.
    /// </summary>
    public static void MigrateLegacyJoins(IEnumerable<LogicWireBlock> legacyWires)
    {
        var byCell = new Dictionary<Vector3Int, LogicWireBlock>();
        foreach (LogicWireBlock wire in legacyWires)
        {
            if (wire != null) byCell[wire.GridPosition] = wire;
        }

        var masks = new Dictionary<LogicWireBlock, int>();
        foreach (LogicWireBlock wire in byCell.Values)
        {
            int mask = 0;
            foreach (Vector3Int cell in LogicSignalBlock.Neighbors(wire.GridPosition))
            {
                if (byCell.TryGetValue(cell, out LogicWireBlock neighbor) &&
                    (wire.HasEndpointTowards(cell) || neighbor.HasEndpointTowards(wire.GridPosition)))
                {
                    mask |= LogicWireBlock.GetDirectionBit(cell - wire.GridPosition);
                }
                else
                {
                    LogicSignalBlock pin = LogicSignalBlock.At(cell);
                    if (pin != null && !(pin is LogicWireBlock) && wire.HasEndpointTowards(cell) &&
                        pin.HasConnection(cell, wire.GridPosition - cell))
                        mask |= LogicWireBlock.GetDirectionBit(cell - wire.GridPosition);
                }
            }
            masks.Add(wire, mask);
        }
        foreach (var entry in masks) entry.Key.SetJoinMask(entry.Value);
    }

    /// <summary>
    /// The physical run containing the clicked cell, through bends and loops.
    /// A junction is a boundary and is retained. Clicking a junction is ambiguous
    /// and returns an empty run. A pin-attached end cell belongs to its run.
    /// </summary>
    public static List<LogicWireBlock> GetRun(LogicWireBlock clicked)
    {
        var run = new List<LogicWireBlock>();
        if (clicked == null || IsProtectedBoundary(clicked)) return run;

        var seen = new HashSet<LogicWireBlock>();
        var pending = new Stack<LogicWireBlock>();
        pending.Push(clicked);
        seen.Add(clicked);
        while (pending.Count > 0)
        {
            LogicWireBlock wire = pending.Pop();
            run.Add(wire);
            if (wire != clicked && HasAttachedPin(wire)) continue;

            foreach (Vector3Int cell in LogicSignalBlock.Neighbors(wire.GridPosition))
            {
                if (!(LogicSignalBlock.At(cell) is LogicWireBlock neighbor) ||
                    !wire.ConnectsTo(neighbor, cell) || seen.Contains(neighbor) ||
                    IsProtectedBoundary(neighbor)) continue;
                seen.Add(neighbor);
                pending.Push(neighbor);
            }
        }
        return run;
    }

    /// <summary>Delete one run atomically, preserving boundary junctions and their other arms.</summary>
    public static int DeleteRun(LogicWireBlock clicked)
    {
        List<LogicWireBlock> run = GetRun(clicked);
        if (run.Count == 0) return 0;

        var removed = new HashSet<LogicWireBlock>(run);
        foreach (LogicWireBlock wire in run)
        {
            foreach (Vector3Int cell in LogicSignalBlock.Neighbors(wire.GridPosition))
            {
                if (!(LogicSignalBlock.At(cell) is LogicWireBlock neighbor) || removed.Contains(neighbor)) continue;
                int bit = LogicWireBlock.GetDirectionBit(wire.GridPosition - cell);
                if ((neighbor.JoinMask & bit) != 0) RemoveArm(neighbor, bit, true);
            }
        }

        foreach (LogicWireBlock wire in run)
        {
            wire.SetJoinMask(0);
            wire.GetComponent<Block>()?.UnregisterCells();
            // Remove registry entries now; deferred Destroy must not let the
            // simulation or another placement see half of the deleted run.
            wire.gameObject.SetActive(false);
            if (Application.isPlaying) Object.Destroy(wire.gameObject);
            else Object.DestroyImmediate(wire.gameObject);
        }
        LogicSignalBlock.MarkCircuitChanged();
        WorldSaveSystem.Instance?.MarkDirty();
        return run.Count;
    }

    private static void RemoveArm(LogicWireBlock wire, int bit, bool removeEndpoint)
    {
        wire.SetJoinMask(wire.JoinMask & ~bit);
        if (removeEndpoint) wire.SetEndpointMask(wire.EndpointMask & ~bit);
    }

    private static bool IsProtectedBoundary(LogicWireBlock wire)
    {
        Block block = wire.GetComponent<Block>();
        if (block != null && !block.breakable) return true;

        int physicalMask = wire.EndpointMask;
        foreach (Vector3Int cell in LogicSignalBlock.Neighbors(wire.GridPosition))
        {
            if (wire.ConnectsTo(LogicSignalBlock.At(cell), cell))
                physicalMask |= LogicWireBlock.GetDirectionBit(cell - wire.GridPosition);
        }
        int count = 0;
        for (; physicalMask != 0; physicalMask &= physicalMask - 1) count++;
        return count >= 3;
    }

    private static bool HasAttachedPin(LogicWireBlock wire)
    {
        foreach (Vector3Int cell in LogicSignalBlock.Neighbors(wire.GridPosition))
        {
            LogicSignalBlock neighbor = LogicSignalBlock.At(cell);
            if (neighbor != null && !(neighbor is LogicWireBlock) && wire.ConnectsTo(neighbor, cell))
                return true;
        }
        return false;
    }
}
