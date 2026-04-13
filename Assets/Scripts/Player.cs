using UnityEngine;
using UnityEngine.InputSystem; // Included for New Input System
using UnityEngine.SceneManagement;

// This script handles player movement, looking around, jumping, and block interaction (breaking and placing).
// Comments have been added via VS Code's AI

public class Player : MonoBehaviour
{
    public CameraSettings cameraSettings; // Grouping camera-related settings for cleaner inspector
    public float speed = 10f; // Movement speed
    public float jumpForce = 5f; // Force applied when jumping
    public float reachDistance = 5f; // How far the player can reach to interact with blocks

    public GameObject blockHighlighter; // Visual indicator for targeted block
    public SaveManager saveManager;
    public Block[] blockPalette; // Array of different block prefabs (Grass, Wire, Voltage, etc.)
    private int selectedBlockIndex = 0; // The current slot selected

    float xRotation; // Vertical rotation (looking up/down)
    float yRotation; // Horizontal rotation (looking left/right)
    bool isGrounded; // Simple grounded check
    float breakSeconds; // How long we've been breaking the current block, used for break progress

    Block targetBlock; // The block currently being targeted by the player's crosshair
    Block breakingBlock; // The block currently being broken (can differ from targetBlock if we look away while breaking)
    RaycastHit targetRaycastHit; // Store the raycast hit info for use in both breaking and placing (especially for placement normal)
    Rigidbody rb; // Reference to the player's Rigidbody for movement and physics interactions

    void Start() // Initialization
    {
        rb = GetComponent<Rigidbody>(); // Get the Rigidbody component for movement
        rb.freezeRotation = true; // Crucial for player controllers (prevents physics from rotating the player when colliding with blocks)
        Cursor.lockState = CursorLockMode.Locked; // Lock the cursor to the center of the screen for better control
        Cursor.visible = false; // Hide cursor for immersion
    }

    void Update() // Main update loop for handling input and interactions
    {
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

        if (Keyboard.current.kKey.wasPressedThisFrame)
        {   

            // Use PlayerPrefs to get the name of the project we opened from the menu
            string currentProject = PlayerPrefs.GetString("CurrentProjectToLoad", "AutoSave");

            if (saveManager != null) 
            {
                saveManager.Save(currentProject);
                Debug.Log("World Saved as: " + currentProject);
            }
            else 
            {
                Debug.LogError("SaveManager is missing from the Player script! Drag it in via the Inspector.");
            }
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            // Unlock the cursor so you can click buttons in the menu
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Load the Main Menu scene
            SceneManager.LoadScene("MainMenu"); 
        }
    }

    void CheckRotation() // Handles looking around with the mouse
    {
        // Read mouse delta for rotation
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        float mouseX = mouseDelta.x * Time.deltaTime * cameraSettings.sensitivityX;
        float mouseY = mouseDelta.y * Time.deltaTime * cameraSettings.sensitivityY;

        // Update rotation values
        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        // Apply rotation to camera and player
        cameraSettings.camera.transform.localRotation = Quaternion.Euler(xRotation, 0, 0);
        transform.rotation = Quaternion.Euler(0, yRotation, 0);
    }

    void CheckMovement() // Handles WASD movement based on player rotation
    {
        // Read keyboard input for movement
        float moveX = 0;
        float moveZ = 0;

        // New Input System checks for WASD keys
        if (Keyboard.current.wKey.isPressed) moveZ = 1;
        if (Keyboard.current.sKey.isPressed) moveZ = -1;
        if (Keyboard.current.aKey.isPressed) moveX = -1;
        if (Keyboard.current.dKey.isPressed) moveX = 1;

        // Calculate movement direction based on player orientation
        Vector3 move = (transform.forward * moveZ + transform.right * moveX).normalized * speed;
        rb.linearVelocity = new Vector3(move.x, rb.linearVelocity.y, move.z);
    }

    void CheckJump() // Handles jumping when the space key is pressed and the player is grounded
    {
        // New Input System check for Space key
        if (isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse); // Apply an instant upward force for jumping
        }
    }

    void CheckTargetBlock() // Handles raycasting to find the block the player is currently looking at and updates the block highlighter
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

    void TryBreakBlock() // Handles breaking the targeted block over time while the left mouse button is held down
    {   
        // If we don't have a target block or if we switched to a different block, reset the breaking progress
        if (!targetBlock) { breakSeconds = 0; return; } 
        if (breakingBlock != targetBlock) { breakSeconds = 0; } 

        // Set the current block being broken to the target block (even if we look away, we want to continue breaking the same block until it's broken or we stop)
        breakingBlock = targetBlock; 
        breakSeconds += Time.deltaTime;

        // Try to break the block with the accumulated break time. If it returns true, the block is broken and we reset everything.
        if (targetBlock.TryBreak(breakSeconds)) 
        { 
            breakSeconds = 0; 
            targetBlock = null;
        }
    }

    void TryPlaceBlock() 
    {   
        if (targetBlock == null || blockPalette.Length == 0) return;

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

        // Single Instantiate call at the end using whichever rotation was calculated
        Instantiate(prefabToPlace, spawnPosition, spawnRotation);
    }

    // Simple trigger-based grounded check. This assumes the player has a trigger collider at their feet to detect when they're on the ground.
    private void OnTriggerStay(Collider other) => isGrounded = true;
    private void OnTriggerExit(Collider other) => isGrounded = false;

    // Struct to group camera settings together for cleaner inspector and code organization
    [System.Serializable]
    public struct CameraSettings
    {
        public Camera camera;
        public float sensitivityX;
        public float sensitivityY;
    }
}