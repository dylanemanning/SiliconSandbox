# Empty inventory grid — issue #31 / PR #94

This document records the base grid in #94. PR #95 adds catalog images; see `art/inventory-images-review/README.md`.

The base inventory is an empty 4-row, 9-column display, separate from the existing hotbar. It is instantiated from `Assets/Prefabs/InventoryGrid.prefab` in `SampleScene`, the gameplay scene loaded by MainMenu and included in build settings.

- Press E to open or close it. Escape (or the configured pause key) closes the inventory first; a subsequent pause-key press opens the pause menu.
- Opening releases the cursor, stops horizontal player movement, hides the targeting highlight and blocks movement/look/build/break/hotbar input. Closing returns control to gameplay. Simulation time continues normally.
- The grid uses 80×80 logical-pixel cells and 8-pixel gaps at the 1920×1080 reference resolution. CanvasScaler scales the UI uniformly, so the slots remain square and the gaps stay at 10% of the cell size.
- All 36 slots are empty. Item data, icons, drag/drop and inventory/hotbar transfers are later work. Future item images can fit the square slot area; no Blender inventory-image models are required for the empty display.
- The same original hotbar layout and contents remain in place. Gameplay art/lighting integration from PR #110 is independent; both PRs modify SampleScene and Player, so their scene/input changes should be retained when combining them.

## Verification

Unity 6000.3.8f1 on macOS:

- PlayMode: 21 passed, 2 skipped, 0 failed. The skipped cases are the existing unimplemented pause-menu settings tests. Inventory tests cover E toggling, 36 square slots, 10% gaps, Escape/pause interaction, cursor release and blocked movement/look/world/hotbar input. OS cursor relocking is checked outside batchmode, where Game-view focus can be established.
- EditMode: 29 passed, 20 skipped, 0 failed. The existing Windows-native fixture is skipped on macOS.
- `inventory-grid.png` is a Unity capture of the implemented UI; camera/canvas changes used for the capture are temporary and are not saved to the scene.
- The macOS development build succeeded with MainMenu and SampleScene. Local output: `Builds/InventoryGrid/SiliconSandbox.app` (ignored).

To regenerate the prefab and gameplay reference, use **SiliconSandbox → UI → Build empty inventory grid**. UI dimensions and colors are authored in `InventoryGridBuilder`; the saved prefab and `InventoryGrid` runtime controller provide the player-build implementation.
