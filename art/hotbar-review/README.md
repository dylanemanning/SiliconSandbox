# Inventory-style hotbar — issue #111

The hotbar is a compact, one-row version of the inventory: nine 80×80 reference-pixel cells, 8-pixel gaps, 3-pixel grey frames, dark interiors and 6-pixel image padding. A dark panel surrounds the row with 24-pixel padding on all four sides. Its width matches the inventory panel. Both canvases use the same 1920×1080 scaling reference and width/height matching. A gold frame marks the selected slot, and small dark-backed 1–9 labels remain readable over the images.

`Assets/Prefabs/Hotbar.prefab` replaces the legacy nested scene widgets. Its slots have individual frame and icon references. `HotbarBuilder.Build` rebuilds the UI while preserving the nine existing gameplay prefab choices and updates the player's manager reference in SampleScene. It also generates the review captures. The normal gameplay view is `hotbar.png`; `hotbar-and-inventory.png` shows both panels. The inventory's modal backdrop dims the underlying hotbar while the inventory is open.

`InventoryCatalog.FindForPrefab` resolves exact assets first, then component-type variants. Clock matching precedes Constant Signal matching because the clock inherits from the signal source. The low source keeps its actual low placement prefab while sharing the Constant Signal thumbnail. Gate types keep distinct symbols; wire/LED state variants share their representative type images. Unknown or empty entries hide the icon rather than showing a white rectangle.

`HotbarSlot.SetItem` changes the placement prefab and image together when #112's hover/number-key assignment replaces a slot. Number keys and scrolling still select the placement tool. Images are pre-rendered Sprites from #95; no thumbnail models or cameras are created in gameplay.

## Dependencies

Builds on #112 (issue #92), #95, #94, #87 and #110. Merge those prerequisites before this PR; it targets `dev`. Assignments retain #112's session-only lifetime.

## Verification

**EditMode: 42 passed, 20 skipped, 0 failed. PlayMode: 32 passed, 2 skipped, 0 failed.** The skipped tests are existing Windows-native fixtures on macOS and pause-menu settings cases. The macOS development build succeeded. Human testing of the new build is pending. Tests check panel and grid dimensions/colors/scaling, all original hotbar choices and initial images, preservation of the low source, absence of duplicate legacy slot components, image updates through real hover/key input, all 13 component images, empty slots, number/scroll highlights and type-variant image lookup. Existing inventory-assignment tests verify normal placement from a reassigned hotbar slot.

The macOS development player is built by `HotbarBuilder.BuildMac` to `Builds/HotbarImages/SiliconSandbox.app` (ignored).

## Manual review

1. Open gameplay and compare the boxed hotbar to the inventory opened with E.
2. Check 1–9 labels and the selected gold border; use number keys and the mouse wheel.
3. Hover a gate in the inventory and press 3. Check the hotbar image changes immediately while the catalog image remains available.
4. Close the inventory, select 3 and place the gate. Repeat with Clock, Wire and LED.
5. Check slot 3's original low-source choice before replacing it: it retains low-source placement behavior while using the shared Constant Signal image.
