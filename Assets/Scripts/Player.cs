using UnityEngine;
using UnityEngine.InputSystem;

public class Player : MonoBehaviour
{
    public CameraSettings cameraSettings;
    public float speed = 10f;
    public float jumpForce = 5f;
    public float reachDistance = 5f;
    public Block activeBlockPrefab; // What you are currently "holding" to place

    float xRotation;
    float yRotation;
    bool isGrounded;
    float breakSeconds;

    Block targetBlock;
    Block breakingBlock;
    RaycastHit targetRaycastHit;
    Rigidbody rb;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true; // Crucial for player controllers
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckTargetBlock();

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
        
        if (Physics.Raycast(ray, out targetRaycastHit, reachDistance)) 
        {
            targetBlock = targetRaycastHit.transform.GetComponent<Block>();
        }
        else
        {
            targetBlock = null;
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
        if (targetBlock == null || activeBlockPrefab == null) return;

        // Simplify placement using the Normal
        // The normal is the direction pointing out from the face we hit
        Vector3 spawnPosition = targetBlock.transform.position + targetRaycastHit.normal;

        Instantiate(activeBlockPrefab, spawnPosition, Quaternion.identity);
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