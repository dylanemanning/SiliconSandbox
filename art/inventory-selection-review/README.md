# Inventory-to-hotbar assignment — issue #92

Open the inventory with **E**, hover over a populated component image and press **1–9** to assign it to that hotbar slot. The hovered frame is highlighted, the footer identifies the component and a confirmation names the destination slot. Close with **E** or **Esc**, then select the destination using the existing number keys or mouse wheel and place blocks normally.

Assignments replace only the destination slot. The active hotbar index stays unchanged during assignment. Inventory entries remain available, and multiple hotbar slots may contain the same component, consistent with unlimited block supply. Empty entries, moving off an entry, closing the inventory and pausing cannot assign stale content. Assignments last for the current gameplay session; saving hotbar configuration is separate work.

The inventory processes opening/closing before player input and resolves number-key assignment in LateUpdate, after the EventSystem has updated the hovered entry. This prevents assigning the previous entry when the pointer moves and a key is pressed in the same frame.

The UI still uses the existing hotbar appearance. Component images in the hotbar, initial-slot image mapping, and inventory-row styling are tracked in **#111**. This feature builds on **#95** and its prerequisites **#94**, **#87**, and **#110**.

## Verification

Unity 6000.3.8f1 on macOS. PlayMode tests exercise real pointer raycasts through the InputSystem UI module, all nine destination keys, replacement and duplicate assignments, unchanged catalog entries, preserved active index, empty/no-hover and pause guards, and selecting and placing an assigned Clock prefab with the normal gameplay mouse action. Existing inventory input tests cover movement/world-click blocking and closing before pausing.

The full PlayMode suite passed: **30 passed, 2 skipped, 0 failed**. The two pre-existing pause-menu settings tests remain skipped. The macOS development build succeeded. Human testing of this new build is still pending. The macOS development player is generated under `Builds/InventorySelection/SiliconSandbox.app` (ignored).

## Manual review

1. Open gameplay and press E. Hover over a gate and check the frame highlight and component name.
2. Press 3. Check the footer confirms slot 3, and that the image remains in the inventory.
3. Hover over Clock and press 9, then press 2 to assign it to a second slot.
4. Hover over an empty slot and press a number; no assignment should occur.
5. Close the inventory, select 3 and place the gate; select 9 or 2 and place a Clock.
6. Reopen the inventory and replace slot 3 with a different component; close, select 3 and place the replacement.
