using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public CameraSettings cameraSettings;
    public float speed = 10f;
    public float jumpForce = 5f;
    public float flightStep = 10f;
    public float reachDistance = 5f;
    public float fallThreshold = -10f;
    public Transform respawnPoint;
    public HotbarManager hotbar;
    public GameObject blockHighlighter; // Visual indicator for targeted block
    public Block[] blockPalette; // Array of different block prefabs (Grass, Wire, Voltage, etc.)
    [SerializeField] private Material wirePreviewMaterial;
    float xRotation;
    float yRotation;
    bool isGrounded;
    float breakSeconds;
    Vector3 spawnPosition;
    Quaternion spawnRotation;
    public float doublePressTime = 0.3f; 
    private float lastSpacePressTime = 0f;
    private bool waitingForSecondSpacePress = false;
    private bool isFlying = false;

    Block targetBlock;
    Block breakingBlock;
    RaycastHit targetRaycastHit;
    Rigidbody rb;
    private LogicWireBlock wireRouteAnchor;
    private readonly List<Vector3Int> wireRoutePath = new List<Vector3Int>();
    private readonly List<GameObject> wirePreviewObjects = new List<GameObject>();
    private readonly List<Material> generatedPreviewMaterials = new List<Material>();
    private Vector3Int wireRouteTargetNeighbor;
    private bool suppressBreakUntilReleased;

    void Start()
    {
        WorldSaveSystem.GetOrCreate(blockPalette, null);
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Crucial for player controllers
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        CancelWireRoute();
    }

    void Update()
    {
        // The inventory can close before this Update runs; block gameplay for the
        // rest of that frame so the closing input cannot also affect the world.
        if (PauseManager.IsPaused || InventoryGrid.IsOpen || InventoryGrid.ClosedThisFrame) return;

        CheckFall();
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckTargetBlock();

        bool routeWasActive = wireRouteAnchor != null;
        if (routeWasActive && !IsWireSelected())
        {
            CancelWireRoute();
        }

        if (wireRouteAnchor != null)
        {
            UpdateWireRoutePreview();
        }

        bool breakHeld = KeybindSettings.IsMouseButtonPressed(KeybindSettings.BreakMouseButton);
        bool cancelRoutePressed = routeWasActive &&
            KeybindSettings.WasMouseButtonPressedThisFrame(KeybindSettings.BreakMouseButton);
        if (cancelRoutePressed)
        {
            CancelWireRoute();
            suppressBreakUntilReleased = true;
        }

        if (routeWasActive)
        {
            breakSeconds = 0; 
            breakingBlock = null;
        }
        else if (suppressBreakUntilReleased)
        {
            if (!breakHeld) suppressBreakUntilReleased = false;
            breakSeconds = 0;
            breakingBlock = null;
        }
        else if (breakHeld)
        {
            TryBreakBlock();
        }
        else
        {
            breakSeconds = 0;
            breakingBlock = null;
        }

        if (!cancelRoutePressed && KeybindSettings.WasMouseButtonPressedThisFrame(KeybindSettings.PlaceMouseButton))
        {
            TryPlaceBlock();
        }

        if (KeybindSettings.WasPressedThisFrame(KeybindSettings.Jump))
        {
            if (waitingForSecondSpacePress && Time.time - lastSpacePressTime <= doublePressTime)
            {
                ToggleFlight();
                waitingForSecondSpacePress = false;
            }
            else
            {
                lastSpacePressTime = Time.time;
                waitingForSecondSpacePress = true;
            }
        }
    }

    void CheckFall()
    {
        if (transform.position.y >= fallThreshold) return;

        Vector3 safePosition = respawnPoint ? respawnPoint.position : spawnPosition;
        Quaternion safeRotation = respawnPoint ? respawnPoint.rotation : spawnRotation;

        rb.position = safePosition;
        rb.rotation = safeRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isGrounded = false;
        targetBlock = null;
        breakingBlock = null;
        breakSeconds = 0;
    }

    void CheckRotation()
    {
        if (cameraSettings.camera == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseDelta = mouse.delta.ReadValue();
        float mouseX = mouseDelta.x * Time.deltaTime * cameraSettings.sensitivityX;
        float mouseY = mouseDelta.y * Time.deltaTime * cameraSettings.sensitivityY;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraSettings.camera.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    void CheckMovement()
    {
        float moveX = 0;
        float moveZ = 0;

        if (KeybindSettings.IsPressed(KeybindSettings.MoveForward)) moveZ = 1;
        if (KeybindSettings.IsPressed(KeybindSettings.MoveBackward)) moveZ = -1;
        if (KeybindSettings.IsPressed(KeybindSettings.MoveLeft)) moveX = -1;
        if (KeybindSettings.IsPressed(KeybindSettings.MoveRight)) moveX = 1;

        Vector3 move = (transform.forward * moveZ + transform.right * moveX).normalized * speed;
        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);
    }

    void CheckJump()
    {
        if(!isFlying) {
            if (isGrounded && KeybindSettings.WasPressedThisFrame(KeybindSettings.Jump))
            {
                rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
        } else
        {
                float verticalInput = (KeybindSettings.IsPressed(KeybindSettings.Jump) ? 1f : 0f)
                    - (KeybindSettings.IsPressed(KeybindSettings.FlyDown) ? 1f : 0f);
                MoveWhileFlying(verticalInput);
        }
    }

        void MoveWhileFlying(float verticalInput)
    {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, verticalInput * flightStep, rb.linearVelocity.z);
    }

    void CheckTargetBlock() 
    {
        if (cameraSettings.camera == null)
        {
            targetBlock = null;

            if (blockHighlighter != null)
                blockHighlighter.SetActive(false);

            return;
        }
        // Shoot ray from center of camera
        Ray ray = cameraSettings.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Check if the ray hits a block within reach distance
        if (Physics.Raycast(ray, out targetRaycastHit, reachDistance)) 
        {
            targetBlock = targetRaycastHit.collider.GetComponentInParent<Block>(); // Try to get the Block component from the hit object

            if (targetBlock != null) // If we hit a block, show the highlighter at the block's position
            {
                if (blockHighlighter != null)
                {
                    blockHighlighter.SetActive(true);
                    var collider = targetBlock.GetComponent<Collider>();
                    blockHighlighter.transform.position = collider.bounds.center;
                    blockHighlighter.transform.rotation = targetBlock.transform.rotation;
                    blockHighlighter.transform.localScale = (Vector3)targetBlock.footprint * 1.02f;
                }
            }
            else if (blockHighlighter != null) blockHighlighter.SetActive(false);
        }
        else // If we don't hit anything, clear the target block and hide the highlighter
        {
            targetBlock = null;
            if (blockHighlighter != null)
                blockHighlighter.SetActive(false);
        }
    }

    void TryBreakBlock() 
    {
        if (!targetBlock || !targetBlock.breakable) { breakSeconds = 0; return; }
        if (breakingBlock != targetBlock) { breakSeconds = 0; }

        breakingBlock = targetBlock;
        breakSeconds += Time.deltaTime;

        if (targetBlock.TryBreak(breakSeconds)) 
        { 
            breakSeconds = 0; 
            targetBlock = null;
        }
    }

    void TryPlaceBlock() 
    {
        Block selectedBlock = hotbar ? hotbar.SelectedBlock : null;
        if (targetBlock == null || selectedBlock == null) return;

        Block prefabToPlace = selectedBlock;
        if (prefabToPlace == null) return;

        if (prefabToPlace.GetComponent<LogicWireBlock>() != null)
        {
            if (wireRouteAnchor != null)
            {
                CommitWireRoute(prefabToPlace);
            }
            else
            {
                BeginWireRoute(prefabToPlace);
            }

            return;
        }

        // Round to Int ensures the grid is perfect (1.0, 2.0, etc.)
        Vector3 spawnPosition = targetBlock.transform.position + targetRaycastHit.normal;
        if (!targetBlock.breakable)
        {
            float gridSize = Mathf.Max(targetBlock.placementGridSize, 0.001f);
            spawnPosition.x = Mathf.Round(targetRaycastHit.point.x / gridSize) * gridSize;
            spawnPosition.z = Mathf.Round(targetRaycastHit.point.z / gridSize) * gridSize;
            spawnPosition.y = targetBlock.transform.position.y + targetRaycastHit.normal.y;
        }
        else
        {
            float gridSize = Mathf.Max(targetBlock.placementGridSize, 0.001f);
            spawnPosition.x = Mathf.Round(spawnPosition.x / gridSize) * gridSize;
            spawnPosition.y = Mathf.Round(spawnPosition.y / gridSize) * gridSize;
            spawnPosition.z = Mathf.Round(spawnPosition.z / gridSize) * gridSize;
        }

        Block prototypeBlock = prefabToPlace.GetComponent<Block>();
        Vector3Int footprint = prototypeBlock != null ? prototypeBlock.GetFootprintSize() : Vector3Int.one;
        if (footprint != Vector3Int.one)
        {
            Vector3 halfExtent = new Vector3(
                (footprint.x - 1) * 0.5f,
                (footprint.y - 1) * 0.5f,
                (footprint.z - 1) * 0.5f);
            spawnPosition = new Vector3(
                Mathf.Round(spawnPosition.x / 1f) * 1f,
                Mathf.Round(spawnPosition.y / 1f) * 1f,
                Mathf.Round(spawnPosition.z / 1f) * 1f);
            spawnPosition += Vector3.Scale(targetRaycastHit.normal, halfExtent);
        }

        if (!Block.CanPlaceAt(spawnPosition, prefabToPlace.GetComponent<Block>()))
        {
            return;
        }
        
        float playerYaw = transform.eulerAngles.y;
        Quaternion spawnRotation = Quaternion.identity;

        // Wires must always sit on a cardinal axis and remain centered inside the
        // target grid cell. Use the dominant face normal, snapped to a single axis,
        // so the wire is straight and the model never drifts off-grid.
        if (prefabToPlace.GetComponent<LogicWireBlock>() != null)
        {
            Vector3 placementAxis = SnapToCardinalAxis(targetRaycastHit.normal);
            spawnPosition = targetBlock.transform.position + placementAxis;
            spawnRotation = Quaternion.FromToRotation(Vector3.up, placementAxis);
        }
        // Gates face the way the player is looking: output on the far side,
        // inputs left and right. Yaw is snapped to 90 degrees so the gate's
        // ports (GateBlock.WorldPorts) point along whole grid directions.
        else if (prefabToPlace.GetComponent<GateBlock>() != null)
        {
            spawnRotation = Quaternion.Euler(0, Mathf.Round(playerYaw / 90f) * 90f, 0);
        }

        GameObject placedBlockObject = Instantiate(prefabToPlace.gameObject, spawnPosition, spawnRotation);
        Block placedBlock = placedBlockObject.GetComponent<Block>();
        if (placedBlock != null)
        {
            Vector3 currentScale = placedBlockObject.transform.localScale;
            placedBlockObject.transform.localScale = new Vector3(
                currentScale.x * Mathf.Max(1, placedBlock.GetFootprintSize().x),
                currentScale.y * Mathf.Max(1, placedBlock.GetFootprintSize().y),
                currentScale.z * Mathf.Max(1, placedBlock.GetFootprintSize().z));
        }
        WorldSaveSystem.Instance?.MarkDirty();
    }

    private bool IsWireSelected()
    {
        return hotbar != null && hotbar.SelectedBlock != null &&
               hotbar.SelectedBlock.GetComponent<LogicWireBlock>() != null;
    }

    private bool TryGetWireTarget(
        Block wirePrefab,
        out Vector3Int targetCell,
        out Vector3Int targetNeighbor,
        out Quaternion rotation)
    {
        targetCell = default;
        targetNeighbor = default;
        rotation = Quaternion.identity;
        if (targetBlock == null || wirePrefab == null) return false;

        Vector3Int facing = Vector3Int.RoundToInt(SnapToCardinalAxis(-transform.forward));
        if (targetBlock.TryGetComponent<GateBlock>(out GateBlock gate) &&
            gate.TryGetFacingPort(facing, targetRaycastHit.point, out targetNeighbor, out Vector3Int gateFace))
        {
            targetCell = targetNeighbor + gateFace;
            rotation = Quaternion.FromToRotation(Vector3.up, (Vector3)gateFace);
            return true;
        }

        if (targetBlock.TryGetComponent<LogicSignalSource>(out LogicSignalSource source))
        {
            Vector3Int sourceForward = Vector3Int.RoundToInt(source.transform.forward);
            Vector3Int sourceFace = Vector3.Dot(sourceForward, facing) >= 0f ? sourceForward : -sourceForward;
            targetNeighbor = source.GridPosition;
            targetCell = targetNeighbor + sourceFace;
            rotation = Quaternion.FromToRotation(Vector3.up, (Vector3)sourceFace);
            return true;
        }

        Vector3 axis = SnapToCardinalAxis(targetRaycastHit.normal);
        targetCell = LogicWirePathfinder.GetCellBeyondSurfaceHit(targetRaycastHit.point, axis);
        targetNeighbor = targetCell - Vector3Int.RoundToInt(axis);
        rotation = Quaternion.FromToRotation(Vector3.up, axis);
        return true;
    }

    private void BeginWireRoute(Block wirePrefab)
    {
        if (!TryGetWireTarget(wirePrefab, out Vector3Int targetCell, out Vector3Int targetNeighbor, out Quaternion rotation)) return;
        Vector3 position = targetCell;
        if (!Block.CanPlaceAt(position, wirePrefab.GetComponent<Block>())) return;

        GameObject placedObject = Instantiate(wirePrefab.gameObject, position, rotation);
        Block placedBlock = placedObject.GetComponent<Block>();
        if (placedBlock != null)
        {
            Vector3Int footprint = placedBlock.GetFootprintSize();
            placedObject.transform.localScale = Vector3.Scale(
                placedObject.transform.localScale,
                new Vector3(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y), Mathf.Max(1, footprint.z)));
        }

        wireRouteAnchor = placedObject.GetComponent<LogicWireBlock>();
        if (wireRouteAnchor != null)
        {
            wireRouteAnchor.SetEndpointMask(LogicWireBlock.GetDirectionBit(targetNeighbor - targetCell));
        }
        wireRoutePath.Clear();
        WorldSaveSystem.Instance?.MarkDirty();
    }

    private void UpdateWireRoutePreview()
    {
        Block wirePrefab = hotbar != null ? hotbar.SelectedBlock : null;
        if (!TryGetWireTarget(wirePrefab, out Vector3Int targetCell, out Vector3Int targetNeighbor, out _))
        {
            wireRoutePath.Clear();
            ClearWirePreview();
            return;
        }

        Block anchorBlock = wireRouteAnchor.GetComponent<Block>();
        Vector3Int startCell = wireRouteAnchor.GridPosition;
        if (!LogicWirePathfinder.TryFindPath(
                startCell,
                targetCell,
                cell => cell == startCell || !Block.IsCellOccupied(cell, anchorBlock),
                out List<Vector3Int> path))
        {
            wireRoutePath.Clear();
            ClearWirePreview();
            return;
        }

        bool pathChanged = !PathsMatch(wireRoutePath, path) || wireRouteTargetNeighbor != targetNeighbor;
        if (!pathChanged) return;

        wireRoutePath.Clear();
        wireRoutePath.AddRange(path);
        wireRouteTargetNeighbor = targetNeighbor;
        RebuildWirePreview(wirePrefab, path, targetNeighbor);
    }

    private void RebuildWirePreview(Block wirePrefab, List<Vector3Int> path, Vector3Int targetNeighbor)
    {
        ClearWirePreview();
        if (path.Count < 2) return;

        for (int index = 1; index < path.Count; index++)
        {
            int endpointMask = GetRouteEndpointMask(path, index, targetNeighbor);
            Vector3Int previous = path[index - 1];
            Vector3Int next = index < path.Count - 1 ? path[index + 1] : targetNeighbor;
            Vector3Int primaryDirection = index < path.Count - 1 ? next - path[index] : previous - path[index];
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, (Vector3)primaryDirection);
            GameObject preview = Instantiate(wirePrefab.gameObject, path[index], rotation);
            LogicWireBlock previewWire = preview.GetComponent<LogicWireBlock>();
            if (previewWire != null) previewWire.SetEndpointMask(endpointMask);
            WireModelVisual previewVisual = preview.GetComponent<WireModelVisual>();
            if (previewVisual != null) previewVisual.RefreshForConnections(endpointMask);

            foreach (MonoBehaviour behaviour in preview.GetComponentsInChildren<MonoBehaviour>(true))
            {
                behaviour.enabled = false;
            }

            foreach (Collider previewCollider in preview.GetComponentsInChildren<Collider>(true))
            {
                previewCollider.enabled = false;
            }

            ApplyWirePreviewMaterial(preview);
            wirePreviewObjects.Add(preview);

            if (index >= 1)
            {
                GameObject linkObject = new GameObject("Wire Route Preview Link");
                LineRenderer link = linkObject.AddComponent<LineRenderer>();
                link.useWorldSpace = true;
                link.positionCount = 2;
                link.startWidth = 0.18f;
                link.endWidth = 0.18f;
                link.SetPosition(0, path[index - 1]);
                link.SetPosition(1, path[index]);

                Renderer wireRenderer = wirePrefab.GetComponentInChildren<Renderer>();
                if (wireRenderer != null) link.sharedMaterial = wireRenderer.sharedMaterial;
                ApplyWirePreviewMaterial(linkObject);
                wirePreviewObjects.Add(linkObject);
            }
        }
    }

    private static int GetRouteEndpointMask(List<Vector3Int> path, int index, Vector3Int targetNeighbor)
    {
        int mask = LogicWireBlock.GetDirectionBit(path[index - 1] - path[index]);
        if (index < path.Count - 1)
        {
            mask |= LogicWireBlock.GetDirectionBit(path[index + 1] - path[index]);
        }
        else
        {
            mask |= LogicWireBlock.GetDirectionBit(targetNeighbor - path[index]);
        }

        return mask;
    }

    private void CommitWireRoute(Block wirePrefab)
    {
        if (wireRoutePath.Count < 2)
        {
            CancelWireRoute();
            return;
        }

        for (int index = 1; index < wireRoutePath.Count; index++)
        {
            if (!Block.CanPlaceAt(wireRoutePath[index], wirePrefab)) return;
        }

        wireRouteAnchor.SetEndpointMask(
            wireRouteAnchor.EndpointMask |
            LogicWireBlock.GetDirectionBit(wireRoutePath[1] - wireRoutePath[0]));
        for (int index = 1; index < wireRoutePath.Count; index++)
        {
            int endpointMask = GetRouteEndpointMask(wireRoutePath, index, wireRouteTargetNeighbor);
            Vector3Int previous = wireRoutePath[index - 1];
            Vector3Int next = index < wireRoutePath.Count - 1
                ? wireRoutePath[index + 1]
                : wireRouteTargetNeighbor;
            Vector3Int primaryDirection = index < wireRoutePath.Count - 1
                ? next - wireRoutePath[index]
                : previous - wireRoutePath[index];
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, (Vector3)primaryDirection);
            Vector3 position = wireRoutePath[index];
            GameObject placedObject = Instantiate(wirePrefab.gameObject, position, rotation);
            LogicWireBlock placedWire = placedObject.GetComponent<LogicWireBlock>();
            if (placedWire != null)
            {
                placedWire.SetEndpointMask(endpointMask);
            }

            Block placedBlock = placedObject.GetComponent<Block>();
            if (placedBlock != null)
            {
                Vector3Int footprint = placedBlock.GetFootprintSize();
                placedObject.transform.localScale = Vector3.Scale(
                    placedObject.transform.localScale,
                    new Vector3(Mathf.Max(1, footprint.x), Mathf.Max(1, footprint.y), Mathf.Max(1, footprint.z)));
            }
        }

        WorldSaveSystem.Instance?.MarkDirty();
        CancelWireRoute();
    }

    private void CancelWireRoute()
    {
        wireRouteAnchor = null;
        wireRoutePath.Clear();
        ClearWirePreview();
    }

    private void ClearWirePreview()
    {
        foreach (GameObject preview in wirePreviewObjects)
        {
            if (preview != null) Destroy(preview);
        }
        wirePreviewObjects.Clear();

        foreach (Material material in generatedPreviewMaterials)
        {
            if (material != null) Destroy(material);
        }
        generatedPreviewMaterials.Clear();
    }

    private void ApplyWirePreviewMaterial(GameObject preview)
    {
        foreach (Renderer previewRenderer in preview.GetComponentsInChildren<Renderer>(true))
        {
            if (wirePreviewMaterial != null)
            {
                Material[] materials = new Material[previewRenderer.sharedMaterials.Length];
                for (int index = 0; index < materials.Length; index++)
                {
                    Material material = new Material(wirePreviewMaterial);
                    ConfigureTransparentMaterial(material);
                    materials[index] = material;
                    generatedPreviewMaterials.Add(material);
                }
                previewRenderer.sharedMaterials = materials;
                continue;
            }

            Material[] sourceMaterials = previewRenderer.sharedMaterials;
            Material[] transparentMaterials = new Material[sourceMaterials.Length];
            for (int index = 0; index < sourceMaterials.Length; index++)
            {
                if (sourceMaterials[index] == null) continue;
                Material material = new Material(sourceMaterials[index]);
                ConfigureTransparentMaterial(material);
                transparentMaterials[index] = material;
                generatedPreviewMaterials.Add(material);
            }
            previewRenderer.sharedMaterials = transparentMaterials;
        }
    }

    private static void ConfigureTransparentMaterial(Material material)
    {
        if (material.HasProperty("_BaseColor"))
        {
            Color color = material.GetColor("_BaseColor");
            color.a = 0.35f;
            material.SetColor("_BaseColor", color);
        }
        if (material.HasProperty("_Color"))
        {
            Color color = material.GetColor("_Color");
            color.a = 0.35f;
            material.SetColor("_Color", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        if (material.HasProperty("_Mode"))
        {
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
    }

    private static bool PathsMatch(List<Vector3Int> first, List<Vector3Int> second)
    {
        if (first.Count != second.Count) return false;
        for (int index = 0; index < first.Count; index++)
        {
            if (first[index] != second[index]) return false;
        }
        return true;
    }

    private static Vector3 SnapToCardinalAxis(Vector3 direction)
    {
        Vector3 absolute = new Vector3(
            Mathf.Abs(direction.x),
            Mathf.Abs(direction.y),
            Mathf.Abs(direction.z));

        int axis = 0;
        if (absolute.y > absolute.x) axis = 1;
        if (absolute.z > absolute[axis]) axis = 2;

        Vector3 snapped = Vector3.zero;
        snapped[axis] = direction[axis] >= 0f ? 1f : -1f;
        return snapped;
    }

    void ToggleFlight()
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        if(!isFlying)
        {
            Physics.gravity = new Vector3(0f, 0f, 0f);
            isFlying = true;
        } else
        {
            Physics.gravity = new Vector3(0f, -9.81f, 0f);
            isFlying = false;
        }
    }

    private void OnTriggerStay(Collider other) => isGrounded = true;
    private void OnTriggerExit(Collider other) => isGrounded = false;

    [System.Serializable]
    public struct CameraSettings
    {
        public Camera camera;
        public float sensitivityX;
        public float sensitivityY;
    }
}
