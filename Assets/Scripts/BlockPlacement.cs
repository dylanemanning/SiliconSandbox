using UnityEngine;

public static class BlockPlacement
{
    public static bool TryGetAnchor(Block prefab, Vector3 hitPoint, Vector3 hitNormal,
        Quaternion rotation, out Vector3Int anchor)
    {
        var normal = Vector3Int.RoundToInt(hitNormal);
        anchor = Vector3Int.RoundToInt(hitPoint + (Vector3)normal * .501f);
        // The face touched by the hit must be the near face of the new footprint.
        int nearest = int.MaxValue;
        foreach (var offset in prefab.OccupiedCells(Vector3Int.zero, rotation))
            nearest = Mathf.Min(nearest, offset.x*normal.x + offset.y*normal.y + offset.z*normal.z);
        anchor -= normal * nearest;
        Physics.SyncTransforms();
        foreach (var cell in prefab.OccupiedCells(anchor, rotation))
            if (Physics.CheckBox(cell, Vector3.one*.48f, Quaternion.identity,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return false;
        return true;
    }
}
