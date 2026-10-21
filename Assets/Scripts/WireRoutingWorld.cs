using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>A deliberately selected cell or component terminal. No network-wide fallback.</summary>
public readonly struct WireEndpoint
{
    public readonly Vector3Int Cell, PinCell, Outward;
    public readonly LogicWireBlock Wire;
    public readonly LogicSignalBlock Pin;
    public readonly bool IsInput;
    public bool IsPin => Pin != null;

    public WireEndpoint(Vector3Int cell, LogicWireBlock wire = null)
    {
        Cell = cell; Wire = wire; Pin = null; PinCell = default; Outward = default; IsInput = false;
    }

    public WireEndpoint(LogicSignalBlock pin, Vector3Int pinCell, Vector3Int outward, bool isInput,
        LogicWireBlock wire = null)
    {
        Pin = pin; PinCell = pinCell; Outward = outward; Cell = pinCell + outward;
        IsInput = isInput; Wire = wire;
    }
}

/// <summary>Main-thread capture. The worker receives only Request's copied grid values.</summary>
public sealed class WireWorldSnapshot
{
    private readonly Dictionary<Block, BlockState> states;
    private readonly HashSet<Vector3Int> occupied;
    public readonly WireRouteRequest Request;
    public const int SearchMargin = 12;
    public const int MaximumSpan = 128;
    public const int MaximumCells = 262144;

    private readonly struct BlockState
    {
        private readonly Vector3 position, scale;
        private readonly Quaternion rotation;
        private readonly Vector3Int footprint, footprintSize;
        private readonly int endpoints, joins;
        private readonly GateType gateType;
        private readonly Bounds bounds;
        private readonly bool hasCollider;
        public BlockState(Block block)
        {
            position = block.transform.position; scale = block.transform.lossyScale;
            rotation = block.transform.rotation; footprint = block.footprint; footprintSize = block.footprintSize;
            var wire = block.GetComponent<LogicWireBlock>();
            endpoints = wire != null ? wire.EndpointMask : 0; joins = wire != null ? wire.JoinMask : 0;
            var gate = block.GetComponent<GateBlock>(); gateType = gate != null ? gate.GateType : GateType.Invalid;
            var collider = block.GetComponent<Collider>();
            hasCollider = collider != null && collider.enabled;
            bounds = hasCollider ? collider.bounds : default;
        }
        public bool Matches(Block block)
        {
            if (block == null || !block.isActiveAndEnabled || position != block.transform.position ||
                scale != block.transform.lossyScale || rotation != block.transform.rotation ||
                footprint != block.footprint || footprintSize != block.footprintSize) return false;
            var gate = block.GetComponent<GateBlock>();
            if (gate != null && gate.GateType != gateType) return false;
            var wire = block.GetComponent<LogicWireBlock>();
            if (wire != null && (endpoints != wire.EndpointMask || joins != wire.JoinMask)) return false;
            var collider = block.GetComponent<Collider>();
            bool currentCollider = collider != null && collider.enabled;
            return currentCollider == hasCollider && (!hasCollider || collider.bounds == bounds);
        }
    }

    private WireWorldSnapshot(Dictionary<Block, BlockState> states, WireRouteRequest request, HashSet<Vector3Int> occupied)
    { this.states = states; Request = request; this.occupied = occupied; }

    public static bool TryCapture(WireEndpoint start, WireEndpoint end, out WireWorldSnapshot snapshot, out string error)
    {
        snapshot = null; error = null;
        var min = Vector3Int.Min(start.Cell, end.Cell) - Vector3Int.one * SearchMargin;
        var max = Vector3Int.Max(start.Cell, end.Cell) + Vector3Int.one * SearchMargin;
        Vector3Int size = max - min + Vector3Int.one;
        if (size.x > MaximumSpan || size.y > MaximumSpan || size.z > MaximumSpan ||
            (long)size.x * size.y * size.z > MaximumCells)
        { error = "Route exceeds search limits. Choose closer endpoints."; return false; }
        Physics.SyncTransforms();
        var states = new Dictionary<Block, BlockState>();
        var blocked = new HashSet<Vector3Int>();
        var pinApproaches = new HashSet<Vector3Int>();
        foreach (var block in UnityEngine.Object.FindObjectsByType<Block>(FindObjectsSortMode.None))
        {
            if (!block.isActiveAndEnabled) continue;
            states.Add(block, new BlockState(block));
            // Native logic footprints are anchor-based and rotate with the component.
            if (block.GetComponent<LogicSignalBlock>() != null)
            {
                var signal = block.GetComponent<LogicSignalBlock>();
                foreach (var cell in block.OccupiedCells(Vector3Int.RoundToInt(block.transform.position), block.transform.rotation))
                {
                    if (Inside(cell, min, max)) blocked.Add(cell);
                    if (signal is LogicWireBlock) continue;
                    foreach (var adjacent in LogicSignalBlock.Neighbors(cell))
                        if (Inside(adjacent, min, max) && signal.HasConnection(cell, adjacent - cell)) pinApproaches.Add(adjacent);
                }
            }
            else
            {
                // Includes the generated floor (one scaled collider), legacy blocks and construction cubes.
                var collider = block.GetComponent<Collider>();
                if (collider != null && collider.enabled)
                {
                    var bounds = collider.bounds;
                    var low = Vector3Int.Max(min, Vector3Int.CeilToInt(bounds.min - Vector3.one * .49f));
                    var high = Vector3Int.Min(max, Vector3Int.FloorToInt(bounds.max + Vector3.one * .49f));
                    for (int x = low.x; x <= high.x; x++)
                    for (int y = low.y; y <= high.y; y++)
                    for (int z = low.z; z <= high.z; z++) blocked.Add(new Vector3Int(x, y, z));
                }
                else foreach (var cell in block.GetOccupiedCellsAt(block.transform.position))
                    if (Inside(cell, min, max)) blocked.Add(cell);
            }
        }
        var occupied = new HashSet<Vector3Int>(blocked);
        // The group's centered pins reserve one adjacent cell. Only a selected terminal
        // or its explicitly selected wire may use that approach cell.
        blocked.UnionWith(pinApproaches);
        var request = new WireRouteRequest
        {
            Blocked = blocked, Start = start.Cell, Target = end.Cell, Min = min, Max = max,
            StartDirection = start.IsPin && start.Wire == null ? start.Outward : Vector3Int.zero,
            TargetDirection = end.IsPin && end.Wire == null ? -end.Outward : Vector3Int.zero,
            PreferTarget = end.IsInput || !start.IsInput,
            MaxPathLength = 512
        };
        snapshot = new WireWorldSnapshot(states, request, occupied);
        if (!snapshot.EndpointIsValid(start) || !snapshot.EndpointIsValid(end))
        { snapshot = null; error = "An endpoint is occupied or no longer available."; return false; }
        return true;
    }

    public bool IsCurrent()
    {
        var blocks = UnityEngine.Object.FindObjectsByType<Block>(FindObjectsSortMode.None);
        int count = 0;
        foreach (var block in blocks)
        {
            if (!block.isActiveAndEnabled) continue;
            count++;
            if (!states.TryGetValue(block, out var state) || !state.Matches(block)) return false;
        }
        return count == states.Count;
    }

    private bool EndpointIsValid(WireEndpoint endpoint)
    {
        if (endpoint.Wire != null)
        {
            if (!endpoint.Wire.isActiveAndEnabled || endpoint.Wire.GridPosition != endpoint.Cell ||
                !states.ContainsKey(endpoint.Wire.GetComponent<Block>())) return false;
        }
        else if (occupied.Contains(endpoint.Cell) || Request.Blocked.Contains(endpoint.Cell) && !endpoint.IsPin) return false;
        if (endpoint.IsPin && (!endpoint.Pin.isActiveAndEnabled ||
            !endpoint.Pin.HasConnection(endpoint.PinCell, endpoint.Outward))) return false;
        return true;
    }

    public static bool Inside(Vector3Int cell, Vector3Int min, Vector3Int max)
        => cell.x >= min.x && cell.x <= max.x && cell.y >= min.y && cell.y <= max.y && cell.z >= min.z && cell.z <= max.z;
}

public static class WireRouteCommit
{
    /// <summary>Validate the complete edit before publishing any objects or joins.</summary>
    public static bool TryCommit(WireWorldSnapshot snapshot, IReadOnlyList<Vector3Int> path,
        Block prefab, WireEndpoint start, WireEndpoint end, out string error, Vector3Int manualAxis = default)
    {
        error = null;
        if (snapshot == null || !snapshot.IsCurrent())
        { error = "World changed. Choose the endpoints again."; return false; }
        if (prefab == null || prefab.GetComponent<LogicWireBlock>() == null || path == null || path.Count == 0 ||
            path.Count > snapshot.Request.MaxPathLength + 1 || path[0] != start.Cell || path[path.Count - 1] != end.Cell)
        { error = "The wire route is invalid."; return false; }
        var unique = new HashSet<Vector3Int>();
        for (int i = 0; i < path.Count; i++)
        {
            Vector3Int cell = path[i];
            bool selectedEndpoint = i == 0 && (start.Wire != null || start.IsPin) ||
                i == path.Count - 1 && (end.Wire != null || end.IsPin);
            if (!unique.Add(cell) || !WireWorldSnapshot.Inside(cell, snapshot.Request.Min, snapshot.Request.Max) ||
                snapshot.Request.Blocked.Contains(cell) && !selectedEndpoint ||
                i > 0 && LogicWireBlock.GetDirectionBit(cell - path[i - 1]) == 0)
            { error = "The complete wire run is obstructed."; return false; }
        }
        if (path.Count > 1 && manualAxis == Vector3Int.zero &&
            (snapshot.Request.StartDirection != Vector3Int.zero && path[1] - path[0] != snapshot.Request.StartDirection ||
             snapshot.Request.TargetDirection != Vector3Int.zero && path[path.Count - 1] - path[path.Count - 2] != snapshot.Request.TargetDirection))
        { error = "Wire must enter pins along their axis."; return false; }
        if (path.Count == 1 && start.Wire != null && end.Wire == start.Wire && !start.IsPin && !end.IsPin)
        { error = "Choose a different wire endpoint."; return false; }
        var created = new List<GameObject>();
        var wires = new List<LogicWireBlock>();
        var saved = new Dictionary<LogicWireBlock, (int endpoints, int joins)>();
        var staging = new GameObject("Wire route staging");
        staging.SetActive(false);
        try
        {
            for (int i = 0; i < path.Count; i++)
            {
                var wire = i == 0 ? start.Wire : i == path.Count - 1 ? end.Wire : null;
                if (wire == null && path.Count == 1) wire = end.Wire;
                if (wire == null)
                {
                    var instance = UnityEngine.Object.Instantiate(prefab.gameObject, path[i], Quaternion.identity, staging.transform);
                    created.Add(instance); wire = instance.GetComponent<LogicWireBlock>();
                    wire.SetEndpointMask(0); wire.SetJoinMask(0);
                }
                else if (!saved.ContainsKey(wire)) saved.Add(wire, (wire.EndpointMask, wire.JoinMask));
                wires.Add(wire);
                int mask = wire.EndpointMask;
                if (i > 0) mask |= LogicWireBlock.GetDirectionBit(path[i - 1] - path[i]);
                if (i + 1 < path.Count) mask |= LogicWireBlock.GetDirectionBit(path[i + 1] - path[i]);
                if (i == 0 && start.IsPin) mask |= LogicWireBlock.GetDirectionBit(-start.Outward);
                if (i == path.Count - 1 && end.IsPin) mask |= LogicWireBlock.GetDirectionBit(-end.Outward);
                // Manual open ends show the complete one-block piece, including quick clicks.
                if (manualAxis != Vector3Int.zero && !saved.ContainsKey(wire)) mask |= LogicWireBlock.GetDirectionBit(manualAxis) | LogicWireBlock.GetDirectionBit(-manualAxis);
                wire.SetEndpointMask(mask);
                if (i == 0 && start.IsPin) wire.SetJoinMask(wire.JoinMask | LogicWireBlock.GetDirectionBit(-start.Outward));
                if (i == path.Count - 1 && end.IsPin) wire.SetJoinMask(wire.JoinMask | LogicWireBlock.GetDirectionBit(-end.Outward));
            }
            for (int i = 1; i < wires.Count; i++) WireTopology.Join(wires[i - 1], wires[i]);
            foreach (var instance in created)
            {
                instance.transform.SetParent(null, true); instance.SetActive(true);
            }
            WorldSaveSystem.Instance?.MarkDirty();
            return true;
        }
        catch (Exception exception)
        {
            foreach (var pair in saved) if (pair.Key != null)
            { pair.Key.SetEndpointMask(pair.Value.endpoints); pair.Key.SetJoinMask(pair.Value.joins); }
            foreach (var instance in created) if (instance != null)
            { instance.SetActive(false); DestroyObject(instance); }
            Debug.LogException(exception); error = "Wire placement failed. The world was preserved."; return false;
        }
        finally { DestroyObject(staging); }
    }

    private static void DestroyObject(GameObject value)
    {
        if (Application.isPlaying) UnityEngine.Object.Destroy(value);
        else UnityEngine.Object.DestroyImmediate(value);
    }
}
