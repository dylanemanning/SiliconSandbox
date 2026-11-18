// NetlistBuilder.Packages.cs — expands placed packages into circuit parts (FR-5).
//
// Every placed package contributes a private copy of its captured circuit. The
// copy is laid out in an unused region of grid space far from the world (a
// "virtual" origin per copy), so its cells can never touch world cells or another
// copy. Ports are bridged with BUFFER gates:
//
//   input port:  outside driver -> package face pin -> BUFFER -> captured cell
//   output port: captured cell -> BUFFER -> package face pin -> outside load
//
// The BUFFER's world-side pin sits on the package's face exactly like a gate
// pin, so wires and directly touching components connect to it with the normal
// rules. Its inside pin faces the captured wire or component pin the port was
// made from, across the original box boundary. Nested packages expand the same
// way inside their parent's copy.

using System.Collections.Generic;
using UnityEngine;

public static partial class NetlistBuilder
{
    private const int MaxPackageDepth = 12;

    private sealed class PackageExpansion
    {
        // Copies are spaced along X starting far below the world.
        private static readonly Vector3Int Base = new Vector3Int(-1_000_000, -1_000_000, -1_000_000);
        private const int Stride = 1024;
        private int next;
        public readonly Dictionary<int, int> SourceStateOfPart = new Dictionary<int, int>();
        public readonly List<(PackageBlock block, int port, int part)> PortParts = new List<(PackageBlock, int, int)>();

        public Vector3Int NextOrigin() => Base + new Vector3Int(Stride * next++, 0, 0);

        public void Publish(Netlist netlist)
        {
            var nodeOfPart = new Dictionary<int, int>();
            for (int node = 0; node < netlist.NodeCount; node++) nodeOfPart[netlist.NodeParts[node]] = node;
            foreach (var entry in SourceStateOfPart)
                if (nodeOfPart.TryGetValue(entry.Key, out int node)) netlist.FixedSourceStates[node] = entry.Value;
            foreach (var (block, port, part) in PortParts)
                if (nodeOfPart.TryGetValue(part, out int node)) netlist.PackagePorts.Add((block, port, node));
        }
    }

    private static void AddPackage(PackageDefinition definition, Vector3Int anchor, Quaternion rotation,
        PackageBlock owner, bool topLevel, List<CircuitPart> parts, List<LogicSignalBlock> partBlocks,
        List<string> warnings, PackageExpansion expansion, int depth)
    {
        if (definition == null)
        {
            warnings.Add($"{(owner != null ? owner.name : "A package")} at {anchor} skipped: its definition is missing.");
            return;
        }
        if (depth > MaxPackageDepth)
        {
            warnings.Add($"Package '{definition.name}' skipped: packages are nested more than {MaxPackageDepth} deep.");
            return;
        }

        Vector3Int origin = expansion.NextOrigin();
        foreach (PackagedPart captured in definition.parts)
        {
            Vector3Int cell = origin + captured.cell;
            CircuitPart part = null;
            switch (captured.kind)
            {
                case PackagedPartKind.Wire:
                    part = new CircuitPart { Kind = PartKind.Wire, Cell = cell, WireEndpointMask = captured.wireMask };
                    break;
                case PackagedPartKind.Source:
                    Vector3Int forward = Vector3Int.RoundToInt(captured.rotation * Vector3.forward);
                    part = new CircuitPart
                    {
                        Kind = PartKind.Source, Cell = cell,
                        Ports = new[] { PartPort.Output(cell, forward), PartPort.Output(cell, -forward) },
                    };
                    expansion.SourceStateOfPart[parts.Count] = captured.sourceState == 0 ? 0 : 1;
                    break;
                case PackagedPartKind.Led:
                    var ledPorts = new PartPort[Faces.Length];
                    for (int f = 0; f < Faces.Length; f++) ledPorts[f] = PartPort.Input(cell, Faces[f], 0);
                    part = new CircuitPart { Kind = PartKind.Output, Cell = cell, Ports = ledPorts };
                    break;
                case PackagedPartKind.Gate:
                    if (!IsGate(captured.gateType)) { warnings.Add($"Package '{definition.name}' has an invalid gate."); break; }
                    var gatePorts = new List<PartPort>();
                    foreach (GatePort port in GatePorts.For(captured.gateType))
                    {
                        var (portCell, face) = GatePorts.ToWorld(port, cell, captured.rotation);
                        gatePorts.Add(port.Role == PortRole.Output
                            ? PartPort.Output(portCell, face) : PartPort.Input(portCell, face, port.InputIndex));
                    }
                    part = new CircuitPart { Kind = PartKind.Gate, Cell = cell, GateType = captured.gateType, Ports = gatePorts.ToArray() };
                    break;
                case PackagedPartKind.Package:
                    AddPackage(PackageLibrary.Get(captured.packageId), cell, captured.rotation, owner, false,
                        parts, partBlocks, warnings, expansion, depth + 1);
                    break;
            }
            if (part == null) continue;
            parts.Add(part);
            partBlocks.Add(owner);
        }

        // Bridge every port with a BUFFER between the package face and the captured circuit.
        for (int i = 0; i < definition.ports.Count; i++)
        {
            PackagePort port = definition.ports[i];
            var (outerCell, outerFace) = definition.WorldPort(i, anchor, rotation);
            Vector3Int inside = origin + port.insideCell;
            Vector3Int bridge = inside + port.outward;
            PartPort[] ports = port.isInput
                ? new[] { PartPort.Input(outerCell, outerFace, 0), new PartPort(bridge, inside, -1) }
                : new[] { new PartPort(bridge, inside, 0), PartPort.Output(outerCell, outerFace) };
            if (topLevel) expansion.PortParts.Add((owner, i, parts.Count));
            parts.Add(new CircuitPart { Kind = PartKind.Gate, Cell = bridge, GateType = GateType.Buffer, Ports = ports });
            partBlocks.Add(owner);
        }
    }
}
