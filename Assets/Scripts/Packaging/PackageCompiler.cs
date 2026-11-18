// PackageCompiler.cs — turns the circuit inside the wand's box into a package draft.
//
// Rules (FR-5, adapted from the personal packaging specification):
//   - Whole components only. A gate, source, LED, wire or package is captured when
//     every cell it occupies is inside the box; one that is only partly inside
//     rejects the draft, so a component is never cut.
//   - Ports come from the box boundary: a captured wire with an arm crossing the
//     boundary, or a captured component pin on the boundary facing out. A pin or
//     wire end deep inside the box is not exposed.
//   - A wire run that touches nothing inside the box is a pass-through and is not
//     a port.
//   - Direction is inferred: something inside drives the port's net -> output;
//     otherwise input. The player can rename ports, change direction and reorder
//     them before creating the package.
//   - Clocks are not packaged yet: a package's internals have no clock of their own.
//   - The source circuit stays in the world unchanged.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class PackageDraft
{
    public Vector3Int Min, Max;
    public Vector3Int Size => Max - Min + Vector3Int.one;
    public readonly List<PackagedPart> Parts = new List<PackagedPart>();
    public readonly List<PackagePort> Ports = new List<PackagePort>();
    public readonly List<string> Errors = new List<string>();
    public bool IsValid => Errors.Count == 0;
    public int ComponentCount => Parts.Count(p => p.kind != PackagedPartKind.Wire);
    public int WireCount => Parts.Count(p => p.kind == PackagedPartKind.Wire);

    /// <summary>Builds the fixed definition from the draft and the player's choices.</summary>
    public PackageDefinition Build(string name, string marking)
    {
        if (!IsValid) throw new InvalidOperationException("The package draft has errors.");
        string fullName = string.IsNullOrWhiteSpace(name) ? "Package" : name.Trim();
        if (fullName.Length > PackageDefinition.MaxNameLength) fullName = fullName.Substring(0, PackageDefinition.MaxNameLength);
        string top = string.IsNullOrWhiteSpace(marking) ? PackageDefinition.SuggestMarking(fullName) : marking.Trim();
        if (top.Length > PackageDefinition.MaxMarkingLength) top = top.Substring(0, PackageDefinition.MaxMarkingLength);
        var definition = new PackageDefinition
        {
            id = Guid.NewGuid().ToString("D"),
            name = fullName,
            marking = top,
            size = Size,
        };
        definition.parts.AddRange(Parts);
        // Inputs first, then outputs, each in the player's order.
        definition.ports.AddRange(Ports.Where(p => p.isInput));
        definition.ports.AddRange(Ports.Where(p => !p.isInput));
        return JsonUtility.FromJson<PackageDefinition>(JsonUtility.ToJson(definition));
    }
}

public static class PackageCompiler
{
    public const int MaxBoxSize = 64;

    public static PackageDraft Analyze(Vector3Int cornerA, Vector3Int cornerB)
        => Analyze(cornerA, cornerB, LogicSignalBlock.All);

    public static PackageDraft Analyze(Vector3Int cornerA, Vector3Int cornerB, IEnumerable<LogicSignalBlock> blocks)
    {
        var draft = new PackageDraft { Min = Vector3Int.Min(cornerA, cornerB), Max = Vector3Int.Max(cornerA, cornerB) };
        Vector3Int size = draft.Size;
        if (size.x > MaxBoxSize || size.y > MaxBoxSize || size.z > MaxBoxSize)
        {
            draft.Errors.Add($"The box is too large. Keep each side at most {MaxBoxSize} blocks.");
            return draft;
        }

        bool Inside(Vector3Int cell) => cell.x >= draft.Min.x && cell.x <= draft.Max.x &&
            cell.y >= draft.Min.y && cell.y <= draft.Max.y && cell.z >= draft.Min.z && cell.z <= draft.Max.z;

        var captured = new List<(LogicSignalBlock block, List<Vector3Int> cells)>();
        foreach (var block in blocks.Where(b => b != null).Distinct()
                     .OrderBy(b => b.GridPosition.x).ThenBy(b => b.GridPosition.y).ThenBy(b => b.GridPosition.z))
        {
            var cells = CellsOf(block);
            int inside = cells.Count(Inside);
            if (inside == 0) continue;
            if (inside < cells.Count)
            {
                draft.Errors.Add($"{DisplayName(block)} at {block.GridPosition} is only partly inside the box. Include all of it or none of it.");
                continue;
            }
            if (block is LogicClockSource)
            {
                draft.Errors.Add($"The clock at {block.GridPosition} can't be packaged yet. Leave clocks outside the box and wire them to an input.");
                continue;
            }
            captured.Add((block, cells));
        }
        if (captured.Count == 0) draft.Errors.Add("The box contains no circuit components.");
        if (!draft.IsValid) return draft;

        foreach (var (block, _) in captured)
        {
            var part = new PackagedPart { cell = block.GridPosition - draft.Min, rotation = block.transform.rotation };
            switch (block)
            {
                case LogicWireBlock wire: part.kind = PackagedPartKind.Wire; part.wireMask = wire.EndpointMask; break;
                case LogicSignalSource source: part.kind = PackagedPartKind.Source; part.sourceState = source.SignalState; break;
                case StateViewerBlock _: part.kind = PackagedPartKind.Led; break;
                case GateBlock gate: part.kind = PackagedPartKind.Gate; part.gateType = gate.GateType; break;
                case PackageBlock package: part.kind = PackagedPartKind.Package; part.packageId = package.PackageId; break;
                default: continue;
            }
            draft.Parts.Add(part);
        }

        // Port candidates at the boundary.
        var candidates = new List<(PackagePort port, LogicSignalBlock block)>();
        foreach (var (block, cells) in captured)
        {
            if (block is LogicWireBlock wire)
            {
                foreach (Vector3Int face in NetlistBuilder.AllFaces)
                    if (wire.HasEndpointTowards(wire.GridPosition + face) && !Inside(wire.GridPosition + face))
                        candidates.Add((new PackagePort { insideCell = wire.GridPosition - draft.Min, outward = face }, block));
                continue;
            }
            foreach (Vector3Int cell in cells)
            foreach (Vector3Int face in NetlistBuilder.AllFaces)
            {
                if (Inside(cell + face) || !block.HasConnection(cell, face)) continue;
                // An LED is a display; its pins are not useful package ports.
                if (block is StateViewerBlock) continue;
                candidates.Add((new PackagePort
                {
                    insideCell = cell - draft.Min, outward = face, isInput = !block.CanDrive(cell, face)
                }, block));
            }
        }

        // Infer wire port directions from the captured circuit alone, and drop pass-throughs.
        var probe = new PackageDefinition { id = "draft", name = "draft", size = size };
        probe.parts.AddRange(draft.Parts);
        Netlist netlist = InternalNetlist(probe, out var partCells);
        foreach (var (port, block) in candidates)
        {
            if (block is LogicWireBlock)
            {
                if (!partCells.TryGetValue(port.insideCell, out int part)) continue;
                int network = netlist.NetworkOfPart[part];
                if (network < 0) continue;
                bool driven = netlist.Networks[network].DriverNodes.Length > 0;
                bool loaded = HasSink(netlist, network);
                if (!driven && !loaded) continue; // pass-through
                port.isInput = !driven;
            }
            draft.Ports.Add(port);
        }
        if (draft.Ports.Count == 0)
            draft.Errors.Add("No ports were found. Run a wire from the circuit to the edge of the box, or put a pin on the edge, for each input and output.");

        // Default names and order: left to right, then bottom to top.
        var ordered = draft.Ports.OrderBy(p => p.insideCell.x).ThenBy(p => p.insideCell.y).ThenBy(p => p.insideCell.z).ToList();
        draft.Ports.Clear();
        draft.Ports.AddRange(ordered.Where(p => p.isInput));
        draft.Ports.AddRange(ordered.Where(p => !p.isInput));
        NameDefaults(draft.Ports);
        return draft;
    }

    /// <summary>A..Z for inputs, Y / Y0, Y1... for outputs.</summary>
    public static void NameDefaults(List<PackagePort> ports)
    {
        int inputs = 0, outputs = 0;
        int outputCount = ports.Count(p => !p.isInput);
        foreach (var port in ports)
        {
            if (port.isInput)
            {
                int i = inputs++;
                port.name = i < 26 ? ((char)('A' + i)).ToString() : "IN" + (i + 1);
            }
            else port.name = outputCount == 1 ? "Y" : "Y" + outputs++;
        }
    }

    // True when a captured input pin faces a wire of the network.
    private static bool HasSink(Netlist netlist, int network) => SinkTouches(netlist, network);

    private static bool SinkTouches(Netlist netlist, int network)
    {
        for (int node = 0; node < netlist.NodeCount; node++)
        {
            var part = netlist.PartList[netlist.NodeParts[node]];
            foreach (var port in part.Ports)
            {
                if (port.IsOutput) continue;
                if (netlist.CellToPart.TryGetValue(port.FacingCell, out int other) && netlist.NetworkOfPart[other] == network)
                    return true;
            }
        }
        return false;
    }

    private static Netlist InternalNetlist(PackageDefinition definition, out Dictionary<Vector3Int, int> partCells)
    {
        // Lay the captured parts out at their own relative cells, without port bridges.
        var parts = new List<CircuitPart>();
        partCells = new Dictionary<Vector3Int, int>();
        foreach (var captured in definition.parts)
        {
            CircuitPart part;
            Vector3Int cell = captured.cell;
            switch (captured.kind)
            {
                case PackagedPartKind.Wire:
                    part = new CircuitPart { Kind = PartKind.Wire, Cell = cell, WireEndpointMask = captured.wireMask }; break;
                case PackagedPartKind.Source:
                    var forward = Vector3Int.RoundToInt(captured.rotation * Vector3.forward);
                    part = new CircuitPart { Kind = PartKind.Source, Cell = cell, Ports = new[] { PartPort.Output(cell, forward), PartPort.Output(cell, -forward) } }; break;
                case PackagedPartKind.Led:
                    part = new CircuitPart { Kind = PartKind.Output, Cell = cell, Ports = NetlistBuilder.AllFaces.Select(f => PartPort.Input(cell, f, 0)).ToArray() }; break;
                case PackagedPartKind.Gate:
                    part = new CircuitPart
                    {
                        Kind = PartKind.Gate, Cell = cell, GateType = captured.gateType,
                        Ports = GatePorts.For(captured.gateType).Select(p =>
                        {
                            var (c, f) = GatePorts.ToWorld(p, cell, captured.rotation);
                            return p.Role == PortRole.Output ? PartPort.Output(c, f) : PartPort.Input(c, f, p.InputIndex);
                        }).ToArray()
                    }; break;
                case PackagedPartKind.Package:
                    // A nested package behaves like a gate with its own ports.
                    var child = PackageLibrary.Get(captured.packageId);
                    var childPorts = new List<PartPort>();
                    if (child != null)
                        for (int i = 0; i < child.ports.Count; i++)
                        {
                            var (c, f) = child.WorldPort(i, cell, captured.rotation);
                            childPorts.Add(child.ports[i].isInput ? PartPort.Input(c, f, i) : PartPort.Output(c, f));
                        }
                    part = new CircuitPart { Kind = PartKind.Gate, Cell = cell, GateType = GateType.Buffer, Ports = childPorts.ToArray() }; break;
                default: continue;
            }
            partCells[cell] = parts.Count;
            parts.Add(part);
        }
        var netlist = NetlistBuilder.FromParts(parts);
        netlist.PartList = parts;
        netlist.CellToPart = partCells;
        return netlist;
    }

    private static List<Vector3Int> CellsOf(LogicSignalBlock block)
    {
        var owner = block.GetComponent<Block>();
        return owner != null
            ? owner.OccupiedCells(block.GridPosition, block.transform.rotation).ToList()
            : new List<Vector3Int> { block.GridPosition };
    }

    private static string DisplayName(LogicSignalBlock block)
    {
        switch (block)
        {
            case GateBlock gate: return gate.GateType.ToString().ToUpperInvariant() + " gate";
            case LogicClockSource _: return "Clock";
            case LogicSignalSource _: return "Constant Signal";
            case StateViewerBlock _: return "LED";
            case LogicWireBlock _: return "Wire";
            case PackageBlock package: return "Package " + (package.Definition?.name ?? "");
            default: return block.name;
        }
    }
}
