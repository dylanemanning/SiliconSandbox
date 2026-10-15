using UnityEngine;
using UnityEngine.InputSystem;

// Empty inventory display for #31. Item storage and hotbar contents are separate work.
[DefaultExecutionOrder(-100)]
public class InventoryGrid : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    private static InventoryGrid instance;
    private static int closedFrame = -1;
    private bool open;

    public static bool IsOpen => instance != null && instance.open;
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;
    public GameObject Panel => panel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetState() { instance = null; closedFrame = -1; }

    private void OnEnable()
    {
        instance = this;
        open = false;
        if (panel != null) panel.SetActive(false);
    }

    private void Update()
    {
        if (Keyboard.current == null || PauseManager.IsPaused) return;
        if (open && (Keyboard.current.escapeKey.wasPressedThisFrame
            || KeybindSettings.WasPressedThisFrame(KeybindSettings.Pause)))
            SetOpen(false);
        else if (Keyboard.current.eKey.wasPressedThisFrame) SetOpen(!open);
    }

    public void SetOpen(bool value)
    {
        if (value == open || (value && PauseManager.IsPaused)) return;
        open = value;
        if (panel != null) panel.SetActive(value);
        if (!value) closedFrame = Time.frameCount;
        Cursor.lockState = value || PauseManager.IsPaused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = value || PauseManager.IsPaused;
        if (value)
        {
            var player = FindFirstObjectByType<Player>();
            if (player == null) return;
            if (player.blockHighlighter != null) player.blockHighlighter.SetActive(false);
            var body = player.GetComponent<Rigidbody>();
            if (body != null) body.linearVelocity = new Vector3(0,body.linearVelocity.y,0);
        }
    }

    public static void Close() { if (instance != null) instance.SetOpen(false); }

    private void OnDisable()
    {
        if (instance != this) return;
        if (open) SetOpen(false);
        instance = null;
    }
}
