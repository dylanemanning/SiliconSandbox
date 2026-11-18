// PackageDefinition.cs — a packaged circuit (FR-5).
//
// Packaging copies the circuit inside the wand's box into a fixed definition: every
// captured wire, source, gate, LED and nested package, at its position inside the
// box, plus the ports where the circuit crosses the box boundary. The definition
// is independent of the source circuit, which stays in the world unchanged.
//
// A placed package (PackageBlock) refers to a definition by id. The simulation
// expands each placed package into its own private copy of the captured circuit,
// so every placement has independent signals (NetlistBuilder.Packages.cs).
//
// Exterior layout follows the gate convention (GatePorts): in the package's local
// frame +Z is the front, +X the right. The block is Width x 1 x 1 cells; input k
// sits on the back face of cell k and output k on the front face of cell k.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum PackagedPartKind
{
    Wire,
    Source,
    Gate,
    Led,
    Package
}

[Serializable]
public class PackagedPart
{
    public PackagedPartKind kind;
    /// <summary>Anchor cell relative to the minimum corner of the packaged box.</summary>
    public Vector3Int cell;
    public Quaternion rotation = Quaternion.identity;
    public int wireMask;
    public GateType gateType;
    public int sourceState;
    public string packageId;
}

[Serializable]
public class PackagePort
{
    public string name;
    public bool isInput;
    /// <summary>The captured cell this port attaches to (a wire, or a component's pin cell).</summary>
    public Vector3Int insideCell;
    /// <summary>Unit direction from insideCell across the box boundary.</summary>
    public Vector3Int outward;
}

[Serializable]
public class PackageDefinition
{
    public const int MaxNameLength = 32;
    public const int MaxMarkingLength = 12;

    public int formatVersion = 1;
    public string id;
    public string name;
    /// <summary>Short label printed on top of every placed copy.</summary>
    public string marking;
    /// <summary>Size of the captured box, in cells.</summary>
    public Vector3Int size;
    public List<PackagedPart> parts = new List<PackagedPart>();
    public List<PackagePort> ports = new List<PackagePort>();

    public IEnumerable<PackagePort> Inputs => ports.Where(p => p.isInput);
    public IEnumerable<PackagePort> Outputs => ports.Where(p => !p.isInput);
    public int InputCount => ports.Count(p => p.isInput);
    public int OutputCount => ports.Count(p => !p.isInput);

    /// <summary>Exterior width in cells: one cell per input on the back and per output on the front.</summary>
    public int Width => Mathf.Max(1, Mathf.Max(InputCount, OutputCount));
    public Vector3Int Footprint => new Vector3Int(Width, 1, 1);
    public int ComponentCount => parts.Count(p => p.kind != PackagedPartKind.Wire);

    /// <summary>Port i's cell offset and outward face in the package's local frame.</summary>
    public (Vector3Int cellOffset, Vector3Int face) LocalPort(int index)
    {
        int slot = 0;
        bool isInput = ports[index].isInput;
        for (int i = 0; i < index; i++)
            if (ports[i].isInput == isInput) slot++;
        return (new Vector3Int(slot, 0, 0), isInput ? Vector3Int.back : Vector3Int.forward);
    }

    /// <summary>Port i's grid cell and face for a package anchored at anchor with rotation.</summary>
    public (Vector3Int cell, Vector3Int face) WorldPort(int index, Vector3Int anchor, Quaternion rotation)
    {
        var (offset, face) = LocalPort(index);
        return (anchor + Vector3Int.RoundToInt(rotation * (Vector3)offset),
            Vector3Int.RoundToInt(rotation * (Vector3)face));
    }

    /// <summary>Suggests a short top marking from a full name.</summary>
    public static string SuggestMarking(string fullName)
    {
        string value = string.IsNullOrWhiteSpace(fullName) ? "PKG" : fullName.Trim();
        if (value.Length <= MaxMarkingLength) return value.ToUpperInvariant();
        var words = value.Split(new[] { ' ', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
        string initials = string.Concat(words.Select(w => char.ToUpperInvariant(w[0])));
        return initials.Length >= 2 && initials.Length <= MaxMarkingLength
            ? initials : value.Substring(0, MaxMarkingLength).ToUpperInvariant();
    }

    public PackageDefinition Clone() => JsonUtility.FromJson<PackageDefinition>(JsonUtility.ToJson(this));
}
