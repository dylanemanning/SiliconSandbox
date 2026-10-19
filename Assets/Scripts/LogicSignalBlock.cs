using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public abstract class LogicSignalBlock : MonoBehaviour
{
    private static readonly Dictionary<Vector3Int, LogicSignalBlock> blocks =
        new Dictionary<Vector3Int, LogicSignalBlock>();

    private static int circuitVersion;

    // Every cell this block holds. One for a 1x1x1 block; a multi-cell block
    // (Block.footprint, e.g. a 2x1x2 gate) holds all of them, each pointing back
    // at the block, so At(cell) finds it from any of its cells.
    private readonly List<Vector3Int> registeredCells = new List<Vector3Int>();
    private Vector3Int lastAnchor, lastFootprint;
    private Quaternion lastRotation;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetRegistry() => blocks.Clear();

    public Vector3Int GridPosition => Vector3Int.RoundToInt(transform.position);

    public virtual int SignalState => 0;

    /// <summary>
    /// Goes up by one every time the circuit changes: a block joins or leaves
    /// the grid, or a source changes value. The simulation driver (#82)
    /// compares it once a frame and re-evaluates only when it has moved, so
    /// nothing is recomputed while the world sits still.
    /// </summary>
    public static int CircuitVersion => circuitVersion;

    /// <summary>
    /// Tells the simulation driver the circuit needs re-evaluating. Blocks call
    /// this themselves when they register or unregister; call it for any other
    /// change that affects signals (LogicSignalSource does, when its value changes).
    /// </summary>
    public static void MarkCircuitChanged()
    {
        circuitVersion++;
    }

    protected virtual void OnEnable()
    {
        if (Register()) MarkCircuitChanged();
    }

    protected virtual void OnDisable()
    {
        if (Unregister()) MarkCircuitChanged();
    }

    protected virtual void Update()
    {
        // Picks up a cell freed by another block since this one was enabled.
        if (Register()) MarkCircuitChanged();
    }

    /// <summary>
    /// Adds this block to the grid registry. Returns true only if it was not
    /// already there, so the per-frame call in Update does not count as a change.
    /// </summary>
    protected bool Register()
    {
        var block = GetComponent<Block>();
        Vector3Int anchor = GridPosition;
        Vector3Int footprint = block != null ? block.footprint : Vector3Int.one;
        Quaternion rotation = transform.rotation;

        // Already registered where it is now: no change. This is the per-frame case.
        if (registeredCells.Count > 0 && lastAnchor == anchor && lastFootprint == footprint
            && lastRotation == rotation
            && blocks.TryGetValue(anchor, out LogicSignalBlock registered) && registered == this)
        {
            return false;
        }

        var cells = block != null
            ? new List<Vector3Int>(block.OccupiedCells(anchor, rotation))
            : new List<Vector3Int> { anchor };

        // Moved, rotated or resized: drop the old cells first. That alone is a change.
        bool changed = Unregister();

        foreach (Vector3Int cell in cells)
        {
            // Another block holds one of the cells: stay out until it frees up.
            if (blocks.TryGetValue(cell, out LogicSignalBlock other) && other != this) return changed;
        }

        foreach (Vector3Int cell in cells)
        {
            blocks[cell] = this;
            registeredCells.Add(cell);
        }

        lastAnchor = anchor;
        lastFootprint = footprint;
        lastRotation = rotation;
        return true;
    }

    private bool Unregister()
    {
        bool removed = false;
        foreach (Vector3Int cell in registeredCells)
        {
            if (blocks.TryGetValue(cell, out LogicSignalBlock other) && other == this)
            {
                blocks.Remove(cell);
                removed = true;
            }
        }

        registeredCells.Clear();
        return removed;
    }

    /// <summary>
    /// Every registered block, once each. A multi-cell block holds several cells
    /// in the registry, so this de-duplicates. The netlist builder (#18) builds
    /// the circuit from this.
    /// </summary>
    public static IEnumerable<LogicSignalBlock> All => blocks.Values.Distinct();

    // Which faces a block connects on. Used by the wire junction visuals; the
    // netlist builder (#18) has its own port rules (NetlistBuilder.Blocks.cs).
    public virtual bool HasConnection(Vector3Int cell, Vector3Int face) => cell == GridPosition;
    public virtual bool CanDrive(Vector3Int cell, Vector3Int face) => HasConnection(cell, face);

    public static LogicSignalBlock At(Vector3Int position)
    {
        blocks.TryGetValue(position, out LogicSignalBlock block);
        return block;
    }

    public static IEnumerable<Vector3Int> Neighbors(Vector3Int position)
    {
        yield return position + Vector3Int.right;
        yield return position + Vector3Int.left;
        yield return position + Vector3Int.up;
        yield return position + Vector3Int.down;
        yield return position + new Vector3Int(0, 0, 1);
        yield return position + new Vector3Int(0, 0, -1);
    }

    public static Vector3Int DirectionToNeighbor(Vector3Int from, Vector3Int neighbor)
    {
        return neighbor - from;
    }
}