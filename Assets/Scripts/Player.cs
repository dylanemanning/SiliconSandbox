using System;
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
    private int selectedBlockIndex = 0; // The current slot selected

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

    void Update()
    {
        if (PauseManager.IsPaused) return;

        CheckFall();
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckTargetBlock();
        HandleHotbarInput();

        // New Input System check for Left Click (Breaking)
        if (KeybindSettings.IsMouseButtonPressed(KeybindSettings.BreakMouseButton)) 
        { 
            TryBreakBlock(); 
        }
        else 
        { 
            breakSeconds = 0; 
            breakingBlock = null;
        }

        // New Input System check for Right Click (Placement)
        if (KeybindSettings.WasMouseButtonPressedThisFrame(KeybindSettings.PlaceMouseButton)) 
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
            targetBlock = targetRaycastHit.transform.GetComponent<Block>(); // Try to get the Block component from the hit object

            if (targetBlock != null) // If we hit a block, show the highlighter at the block's position
            {
                if (blockHighlighter != null)
                {
                    blockHighlighter.SetActive(true);
                    blockHighlighter.transform.position = targetBlock.transform.position;
                }
            }
        }
        else // If we don't hit anything, clear the target block and hide the highlighter
        {
            targetBlock = null;
            if (blockHighlighter != null)
                blockHighlighter.SetActive(false);
        }
    }

    void HandleHotbarInput()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;
        // Directly check each number key. 
        // This is the most reliable way with the New Input System's current API.
        if (Keyboard.current.digit1Key.wasPressedThisFrame) selectedBlockIndex = 0;
        else if (Keyboard.current.digit2Key.wasPressedThisFrame) selectedBlockIndex = 1;
        else if (Keyboard.current.digit3Key.wasPressedThisFrame) selectedBlockIndex = 2;
        else if (Keyboard.current.digit4Key.wasPressedThisFrame) selectedBlockIndex = 3;
        else if (Keyboard.current.digit5Key.wasPressedThisFrame) selectedBlockIndex = 4;
        else if (Keyboard.current.digit6Key.wasPressedThisFrame) selectedBlockIndex = 5;
        else if (Keyboard.current.digit7Key.wasPressedThisFrame) selectedBlockIndex = 6;
        else if (Keyboard.current.digit8Key.wasPressedThisFrame) selectedBlockIndex = 7;
        else if (Keyboard.current.digit9Key.wasPressedThisFrame) selectedBlockIndex = 8;

        // Clamp the index to ensure it doesn't exceed the number of blocks you've actually added to the palette
        if (blockPalette != null && blockPalette.Length > 0)
        {
            selectedBlockIndex = Mathf.Clamp(selectedBlockIndex, 0, blockPalette.Length - 1);
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

        Block prefabToPlace = blockPalette[selectedBlockIndex];
        if (prefabToPlace == null) return;

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
