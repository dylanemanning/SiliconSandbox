using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public CameraSettings cameraSettings;
    public float speed = 10f;
    public float jumpForce = 5f;
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

    Block targetBlock;
    Block breakingBlock;
    RaycastHit targetRaycastHit;
    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Crucial for player controllers
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        CheckFall();
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckTargetBlock();
        HandleHotbarInput();

        // New Input System check for Left Click (Breaking)
        if (Mouse.current.leftButton.isPressed) 
        { 
            TryBreakBlock(); 
        }
        else 
        { 
            breakSeconds = 0; 
            breakingBlock = null;
        }

        // New Input System check for Right Click (Placement)
        if (Mouse.current.rightButton.wasPressedThisFrame) 
        { 
            TryPlaceBlock(); 
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
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
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

        if (Keyboard.current.wKey.isPressed) moveZ = 1;
        if (Keyboard.current.sKey.isPressed) moveZ = -1;
        if (Keyboard.current.aKey.isPressed) moveX = -1;
        if (Keyboard.current.dKey.isPressed) moveX = 1;

        Vector3 move = (transform.forward * moveZ + transform.right * moveX).normalized * speed;
        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);
    }

    void CheckJump()
    {
        if (isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        }
    }

    void CheckTargetBlock() 
    {
        // Shoot ray from center of camera
        Ray ray = cameraSettings.camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        
        // Check if the ray hits a block within reach distance
        if (Physics.Raycast(ray, out targetRaycastHit, reachDistance)) 
        {
            targetBlock = targetRaycastHit.transform.GetComponent<Block>(); // Try to get the Block component from the hit object

            if (targetBlock != null) // If we hit a block, show the highlighter at the block's position
            {
                blockHighlighter.SetActive(true);
                blockHighlighter.transform.position = targetBlock.transform.position;
            }
        }
        else // If we don't hit anything, clear the target block and hide the highlighter
        {
            targetBlock = null;
            blockHighlighter.SetActive(false);
        }
    }

    void HandleHotbarInput()
    {
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
        if (blockPalette.Length > 0)
        {
            selectedBlockIndex = Mathf.Clamp(selectedBlockIndex, 0, blockPalette.Length - 1);
        }
    }

    void TryBreakBlock() 
    {
        if (!targetBlock) { breakSeconds = 0; return; }
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
        
        // Round to Int ensures the grid is perfect (1.0, 2.0, etc.)
        Vector3 spawnPosition = targetBlock.transform.position + targetRaycastHit.normal;
        
        float playerYaw = transform.eulerAngles.y;
        Quaternion spawnRotation = Quaternion.identity;

        // Check if the prefab is a wire. 
        if (prefabToPlace.name.Contains("Wire")) 
        {
            if (playerYaw > 45 && playerYaw <= 135)
                spawnRotation = Quaternion.Euler(0,0, 90);
            else if (playerYaw > 135 && playerYaw <= 225)
                spawnRotation = Quaternion.Euler(0, 90, 90);
            else if (playerYaw > 225 && playerYaw <= 315)
                spawnRotation = Quaternion.Euler(0, 00, 90);
            else
                spawnRotation = Quaternion.Euler(0, 90, 90);
        }

        Instantiate(selectedBlock, spawnPosition, Quaternion.identity);
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
