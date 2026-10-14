using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Player : MonoBehaviour
{
    public CameraSettings cameraSettings;
    public float speed = 10f;
    public float jumpForce = 5f;
    public float flightStep = 1f;
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
        if (PauseManager.IsPaused || InventoryGrid.IsOpen || InventoryGrid.ClosedThisFrame) return;

        CheckFall();
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckTargetBlock();

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
            if (KeybindSettings.WasPressedThisFrame(KeybindSettings.Jump))
            {
                MoveWhileFlying(Vector3.up);
            }
            if (KeybindSettings.WasPressedThisFrame(KeybindSettings.FlyDown))
            {
                MoveWhileFlying(Vector3.down);
            }
        }
    }

    void MoveWhileFlying(Vector3 direction)
    {
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.MovePosition(rb.position + direction * flightStep);
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
        float playerYaw = transform.eulerAngles.y;
        Quaternion spawnRotation = Quaternion.Euler(0, Mathf.Round(playerYaw / 90f) * 90f, 0);
        if (!BlockPlacement.TryGetAnchor(prefabToPlace, targetRaycastHit.point,
                targetRaycastHit.normal, spawnRotation, out Vector3Int anchor)) return;
        Instantiate(prefabToPlace, anchor, spawnRotation);
        WorldSaveSystem.Instance?.MarkDirty();
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
