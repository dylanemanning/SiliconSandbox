// NetlistBuilder.cs — turns placed parts into a netlist the logic engine can
// evaluate (#18).
//
// This file is the pure core: it works on CircuitParts (a kind, a cell, and
// ports in world space) and never touches a GameObject, so it can be tested
// from hand-written parts. NetlistBuilder.Blocks.cs is the thin adapter that
// turns placed LogicSignalBlocks into parts.
//
// Connection rules (demo version):
//
//   - Wire to wire: two wire blocks connect if they share a face — all six
//     neighbours, the same rule Dylan's LogicWireBlock uses to draw junctions.
//     The end-face rule (decision 2; parallel wires stay separate, crossings go
//     over/under) replaces this with #63 after the demo, and only
//     WireNeighbours below needs to change.
//
//   - Part to wire: a port connects to a wire in the cell it faces. Sources and
//     LEDs have a port on every face; a gate only on the faces GatePorts lists.
//     A wire beside a gate's side with no port is not connected.
//
//   - Part to part, no wire between: an output connects straight to an input
//     when each faces the other's cell. So an LED placed right against a gate's
//     output lights up, and two gates can be butted together.
//
//   - One driver per network (decision 1): a network touched by two or more
//     outputs is a short circuit. Nothing is connected from it, and the driver
//     draws it red. Its inputs read 0 meanwhile. Combine signals with an OR gate.
//
//   - One driver per input pin: if an input ends up fed from two places (an
//     LED touching two separately driven networks, say), the first is kept and
//     the rest are reported in Netlist.Warnings. The engine would otherwise
//     reject the whole netlist.
//
// Node order follows part order, and FromBlocks sorts parts by cell, so the
// same world always produces the same netlist. That keeps bug reports and test
// output reproducible.

using System;
using System.Collections.Generic;
using UnityEngine;

public static partial class NetlistBuilder
{
    // The six face neighbours. Kept in one place because #63 replaces wire
    // adjacency with the end-face rule, and part ports already use their own.
    private static readonly Vector3Int[] Faces =
    {
        new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
        new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
        new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
    };

    /// <summary>The six unit directions, for building Source and Output ports.</summary>
    public static IReadOnlyList<Vector3Int> AllFaces => Faces;

    /// <summary>
    /// Builds a netlist from parts. Never throws for a bad circuit: anything
    /// unusual is resolved by the rules above and recorded in Warnings.
    /// </summary>
    public static Netlist FromParts(IReadOnlyList<CircuitPart> parts)
    {
        if (parts == null) throw new ArgumentNullException(nameof(parts));

        var netlist = new Netlist();
        int partCount = parts.Count;

        // 1. Cell lookup. The grid registry already keeps one block per cell,
        //    so a clash here means two parts were handed in for the same cell.
        var partAt = new Dictionary<Vector3Int, int>(partCount);
        for (int p = 0; p < partCount; p++)
        {
            if (parts[p] == null) continue;
            if (!partAt.ContainsKey(parts[p].Cell))
            {
                partAt.Add(parts[p].Cell, p);
            }
            else
            {
                netlist.Warnings.Add($"{parts[p]} ignored: {parts[partAt[parts[p].Cell]]} is on the same cell.");
            }
        }

        bool IsLive(int p) => parts[p] != null && partAt.TryGetValue(parts[p].Cell, out int owner) && owner == p;

        // 2. Nodes: every live part that is not a wire, in part order.
        var nodes = new List<NetlistNode>();
        var nodeParts = new List<int>();
        var nodeOfPart = new int[partCount];
        for (int p = 0; p < partCount; p++)
        {
            nodeOfPart[p] = -1;
            if (!IsLive(p) || parts[p].Kind == PartKind.Wire) continue;

            GateType type = NodeType(parts[p]);
            if (type == GateType.Invalid)
            {
                netlist.Warnings.Add($"{parts[p]} skipped: {parts[p].GateType} is not a gate type.");
                continue;
            }

            nodeOfPart[p] = nodes.Count;
            nodes.Add(new NetlistNode(nodes.Count, type));
            nodeParts.Add(p);
        }

        // 3. Wire networks: flood fill over face-adjacent wire blocks.
        var networkOfPart = new int[partCount];
        for (int p = 0; p < partCount; p++) networkOfPart[p] = -1;

        var networkWires = new List<List<int>>();
        var pending = new Stack<int>();
        for (int start = 0; start < partCount; start++)
        {
            if (!IsLive(start) || parts[start].Kind != PartKind.Wire || networkOfPart[start] >= 0) continue;

            int network = networkWires.Count;
            var wires = new List<int>();
            networkWires.Add(wires);

            networkOfPart[start] = network;
            pending.Push(start);
            while (pending.Count > 0)
            {
                int wire = pending.Pop();
                wires.Add(wire);
                foreach (Vector3Int face in Faces)
                {
                    if (partAt.TryGetValue(parts[wire].Cell + face, out int next) &&
                        parts[next].Kind == PartKind.Wire &&
                        networkOfPart[next] < 0)
                    {
                        networkOfPart[next] = network;
                        pending.Push(next);
                    }
                }
            }
            wires.Sort(); // flood-fill order depends on the stack; part order does not
        }

        // 4. Ports. Each node port either faces a wire (joins that network), or
        //    faces another part (a direct link, checked from the output side
        //    only so each link is found once), or faces nothing.
        int networkCount = networkWires.Count;
        var drivers = new List<SortedSet<int>>(networkCount);   // node indices
        var sinks = new List<List<(int node, int pin)>>(networkCount);
        for (int n = 0; n < networkCount; n++)
        {
            drivers.Add(new SortedSet<int>());
            sinks.Add(new List<(int, int)>());
        }
        var directLinks = new List<(int from, int to, int pin)>();

        for (int node = 0; node < nodes.Count; node++)
        {
            CircuitPart part = parts[nodeParts[node]];
            foreach (PartPort port in PortsOf(part))
            {
                if (!partAt.TryGetValue(port.FacingCell, out int other)) continue;

                int network = networkOfPart[other];
                if (network >= 0)
                {
                    if (port.IsOutput)
                    {
                        drivers[network].Add(node);
                    }
                    else if (!sinks[network].Contains((node, port.Pin)))
                    {
                        // A source or LED can touch one network on several faces;
                        // it is still one terminal.
                        sinks[network].Add((node, port.Pin));
                    }
                    continue;
                }

                int otherNode = nodeOfPart[other];
                if (!port.IsOutput || otherNode < 0) continue;

                foreach (PartPort back in PortsOf(parts[other]))
                {
                    if (!back.IsOutput && back.FacingCell == port.OnCell && back.OnCell == port.FacingCell)
                    {
                        directLinks.Add((node, otherNode, back.Pin));
                    }
                }
            }
        }

        // 5. Connections. Networks first, in network order, then direct links.
        var connections = new List<NetlistConnection>();
        var fedPins = new Dictionary<(int node, int pin), (int from, string via)>();

        void Connect(int from, int to, int pin, string via)
        {
            if (fedPins.TryGetValue((to, pin), out var first))
            {
                // The same driver reaching the same pin twice (an LED touching a
                // source directly and through the source's own wire, say) is one
                // signal, not a conflict: nothing to add, nothing to report.
                if (first.from != from)
                {
                    netlist.Warnings.Add(
                        $"{parts[nodeParts[to]]} input {pin} is fed by {via} as well as by {first.via}; keeping {first.via}.");
                }
                return;
            }
            fedPins.Add((to, pin), (from, via));
            connections.Add(new NetlistConnection(from, to, pin));
        }

        var networks = new WireNetwork[networkCount];
        for (int n = 0; n < networkCount; n++)
        {
            networks[n] = new WireNetwork
            {
                WireParts = networkWires[n].ToArray(),
                DriverNodes = new List<int>(drivers[n]).ToArray(),
            };

            if (networks[n].DriverNode < 0) continue; // floating or shorted: connect nothing

            int driver = networks[n].DriverNode;
            foreach ((int node, int pin) in sinks[n])
            {
                // A gate whose output is wired back to its own input lands here
                // too. That is a feedback loop, which the engine reports.
                Connect(driver, node, pin, $"wire network {n}");
            }
        }

        foreach ((int from, int to, int pin) in directLinks)
        {
            Connect(from, to, pin, $"{parts[nodeParts[from]]} directly");
        }

        netlist.Nodes = nodes.ToArray();
        netlist.Connections = connections.ToArray();
        netlist.NodeParts = nodeParts.ToArray();
        netlist.Networks = networks;
        netlist.NetworkOfPart = networkOfPart;
        return netlist;
    }

    private static IReadOnlyList<PartPort> PortsOf(CircuitPart part)
        => part.Ports ?? (IReadOnlyList<PartPort>)Array.Empty<PartPort>();

    // The engine node type for a non-wire part, or Invalid to skip it.
    private static GateType NodeType(CircuitPart part)
    {
        switch (part.Kind)
        {
            case PartKind.Source: return GateType.Source;
            case PartKind.Output: return GateType.Output;
            case PartKind.Gate:
                return IsGate(part.GateType) ? part.GateType : GateType.Invalid;
            default:
                return GateType.Invalid;
        }
    }

    private static bool IsGate(GateType type)
        => type != GateType.Invalid && type != GateType.Source && type != GateType.Output &&
           Enum.IsDefined(typeof(GateType), type);
}
