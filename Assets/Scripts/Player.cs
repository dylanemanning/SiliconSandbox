using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public partial class Player : MonoBehaviour
{
    public CameraSettings cameraSettings;
    [Header("Walking (blocks per second)")]
    public float speed = 7.5f;
    public float sprintMultiplier = 1.3f;
    public float crouchSpeedMultiplier = 0.3f;
    public float groundAcceleration = 70f;
    public float airAcceleration = 20f;

    [Header("Jumping")]
    public float jumpHeight = 1.25f;
    public float gravity = 32f;
    public float maxFallSpeed = 78f;
    public float jumpCooldown = 0.2f;

    [Header("Flight (blocks per second)")]
    public float flightStep = 4.5f; // vertical flight speed
    public float flightSpeed = 15f; // default horizontal flight speed
    public float maxFlightSpeedMultiplier = 3f;
    public int flightSpeedScrollSteps = 20;
    public float flightAcceleration = 45f;

    [Header("Crouch")]
    [Range(0.1f, 1f)] public float crouchEyeHeightScale = 0.5f;
    [Tooltip("Collider height while crouching, as a share of the standing height.")]
    [Range(0.3f, 1f)] public float crouchColliderHeightScale = 0.6f;
    public float crouchTransitionSpeed = 10f;
    [Tooltip("How far below the feet still counts as ground for crouch edge protection.")]
    public float crouchEdgeDrop = 0.55f;

    public float reachDistance = 15f;
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
    private float currentFlightSpeed = -1f;
    private float nextJumpTime;
    private bool isCrouching;
    private float standingEyeHeight = -1f;
    private CapsuleCollider bodyCollider;
    private float standingColliderHeight = -1f;
    private Vector3 standingColliderCenter;
    private readonly Collider[] crouchOverlap = new Collider[16];

    public bool IsFlying => isFlying;
    public bool IsCrouching => isCrouching;
    public float CurrentFlightSpeed => currentFlightSpeed > 0f ? currentFlightSpeed : flightSpeed;
    public float MinFlightSpeed => speed;
    public float MaxFlightSpeed => flightSpeed * maxFlightSpeedMultiplier;

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
        // Player gravity is applied in FixedUpdate so jumps follow Minecraft-like
        // timing without changing global gravity for every other rigidbody.
        rb.useGravity = false;
        currentFlightSpeed = flightSpeed;
        if (cameraSettings.camera != null) standingEyeHeight = cameraSettings.camera.transform.localPosition.y;
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
        if (PauseManager.IsPaused || InventoryGrid.IsOpen || InventoryGrid.ClosedThisFrame || PackagingWandController.IsConfirmationOpen)
        {
            CancelWireRoute();
            return;
        }

        CheckFall();
        CheckRotation();
        CheckCrouch();
        CheckFlightSpeedScroll();
        CheckMovement();
        KeepCrouchingPlayerOnEdges();
        CheckJump();
        CheckTargetBlock();

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

        bool packagingWandSelected = hotbar != null && hotbar.IsPackagingWandSelected;
        packagingWandController.HandleTool(packagingWandSelected, targetBlock, targetRaycastHit);
        if (packagingWandSelected)
        {
            CancelWireRoute();

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

        if (KeybindSettings.IsPressed(KeybindSettings.MoveForward)) moveZ += 1;
        if (KeybindSettings.IsPressed(KeybindSettings.MoveBackward)) moveZ -= 1;
        if (KeybindSettings.IsPressed(KeybindSettings.MoveLeft)) moveX -= 1;
        if (KeybindSettings.IsPressed(KeybindSettings.MoveRight)) moveX += 1;

        Vector3 direction = (transform.forward * moveZ + transform.right * moveX).normalized;
        float targetSpeed;
        float acceleration;
        if (isFlying)
        {
            targetSpeed = CurrentFlightSpeed;
            acceleration = flightAcceleration;
        }
        else
        {
            targetSpeed = speed;
            if (isCrouching) targetSpeed *= crouchSpeedMultiplier;
            else if (moveZ > 0 && KeybindSettings.IsPressed(KeybindSettings.Sprint)) targetSpeed *= sprintMultiplier;
            acceleration = isGrounded ? groundAcceleration : airAcceleration;
        }

        // Accelerate toward the requested velocity instead of snapping to it:
        // quick on the ground, with carried momentum in the air and in flight.
        Vector3 velocity = rb.linearVelocity;
        Vector3 horizontal = Vector3.MoveTowards(new Vector3(velocity.x, 0f, velocity.z),
            direction * targetSpeed, acceleration * Time.deltaTime);
        rb.linearVelocity = new Vector3(horizontal.x, velocity.y, horizontal.z);
    }

    void CheckJump()
    {
        if (!isFlying)
        {
            // Holding jump keeps jumping on each landing, as in Minecraft.
            if (isGrounded && Time.time >= nextJumpTime && rb.linearVelocity.y <= 0.1f &&
                KeybindSettings.IsPressed(KeybindSettings.Jump))
            {
                Vector3 velocity = rb.linearVelocity;
                rb.linearVelocity = new Vector3(velocity.x, JumpVelocity, velocity.z);
                nextJumpTime = Time.time + jumpCooldown;
            }
            return;
        }

        float verticalInput = (KeybindSettings.IsPressed(KeybindSettings.Jump) ? 1f : 0f)
            - (KeybindSettings.IsPressed(KeybindSettings.FlyDown) ? 1f : 0f);
        // Touching the ground while descending ends flight, as in Minecraft creative mode.
        if (verticalInput < 0f && isGrounded)
        {
            ToggleFlight();
            return;
        }
        MoveWhileFlying(verticalInput);
    }

    /// <summary>Launch speed that reaches jumpHeight under this player's gravity.</summary>
    public float JumpVelocity => Mathf.Sqrt(2f * Mathf.Max(0.01f, gravity) * Mathf.Max(0f, jumpHeight));

    void MoveWhileFlying(float verticalInput)
    {
        Vector3 velocity = rb.linearVelocity;
        float vertical = Mathf.MoveTowards(velocity.y, verticalInput * flightStep,
            flightAcceleration * Time.deltaTime);
        rb.linearVelocity = new Vector3(velocity.x, vertical, velocity.z);
    }

    private void FixedUpdate()
    {
        if (rb == null || isFlying || rb.isKinematic) return;
        Vector3 velocity = rb.linearVelocity;
        velocity.y = Mathf.Max(velocity.y - gravity * Time.fixedDeltaTime, -maxFallSpeed);
        rb.linearVelocity = velocity;
    }

    void CheckCrouch()
    {
        // Crouching shares the descend key; while flying that key descends instead.
        bool wantsCrouch = !isFlying && KeybindSettings.IsPressed(KeybindSettings.FlyDown);
        // As in Minecraft, stay crouched under a low ceiling until there is room to stand.
        if (!wantsCrouch && isCrouching && !isFlying && !CanStandUp()) wantsCrouch = true;
        isCrouching = wantsCrouch;
        ApplyCrouchCollider();

        if (cameraSettings.camera == null) return;
        Transform view = cameraSettings.camera.transform;
        if (standingEyeHeight < 0f) standingEyeHeight = view.localPosition.y;
        float targetHeight = isCrouching ? standingEyeHeight * crouchEyeHeightScale : standingEyeHeight;
        Vector3 local = view.localPosition;
        local.y = Mathf.MoveTowards(local.y, targetHeight,
            crouchTransitionSpeed * standingEyeHeight * Time.deltaTime);
        view.localPosition = local;
    }

    private bool ResolveBodyCollider()
    {
        if (bodyCollider == null) bodyCollider = GetComponent<CapsuleCollider>();
        if (bodyCollider == null) return false;
        if (standingColliderHeight < 0f)
        {
            standingColliderHeight = bodyCollider.height;
            standingColliderCenter = bodyCollider.center;
        }
        return true;
    }

    // Shrinks the hitbox from the top while crouching; the feet stay where they are.
    private void ApplyCrouchCollider()
    {
        if (!ResolveBodyCollider()) return;
        float height = isCrouching ? standingColliderHeight * crouchColliderHeightScale : standingColliderHeight;
        if (Mathf.Approximately(bodyCollider.height, height)) return;
        float feet = standingColliderCenter.y - standingColliderHeight * .5f;
        bodyCollider.height = height;
        bodyCollider.center = new Vector3(standingColliderCenter.x, feet + height * .5f, standingColliderCenter.z);
    }

    /// <summary>True when the standing hitbox would not overlap any solid block.</summary>
    public bool CanStandUp()
    {
        if (!ResolveBodyCollider()) return true;
        float radius = Mathf.Max(.01f, bodyCollider.radius - .02f);
        Vector3 feet = transform.position + Vector3.up * (standingColliderCenter.y - standingColliderHeight * .5f);
        Vector3 bottom = feet + Vector3.up * (radius + .05f);
        Vector3 top = feet + Vector3.up * (standingColliderHeight - radius - .01f);
        return !OverlapsSolid(Physics.OverlapCapsuleNonAlloc(bottom, top, radius, crouchOverlap,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
    }

    private bool OverlapsSolid(int count)
    {
        for (int i = 0; i < count; i++)
        {
            Collider other = crouchOverlap[i];
            if (other == null || other.attachedRigidbody == rb || other.transform.IsChildOf(transform)) continue;
            return true;
        }
        return false;
    }

    // Minecraft-style sneaking: a crouching player on the ground cannot walk off an
    // edge. Each horizontal direction is stopped when the step would leave nothing
    // under the player's footprint within crouchEdgeDrop.
    void KeepCrouchingPlayerOnEdges()
    {
        if (!isCrouching || isFlying || rb == null) return;
        // Probe the ground directly: the trigger-based isGrounded flag clears when any
        // trigger contact ends, even while the player still stands on a block.
        if (rb.linearVelocity.y > .1f || !HasGroundBelow(rb.position)) return;
        Vector3 velocity = rb.linearVelocity;
        float lookAhead = Mathf.Max(Time.deltaTime, Time.fixedDeltaTime) * 2f;
        // The capsule's rounded bottom rolls off a corner once its centre passes the
        // edge, so keep the centre (plus a small margin) over the block.
        float Ahead(float speedAlongAxis) => speedAlongAxis == 0f ? 0f
            : Mathf.Sign(speedAlongAxis) * Mathf.Max(Mathf.Abs(speedAlongAxis) * lookAhead, CrouchEdgeMargin);
        Vector3 position = rb.position;
        float aheadX = Ahead(velocity.x), aheadZ = Ahead(velocity.z);
        if (aheadX != 0f && !HasGroundBelow(position + new Vector3(aheadX, 0f, 0f))) velocity.x = 0f;
        if (aheadZ != 0f && !HasGroundBelow(position + new Vector3(0f, 0f, aheadZ))) velocity.z = 0f;
        if (velocity.x != 0f && velocity.z != 0f && !HasGroundBelow(position + new Vector3(aheadX, 0f, aheadZ)))
            velocity.x = velocity.z = 0f;
        rb.linearVelocity = velocity;
    }

    private const float CrouchEdgeMargin = .1f;

    private bool HasGroundBelow(Vector3 playerPosition)
    {
        if (!ResolveBodyCollider()) return true;
        const float halfWidth = .05f; // a narrow probe under the body's centre
        Vector3 feet = playerPosition + Vector3.up * (standingColliderCenter.y - standingColliderHeight * .5f);
        Vector3 center = feet - Vector3.up * (crouchEdgeDrop * .5f + .02f);
        var extents = new Vector3(halfWidth, crouchEdgeDrop * .5f, halfWidth);
        return OverlapsSolid(Physics.OverlapBoxNonAlloc(center, extents, crouchOverlap, Quaternion.identity,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore));
    }

    void CheckFlightSpeedScroll()
    {
        if (!isFlying || !KeybindSettings.FlightSpeedModifierHeld) return;
        float scroll = KeybindSettings.ReadScroll();
        if (scroll == 0f) return;
        AdjustFlightSpeed(scroll > 0f ? 1 : -1);
    }

    /// <summary>One scroll notch changes flight speed by a fixed step between walking pace and the maximum.</summary>
    public void AdjustFlightSpeed(int notches)
    {
        float step = (MaxFlightSpeed - MinFlightSpeed) / Mathf.Max(1, flightSpeedScrollSteps);
        currentFlightSpeed = Mathf.Clamp(CurrentFlightSpeed + notches * step, MinFlightSpeed, MaxFlightSpeed);
        ShowPlayerStatus($"Flight speed: {currentFlightSpeed / flightSpeed:0.0#}x");
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
        // Ignore triggers such as the player's own jump trigger at its feet.
        if (Physics.Raycast(ray, out targetRaycastHit, reachDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) 
        {
            targetBlock = targetRaycastHit.collider.GetComponentInParent<Block>(); // Try to get the Block component from the hit object

            if (targetBlock != null) // If we hit a block, show the highlighter at the block's position
            {
                if (blockHighlighter != null)
                {
                    blockHighlighter.SetActive(true);
                    if (targetBlock.isGround)
                    {
                        // The floor is one large block: outline the grid cell under the crosshair.
                        Vector3 normal = SnapToCardinalAxis(targetRaycastHit.normal);
                        blockHighlighter.transform.position = Vector3Int.RoundToInt(targetRaycastHit.point - normal * .5f);
                        blockHighlighter.transform.rotation = Quaternion.identity;
                        blockHighlighter.transform.localScale = Vector3.one * 1.02f;
                    }
                    else
                    {
                        var collider = targetBlock.GetComponent<Collider>();
                        blockHighlighter.transform.position = collider.bounds.center;
                        blockHighlighter.transform.rotation = targetBlock.transform.rotation;
                        blockHighlighter.transform.localScale = (Vector3)targetBlock.footprint * 1.02f;
                    }
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
        if (!KeybindSettings.InteractPressedThisFrame()) return false;
        // Holding Crouch overrides interaction, so Shift + right click places a block
        // against an interactive one, as in Minecraft.
        if (KeybindSettings.CrouchHeld) return false;
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
        // Every circuit component (gates, signal sources, clocks, LEDs) faces the way
        // the player looks when it is placed.
        if (prefabToPlace.GetComponent<LogicSignalBlock>() != null)
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
        // A wire end already pointing at one of the new block's pins joins it.
        WireTopology.ConnectFacingDeadEnds(placedBlockObject.GetComponent<LogicSignalBlock>());
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
        isFlying = !isFlying;
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
