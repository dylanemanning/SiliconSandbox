// NetlistBuilder.Blocks.cs — turns placed LogicSignalBlocks into CircuitParts
// and builds the netlist from them (#18).
//
// The only part of the builder that knows about Unity components. The rules
// live in NetlistBuilder.cs; this file just says what each block type looks
// like as a part:
//
//   LogicWireBlock     Wire
//   LogicSignalSource  Source, an output on all six faces
//   StateViewerBlock   Output (LED), input pin 0 on all six faces
//   GateBlock          Gate, ports from GateBlock.WorldPorts (the GatePorts table)
//
// Other LogicSignalBlock subclasses are ignored.
//
// The blocks are passed in rather than read from the LogicSignalBlock grid
// registry, so tests can build circuits from GameObjects in EditMode (where
// OnEnable never runs and the registry stays empty). In play mode the
// simulation driver passes LogicSignalBlock.All.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static partial class NetlistBuilder
{
    /// <summary>
    /// Builds a netlist from placed blocks. Null and destroyed blocks are skipped.
    /// Netlist.Blocks maps every part index back to its block.
    /// </summary>
    public static Netlist FromBlocks(IEnumerable<LogicSignalBlock> blocks)
    {
        if (blocks == null) throw new ArgumentNullException(nameof(blocks));

        // Sorted by cell so the same world always gives the same node order.
        List<LogicSignalBlock> ordered = blocks
            .Where(b => b != null) // Unity's == also catches destroyed blocks
            .Distinct()
            .OrderBy(b => b.GridPosition.x)
            .ThenBy(b => b.GridPosition.y)
            .ThenBy(b => b.GridPosition.z)
            .ToList();

        var parts = new List<CircuitPart>(ordered.Count);
        var partBlocks = new List<LogicSignalBlock>(ordered.Count);
        var warnings = new List<string>();

        foreach (LogicSignalBlock block in ordered)
        {
            CircuitPart part = ToPart(block, warnings);
            if (part == null) continue;
            parts.Add(part);
            partBlocks.Add(block);
        }

        Netlist netlist = FromParts(parts);
        netlist.Blocks = partBlocks;
        netlist.Warnings.InsertRange(0, warnings);
        return netlist;
    }

    // The part for one block, or null to leave it out.
    private static CircuitPart ToPart(LogicSignalBlock block, List<string> warnings)
    {
        Vector3Int cell = block.GridPosition;

        switch (block)
        {
            case LogicWireBlock _:
                return new CircuitPart { Kind = PartKind.Wire, Cell = cell };

            case LogicSignalSource _:
                return new CircuitPart
                {
                    Kind = PartKind.Source,
                    Cell = cell,
                    Ports = Faces.Select(f => PartPort.Output(cell, f)).ToArray(),
                };

            case StateViewerBlock _:
                return new CircuitPart
                {
                    Kind = PartKind.Output,
                    Cell = cell,
                    Ports = Faces.Select(f => PartPort.Input(cell, f, 0)).ToArray(),
                };

            case GateBlock gate:
                try
                {
                    PartPort[] ports = gate.WorldPorts()
                        .Select(w => w.port.Role == PortRole.Output
                            ? PartPort.Output(w.cell, w.face)
                            : PartPort.Input(w.cell, w.face, w.port.InputIndex))
                        .ToArray();
                    return new CircuitPart { Kind = PartKind.Gate, Cell = cell, GateType = gate.GateType, Ports = ports };
                }
                catch (ArgumentException)
                {
                    // GatePorts.For throws for Invalid / Source / Output: a prefab
                    // with its gate type never set. Skip it rather than stop the
                    // whole circuit simulating.
                    warnings.Add($"{gate.name} at {cell} skipped: its GateType is {gate.GateType}, which is not a gate.");
                    return null;
                }

            default:
                return null;
        }
    }
}
