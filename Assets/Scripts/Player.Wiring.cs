using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class Player
{
    private enum WireGesture { None, Provisional, Searching, Manual }
    private WireGesture wireGesture;
    private WireEndpoint wireStart, wireEnd;
    private WireWorldSnapshot wireSnapshot;
    private CancellationTokenSource wireCancellation;
    private Task<WireRouteResult> wireSearch;
    private Block gesturePrefab;
    private WireMouseChord manualBinding;
    private Vector3Int manualAxis;
    private Vector3 manualOrigin;
    private int manualSteps;
    private LineRenderer wireGuide;
    private Material guideMaterial;
    private TMP_Text wireStatus;
    private float statusUntil;
    private bool suppressBreakUntilReleased;
    private static int wireCancelFrame = -1;
    public bool HasWireGesture => wireGesture != WireGesture.None;
    public bool IsWireSearchPending => wireGesture == WireGesture.Searching;
    public string WireStatus => wireStatus != null ? wireStatus.text : string.Empty;

    /// <summary>Escape cancels a wire gesture before the pause menu consumes it, regardless of script order.</summary>
    public static bool ConsumeWireCancelThisFrame()
    {
        if (wireCancelFrame == Time.frameCount) return true;
        if (!KeybindSettings.WasPressedThisFrame(Key.Escape)) return false;
        bool consumed = false;
        foreach (var player in FindObjectsByType<Player>(FindObjectsSortMode.None))
        {
            if (!player.HasWireGesture) continue;
            player.CancelWireRoute(); consumed = true;
        }
        if (consumed) wireCancelFrame = Time.frameCount;
        return consumed;
    }

    private void OnApplicationFocus(bool focus) { if (!focus) CancelWireRoute(); }

    private void OnDestroy()
    {
        CancelWireRoute();
        if (wireGuide != null) Destroy(wireGuide.gameObject);
        if (guideMaterial != null) Destroy(guideMaterial);
        if (wireStatus != null) Destroy(wireStatus.transform.parent.gameObject);
    }

    private bool ProcessWireInput(out bool consumeBreak)
    {
        consumeBreak = false;
        if (HasWireGesture && (KeybindSettings.WasPressedThisFrame(Key.E) ||
            KeybindSettings.WasPressedThisFrame(KeybindSettings.Pause) || HotbarSelectionPressed()))
        { CancelWireRoute(); consumeBreak = true; return true; }
        if (HasWireGesture && (!IsWireSelected() || hotbar.SelectedBlock != gesturePrefab)) CancelWireRoute();
        bool wasSearching = IsWireSearchPending;
        if (KeybindSettings.DeleteWireRunBinding.WasPressedThisFrame())
        {
            CancelWireRoute(); consumeBreak = true; suppressBreakUntilReleased = true;
            var wire = targetBlock != null ? targetBlock.GetComponent<LogicWireBlock>() : null;
            int removed = wire != null ? WireTopology.DeleteRun(wire) : 0;
            SetWireStatus(removed > 0 ? $"Deleted {removed} wire blocks." : "Target a wire run away from its junction.", removed == 0);
            return true;
        }
        if (IsWireSelected() && KeybindSettings.ManualWireBinding.WasPressedThisFrame())
        {
            CancelWireRoute(); consumeBreak = true;
            BeginManualWire(hotbar.SelectedBlock);
            return true;
        }
        if (wireGesture == WireGesture.Manual)
        {
            consumeBreak = true;
            UpdateManualWire();
            if (!manualBinding.PrimaryIsPressed()) FinishManualWire();
            return true;
        }
        if (HasWireGesture && KeybindSettings.WasMouseButtonPressedThisFrame(KeybindSettings.BreakMouseButton))
        {
            CancelWireRoute(); suppressBreakUntilReleased = true; consumeBreak = true; return true;
        }
        PollWireSearch();
        consumeBreak = HasWireGesture;
        return wasSearching || wireGesture == WireGesture.Searching;
    }

    private static bool HotbarSelectionPressed()
    {
        if (Mouse.current != null && Mouse.current.scroll.ReadValue().y != 0) return true;
        for (int i = 0; i < 9; i++)
            if (KeybindSettings.WasPressedThisFrame((Key)((int)Key.Digit1 + i))) return true;
        return false;
    }

    private bool TrySelectWireEndpoint(out WireEndpoint endpoint, out Vector3Int axis)
    {
        endpoint = default; axis = Vector3Int.RoundToInt(SnapToCardinalAxis(targetRaycastHit.normal));
        if (targetBlock == null) return false;
        if (targetBlock.TryGetComponent<LogicWireBlock>(out var wire))
        { endpoint = new WireEndpoint(wire.GridPosition, wire); return true; }
        LogicSignalBlock pin = null;
        Vector3Int pinCell = default, face = default;
        bool input = false;
        if (targetBlock.TryGetComponent<GateBlock>(out var gate))
        {
            float bestDistance = float.PositiveInfinity;
            foreach (var port in gate.WorldPorts())
            {
                if (port.face != axis) continue;
                float distance = ((Vector3)port.cell + (Vector3)port.face * .5f - targetRaycastHit.point).sqrMagnitude;
                if (distance > .35f * .35f || distance >= bestDistance) continue;
                bestDistance = distance; pinCell = port.cell; face = port.face; input = port.port.Role == PortRole.Input;
            }
            if (bestDistance < float.PositiveInfinity) pin = gate;
        }
        else if (targetBlock.TryGetComponent<LogicSignalSource>(out var source))
        {
            pinCell = source.GridPosition;
            var forward = Vector3Int.RoundToInt(source.transform.forward);
            face = Vector3.Dot(targetRaycastHit.point - source.transform.position, forward) >= 0 ? forward : -forward;
            if (face == axis && ((Vector3)pinCell + (Vector3)face * .5f - targetRaycastHit.point).sqrMagnitude <= .35f * .35f)
                pin = source;
        }
        else if (targetBlock.TryGetComponent<StateViewerBlock>(out var viewer))
        {
            pinCell = viewer.GridPosition; face = axis; input = true;
            if (((Vector3)pinCell + (Vector3)face * .5f - targetRaycastHit.point).sqrMagnitude <= .35f * .35f) pin = viewer;
        }
        if (pin != null)
        {
            axis = face;
            var existing = LogicSignalBlock.At(pinCell + face) as LogicWireBlock;
            // An occupied pin branches from precisely its attached cell; unrelated nearby wires stay blocked.
            if (existing != null && !existing.HasEndpointTowards(pinCell)) return false;
            endpoint = new WireEndpoint(pin, pinCell, face, input, existing);
            return true;
        }
        endpoint = new WireEndpoint(LogicWirePathfinder.GetCellBeyondSurfaceHit(targetRaycastHit.point, axis));
        return true;
    }

    private void HandleAutomaticWireClick(Block prefab)
    {
        if (!TrySelectWireEndpoint(out var endpoint, out var axis))
        { SetWireStatus("Choose a pin, wire, or visible surface.", true); return; }
        if (wireGesture == WireGesture.None)
        {
            if (!WireWorldSnapshot.TryCapture(endpoint, endpoint, out wireSnapshot, out string error))
            { SetWireStatus(error, true); return; }
            wireStart = endpoint; gesturePrefab = prefab; wireGesture = WireGesture.Provisional;
            DrawStartSelection(endpoint, axis);
            SetWireStatus("Wire start selected. Choose the other end; Escape cancels.");
            return;
        }
        if (wireGesture != WireGesture.Provisional) return;
        if (!wireSnapshot.IsCurrent())
        { CancelWireRoute(); SetWireStatus("World changed. Choose the endpoints again.", true); return; }
        wireEnd = endpoint;
        if (!WireWorldSnapshot.TryCapture(wireStart, wireEnd, out wireSnapshot, out string failure))
        { CancelWireRoute(); SetWireStatus(failure, true); return; }
        if (wireStart.Cell == wireEnd.Cell && !wireStart.IsPin && !wireEnd.IsPin)
        { CancelWireRoute(); SetWireStatus("Choose a different wire endpoint.", true); return; }
        wireGesture = WireGesture.Searching;
        if (wireGuide != null) wireGuide.enabled = false;
        wireCancellation = new CancellationTokenSource();
        // Capture value data now. No Transform, Physics, registry or other Unity object access on this worker.
        var request = wireSnapshot.Request;
        var token = wireCancellation.Token;
        wireSearch = Task.Run(() => WireRouteSolver.Find(request, token));
        SetWireStatus("Finding wire route… Escape cancels.");
    }

    private void PollWireSearch()
    {
        if (wireGesture != WireGesture.Searching || wireSearch == null || !wireSearch.IsCompleted) return;
        if (wireSearch.IsFaulted)
        {
            Debug.LogException(wireSearch.Exception.GetBaseException());
            CancelWireRoute(); SetWireStatus("Wire search failed. No wire was placed.", true); return;
        }
        var result = wireSearch.Result;
        string message = null;
        bool success = false;
        if (result.Status == WireRouteStatus.Success)
            success = WireRouteCommit.TryCommit(wireSnapshot, result.Path, gesturePrefab, wireStart, wireEnd, out message);
        else if (result.Status == WireRouteStatus.LimitExceeded) message = "Search limit reached. Choose closer endpoints.";
        else if (result.Status != WireRouteStatus.Cancelled) message = "No route found within the search area.";
        CancelWireRoute();
        if (success) SetWireStatus("Wire connected.");
        else if (message != null) SetWireStatus(message, true);
    }

    private void BeginManualWire(Block prefab)
    {
        if (!TrySelectWireEndpoint(out wireStart, out manualAxis))
        { SetWireStatus("Choose a pin, wire, or visible surface.", true); return; }
        if (!WireWorldSnapshot.TryCapture(wireStart, wireStart, out wireSnapshot, out string error))
        { SetWireStatus(error, true); return; }
        gesturePrefab = prefab; manualBinding = KeybindSettings.ManualWireBinding;
        if (wireStart.IsPin) manualAxis = wireStart.Outward;
        if (wireStart.Wire != null && (wireStart.Wire.JoinMask & LogicWireBlock.GetDirectionBit(manualAxis)) != 0)
        { CancelWireRoute(); SetWireStatus("A wire already leaves that face.", true); return; }
        manualOrigin = wireStart.IsPin ? (Vector3)wireStart.PinCell + (Vector3)wireStart.Outward * .5f
            : wireStart.Wire != null ? (Vector3)wireStart.Cell : (Vector3)wireStart.Cell - (Vector3)manualAxis * .5f;
        manualSteps = 1;
        wireGesture = WireGesture.Manual;
        DrawManualGuide();
        SetWireStatus("1 block. Drag a straight row; release to place.");
    }

    private void UpdateManualWire()
    {
        // The clicked face fixes the signed axis for the entire gesture. A miss keeps the last length.
        if (targetBlock == null) return;
        float projectedLength = Vector3.Dot(targetRaycastHit.point - manualOrigin, manualAxis);
        manualSteps = Mathf.Clamp(Mathf.FloorToInt(projectedLength + .5f), 1, 100);
        DrawManualGuide();
        SetWireStatus($"{Mathf.Abs(manualSteps)} blocks. Release to place; Escape cancels.");
    }

    private List<Vector3Int> BuildManualPath(out WireEndpoint end)
    {
        int count = Mathf.Abs(manualSteps);
        var direction = manualAxis;
        var path = new List<Vector3Int> { wireStart.Cell };
        int extra = wireStart.Wire != null ? count : count - 1;
        for (int i = 1; i <= extra; i++) path.Add(wireStart.Cell + direction * i);
        Vector3Int last = path[path.Count - 1];
        // Ending the authored row exactly on this cell deliberately joins it; intermediate wires remain obstacles.
        var aimed = LogicSignalBlock.At(last) as LogicWireBlock;
        if (aimed != null && aimed != wireStart.Wire && aimed.GridPosition == last)
            end = new WireEndpoint(last, aimed);
        else end = new WireEndpoint(last);
        // A coaxial terminal exactly at the row's end is a deliberate manual attachment.
        var pin = LogicSignalBlock.At(last + direction);
        if (aimed == null && pin != null && !(pin is LogicWireBlock) && pin.HasConnection(last + direction, -direction))
            end = new WireEndpoint(pin, last + direction, -direction, !pin.CanDrive(last + direction, -direction));
        return path;
    }

    private void FinishManualWire()
    {
        if (!wireSnapshot.IsCurrent())
        { CancelWireRoute(); SetWireStatus("World changed. Start the wire row again.", true); return; }
        var path = BuildManualPath(out var end);
        string error;
        bool success = WireWorldSnapshot.TryCapture(wireStart, end, out var snapshot, out error) &&
            WireRouteCommit.TryCommit(snapshot, path, gesturePrefab, wireStart, end, out error, manualAxis);
        CancelWireRoute();
        SetWireStatus(success ? "Wire row placed." : error, !success);
    }

    private void CancelWireRoute()
    {
        bool wasActive = HasWireGesture;
        wireCancellation?.Cancel(); wireCancellation?.Dispose(); wireCancellation = null;
        // Observe a late worker fault without letting it publish after cancellation or scene exit.
        if (wireSearch != null && !wireSearch.IsCompleted)
            _ = wireSearch.ContinueWith(task => { var ignored = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
        wireSearch = null; wireSnapshot = null; gesturePrefab = null; wireGesture = WireGesture.None;
        if (wireGuide != null) wireGuide.enabled = false;
        if (wasActive && wireStatus != null) wireStatus.text = string.Empty;
    }

    private void EnsureWireGuide()
    {
        if (wireGuide != null) return;
        wireGuide = new GameObject("Wire selection guide").AddComponent<LineRenderer>();
        wireGuide.useWorldSpace = true; wireGuide.startWidth = wireGuide.endWidth = .035f;
        wireGuide.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wireGuide.receiveShadows = false;
        var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
        guideMaterial = wirePreviewMaterial != null ? new Material(wirePreviewMaterial) : new Material(shader);
        guideMaterial.color = new Color(1f, .78f, .15f);
        wireGuide.sharedMaterial = guideMaterial;
        wireGuide.startColor = wireGuide.endColor = new Color(1f, .78f, .15f);
    }

    private void DrawStartSelection(WireEndpoint endpoint, Vector3Int axis)
    {
        EnsureWireGuide();
        Vector3 center = endpoint.IsPin ? (Vector3)endpoint.PinCell + (Vector3)endpoint.Outward * .53f
            : endpoint.Wire != null ? (Vector3)endpoint.Cell : (Vector3)endpoint.Cell - (Vector3)axis * .47f;
        Vector3 normal = axis;
        Vector3 u = Vector3.Cross(normal, Vector3.up);
        if (u.sqrMagnitude < .1f) u = Vector3.right;
        u.Normalize(); Vector3 v = Vector3.Cross(normal, u);
        const float size = .35f;
        wireGuide.positionCount = 5;
        wireGuide.SetPositions(new[] {center + (u + v) * size, center + (-u + v) * size,
            center + (-u - v) * size, center + (u - v) * size, center + (u + v) * size});
        wireGuide.enabled = true;
    }

    private void DrawManualGuide()
    {
        EnsureWireGuide();
        var path = BuildManualPath(out var end);
        var direction = manualAxis;
        wireGuide.positionCount = 2;
        wireGuide.SetPosition(0, (Vector3)path[0] - (wireStart.Wire != null ? Vector3.zero : (Vector3)direction * .5f));
        wireGuide.SetPosition(1, (Vector3)path[path.Count - 1] + (end.Wire != null ? Vector3.zero : (Vector3)direction * .5f));
        wireGuide.enabled = true;
    }

    private void SetWireStatus(string text, bool error = false)
    {
        if (wireStatus == null)
        {
            var root = new GameObject("Wire status", typeof(Canvas), typeof(CanvasScaler));
            var canvas = root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            var label = new GameObject("Message", typeof(RectTransform), typeof(TextMeshProUGUI));
            label.transform.SetParent(root.transform, false);
            wireStatus = label.GetComponent<TMP_Text>(); wireStatus.fontSize = 24; wireStatus.alignment = TextAlignmentOptions.Center;
            wireStatus.raycastTarget = false; wireStatus.textWrappingMode = TextWrappingModes.Normal;
            var rect = wireStatus.rectTransform; rect.anchorMin = rect.anchorMax = new Vector2(.5f, 0);
            rect.pivot = new Vector2(.5f, 0); rect.anchoredPosition = new Vector2(0, 130); rect.sizeDelta = new Vector2(900, 72);
        }
        wireStatus.text = text ?? "Wire placement failed.";
        wireStatus.color = error ? new Color(1f, .35f, .3f) : new Color(1f, .87f, .5f);
        statusUntil = HasWireGesture ? float.PositiveInfinity : Time.unscaledTime + 3f;
    }

    private void UpdateWireFeedback()
    {
        if (wireStatus != null && Time.unscaledTime >= statusUntil) wireStatus.text = string.Empty;
    }
}
