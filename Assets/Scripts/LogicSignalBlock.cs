using System.Collections.Generic;
using UnityEngine;

public abstract class LogicSignalBlock : MonoBehaviour
{
    private static readonly Dictionary<Vector3Int, LogicSignalBlock> blocks =
        new Dictionary<Vector3Int, LogicSignalBlock>();

    private static int circuitVersion;

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
        Vector3Int position = GridPosition;
        if (blocks.ContainsKey(position))
        {
            // Either this block is already registered, or another block holds
            // the cell. Both are "no change".
            return false;
        }

        blocks[position] = this;
        return true;
    }

    private bool Unregister()
    {
        Vector3Int position = GridPosition;
        if (blocks.TryGetValue(position, out LogicSignalBlock existing) && existing == this)
        {
            blocks.Remove(position);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Every registered block, one per grid cell. The netlist builder (#18)
    /// builds the circuit from this.
    /// </summary>
    public static IEnumerable<LogicSignalBlock> All => blocks.Values;

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
