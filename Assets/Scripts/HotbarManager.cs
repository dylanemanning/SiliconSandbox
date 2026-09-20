using UnityEngine;
using UnityEngine.InputSystem;

public class HotbarManager : MonoBehaviour
{
    [SerializeField] private HotbarSlot[] slots;

    private int selectedSlot = 0;

    public Block SelectedBlock => slots != null && slots.Length > 0
        ? slots[selectedSlot].BlockPrefab
        : null;

    private void Start()
    {
        SelectSlot(0);
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            SelectSlot(0);
        }
        else if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            SelectSlot(1);
        }
        else if (Keyboard.current.digit3Key.wasPressedThisFrame)
        {
            SelectSlot(2);
        }
        else if (Keyboard.current.digit4Key.wasPressedThisFrame)
        {
            SelectSlot(3);
        }
        else if (Keyboard.current.digit5Key.wasPressedThisFrame)
        {
            SelectSlot(4);
        }
        else if (Keyboard.current.digit6Key.wasPressedThisFrame)
        {
            SelectSlot(5);
        }
        else if (Keyboard.current.digit7Key.wasPressedThisFrame)
        {
            SelectSlot(6);
        }
        else if (Keyboard.current.digit8Key.wasPressedThisFrame)
        {
            SelectSlot(7);
        }
        else if (Keyboard.current.digit9Key.wasPressedThisFrame)
        {
            SelectSlot(8);
        }else
        {
            float scroll = Mouse.current.scroll.ReadValue().y;

            if (scroll > 0)
            {
                SelectSlot((selectedSlot - 1 + slots.Length) % slots.Length);
            }
            else if (scroll < 0)
            {
                SelectSlot((selectedSlot + 1) % slots.Length);
            }
        }
    }

    private void SelectSlot(int index)
    {
        if (index < 0 || index >= slots.Length)
            return;

        selectedSlot = index;

        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].SetSelected(i == selectedSlot);
        }
    }
}