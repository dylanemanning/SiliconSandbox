using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class Player : MonoBehaviour
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
    private PackagingWandController packagingWandController;
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
    private bool useGravityBeforeFlight = true;
    private bool spawnSafetyWarningLogged;
    private readonly Collider[] spawnOverlapBuffer = new Collider[32];

    private const int MaxSpawnSearchSteps = 32;
    private const float SpawnSearchStep = 0.5f;

    Block targetBlock;
    Block breakingBlock;
    RaycastHit targetRaycastHit;
    Rigidbody rb;
    void Start()
    {
        WorldSaveSystem.GetOrCreate(blockPalette, null);
        packagingWandController = GetComponent<PackagingWandController>();
        if (packagingWandController == null)
            packagingWandController = gameObject.AddComponent<PackagingWandController>();
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Crucial for player controllers
        useGravityBeforeFlight = rb.useGravity;
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        if (TryFindSafeSpawnPosition(spawnPosition, out Vector3 safePosition))
        {
            rb.position = safePosition;
        }
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDisable()
    {
        CancelWireRoute();
        ResetFlightForDisable();
    }

    void Update()
    {
        UpdateWireFeedback();
        bool cancelled = ConsumeWireCancelThisFrame();
        if (PauseManager.IsPaused || InventoryGrid.IsOpen || InventoryGrid.ClosedThisFrame || PackagingWandController.IsConfirmationOpen)
        {
            waitingForSecondSpacePress = false;
            CancelWireRoute();
            return;
        }

        CheckFall();
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckTargetBlock();

        //bool routeWasActive = wireRouteAnchor != null;
        //if (routeWasActive && !IsWireSelected())
        //{
        //    CancelWireRoute();
        //}

        //if (wireRouteAnchor != null)
        //{
        //    UpdateWireRoutePreview();
        //}

        bool packagingWandSelected = hotbar != null && hotbar.IsPackagingWandSelected;
        packagingWandController.HandleTool(packagingWandSelected, targetBlock, targetRaycastHit);
        if (packagingWandSelected)
        {
            waitingForSecondSpacePress = false;
            if (blockHighlighter != null) blockHighlighter.SetActive(false);
            breakSeconds = 0f;
            breakingBlock = null;

            if (KeybindSettings.WasMouseButtonPressedThisFrame(KeybindSettings.BreakMouseButton))
            {
                packagingWandController.CancelSelection();
            }

            return;
        }

        bool wireConsumed = ProcessWireInput(out bool consumeBreak);
        bool breakHeld = KeybindSettings.IsMouseButtonPressed(KeybindSettings.BreakMouseButton);
        if (cancelled || consumeBreak || suppressBreakUntilReleased)
        {
            if (!breakHeld) suppressBreakUntilReleased = false;
            breakSeconds = 0;
            breakingBlock = null;
        }
        else if (breakHeld) TryBreakBlock();
        else { breakSeconds = 0; breakingBlock = null; }

        bool toggledSource = !cancelled && !wireConsumed && TryToggleSource();
        if (!cancelled && !wireConsumed && !toggledSource &&
            KeybindSettings.WasMouseButtonPressedThisFrame(KeybindSettings.PlaceMouseButton)) TryPlaceBlock();

        if (KeybindSettings.WasPressedThisFrame(KeybindSettings.Jump))
        {
            if (waitingForSecondSpacePress && Time.unscaledTime - lastSpacePressTime <= doublePressTime)
            {
                ToggleFlight();
                waitingForSecondSpacePress = false;
            }
            else
            {
                lastSpacePressTime = Time.unscaledTime;
                waitingForSecondSpacePress = true;
            }
        }
    }

    void CheckFall()
    {
        if (transform.position.y >= fallThreshold) return;

        Vector3 requestedPosition = respawnPoint ? respawnPoint.position : spawnPosition;
        Quaternion safeRotation = respawnPoint ? respawnPoint.rotation : spawnRotation;
        if (!TryFindSafeSpawnPosition(requestedPosition, out Vector3 safePosition))
        {
            if (!spawnSafetyWarningLogged)
            {
                Debug.LogError("No safe player respawn position was found near the spawn point.", this);
                spawnSafetyWarningLogged = true;
            }
            return;
        }

        rb.position = safePosition;
        rb.rotation = safeRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        isGrounded = false;
        targetBlock = null;
        breakingBlock = null;
        breakSeconds = 0;
    }

    private bool TryFindSafeSpawnPosition(Vector3 requestedPosition, out Vector3 safePosition)
    {
        Physics.SyncTransforms();
        for (int shell = 0; shell <= MaxSpawnSearchSteps; shell++)
        {
            float bestDistanceSquared = float.PositiveInfinity;
            Vector3 bestPosition = requestedPosition;
            for (int x = -shell; x <= shell; x++)
            for (int y = -shell; y <= shell; y++)
            for (int z = -shell; z <= shell; z++)
            {
                if (Mathf.Max(Mathf.Abs(x), Mathf.Abs(y), Mathf.Abs(z)) != shell) continue;

                Vector3 candidate = requestedPosition + new Vector3(x, y, z) * SpawnSearchStep;
                float distanceSquared = (candidate - requestedPosition).sqrMagnitude;
                if (distanceSquared >= bestDistanceSquared || !IsSpawnPositionClear(candidate)) continue;

                bestDistanceSquared = distanceSquared;
                bestPosition = candidate;
            }

            float nextShellDistance = (shell + 1) * SpawnSearchStep;
            if (!float.IsPositiveInfinity(bestDistanceSquared) &&
                bestDistanceSquared < nextShellDistance * nextShellDistance)
            {
                safePosition = bestPosition;
                spawnSafetyWarningLogged = false;
                return true;
            }
        }

        safePosition = requestedPosition;
        return false;
    }

    private bool IsSpawnPositionClear(Vector3 candidatePosition)
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        int overlapCount;
        if (capsule != null)
        {
            Vector3 scale = transform.lossyScale;
            int axisIndex = capsule.direction;
            Vector3 axis = axisIndex == 0 ? transform.right
                : axisIndex == 1 ? transform.up
                : transform.forward;
            float axisScale = Mathf.Abs(scale[axisIndex]);
            float radiusScale = axisIndex == 0
                ? Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z))
                : axisIndex == 1
                    ? Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z))
                    : Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));
            float radius = Mathf.Max(0.01f, capsule.radius * radiusScale - 0.02f);
            float height = Mathf.Max(capsule.height * axisScale, radius * 2f);
            Vector3 centerOffset = transform.rotation * Vector3.Scale(capsule.center, scale);
            Vector3 center = candidatePosition + centerOffset;
            float halfSegment = Mathf.Max(0f, height * 0.5f - radius);
            overlapCount = Physics.OverlapCapsuleNonAlloc(
                center + axis * halfSegment,
                center - axis * halfSegment,
                radius,
                spawnOverlapBuffer,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
        }
        else
        {
            Collider playerCollider = GetComponent<Collider>();
            if (playerCollider == null) return true;

            Bounds bounds = playerCollider.bounds;
            Vector3 center = bounds.center + candidatePosition - transform.position;
            Vector3 extents = Vector3.Max(Vector3.one * 0.01f, bounds.extents - Vector3.one * 0.02f);
            overlapCount = Physics.OverlapBoxNonAlloc(
                center,
                extents,
                spawnOverlapBuffer,
                Quaternion.identity,
                Physics.AllLayers,
                QueryTriggerInteraction.Ignore);
        }

        if (overlapCount == spawnOverlapBuffer.Length) return false;

        for (int index = 0; index < overlapCount; index++)
        {
            Collider other = spawnOverlapBuffer[index];
            if (other == null || other.attachedRigidbody == rb ||
                other.transform == transform || other.transform.IsChildOf(transform)) continue;
            return false;
        }

        return true;
    }

    void CheckRotation()
    {
        if (cameraSettings.camera == null) return;

        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseDelta = mouse.delta.ReadValue();
        float sensitivity = AccessibilitySettings.MouseSensitivityMultiplier;
        float mouseX = mouseDelta.x * Time.deltaTime * cameraSettings.sensitivityX * sensitivity;
        float mouseY = mouseDelta.y * Time.deltaTime * cameraSettings.sensitivityY * sensitivity;

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

    private bool TryToggleSource()
    {
        if (!KeybindSettings.WasPressedThisFrame(KeybindSettings.Interact)) return false;
        if (targetBlock == null || !targetBlock.TryGetComponent<LogicSignalSource>(out var source) ||
            source is LogicClockSource) return false;

        // Consume the interaction so a simultaneous placement click cannot also
        // place a block or finish a wire route.
        source.ToggleState();
        WorldSaveSystem.Instance?.MarkDirty();
        return true;
    }

    void TryPlaceBlock() 
    {
        Block selectedBlock = hotbar ? hotbar.SelectedBlock : null;
        if (targetBlock == null || selectedBlock == null) return;

        Block prefabToPlace = selectedBlock;
        if (prefabToPlace == null) return;

        if (prefabToPlace.GetComponent<LogicWireBlock>() != null)
        {
            HandleAutomaticWireClick(prefabToPlace);

            return;
        }

        float playerYaw = transform.eulerAngles.y;
        Quaternion spawnRotation = Quaternion.identity;
        if (prefabToPlace.GetComponent<GateBlock>() != null)
        {
            spawnRotation = Quaternion.Euler(0, Mathf.Round(playerYaw / 90f) * 90f, 0);
        }

        if (!BlockPlacement.TryGetAnchor(
                prefabToPlace,
                targetRaycastHit.point,
                targetRaycastHit.normal,
                spawnRotation,
                out Vector3Int anchor)) return;

        Vector3 spawnPosition = anchor;
        if (!Block.CanPlaceAt(spawnPosition, prefabToPlace)) return;

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

    // Kept as a small targeting adapter for existing source-terminal regression tests.
    private bool TryGetWireTarget(Block wirePrefab, out Vector3Int targetCell,
        out Vector3Int targetNeighbor, out Quaternion rotation)
    {
        bool found = TrySelectWireEndpoint(out var endpoint, out var axis);
        targetCell = endpoint.Cell;
        targetNeighbor = endpoint.IsPin ? endpoint.PinCell : endpoint.Cell - axis;
        rotation = Quaternion.FromToRotation(Vector3.up, axis);
        return found && wirePrefab != null;
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
            isFlying = true;
            rb.useGravity = false;
        } else
        {
            isFlying = false;
            rb.useGravity = useGravityBeforeFlight;
        }
    }

    private void ResetFlightForDisable()
    {
        waitingForSecondSpacePress = false;
        if (!isFlying) return;

        isFlying = false;
        if (rb != null) rb.useGravity = useGravityBeforeFlight;
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
