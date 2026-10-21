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
        UpdateWireFeedback();
        bool cancelled = ConsumeWireCancelThisFrame();
        if (PauseManager.IsPaused || InventoryGrid.IsOpen || InventoryGrid.ClosedThisFrame)
        {
            CancelWireRoute();
            return;
        }

        CheckFall();
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckTargetBlock();

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

    private bool TryToggleSource()
    {
        var keyboard = Keyboard.current;
        var mouse = Mouse.current;
        if (keyboard == null || mouse == null || !mouse.rightButton.wasPressedThisFrame ||
            !(keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)) return false;
        if (targetBlock == null || !targetBlock.TryGetComponent<LogicSignalSource>(out var source) ||
            source is LogicClockSource) return false;

        // Consume the interaction so this click cannot also place a block or finish a wire route.
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
