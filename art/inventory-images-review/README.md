# Inventory block images and catalog — PR #95

This builds on the grid in #94, models in #87, and gameplay prefabs in #110. It implements inventory image display (#90) and the current block catalog (#91). Item selection and inventory-to-hotbar transfers remain #92.

## Entries

The catalog contains 13 component types: BUFFER, NOT, AND, NAND, OR, NOR, XOR, XNOR, Constant Signal, Clock, Wire, LED, and Building Block. Each `InventoryItem` has a stable ID, display name, gameplay Block prefab, and Sprite. The 36-slot grid shows these 13 entries in catalog order, leaving 23 empty slots.

There is one entry per type. Constant Signal uses the high-source prefab by default; its low-state variant shares that component type. Wire shapes/states and LED/Clock states do not create duplicate entries. Icons are representative images rather than live simulation readings. Existing hotbar choices, including the low source, remain unchanged.

## Images and binding

- `InventoryBlockImageBuilder` renders transparent 256×256 PNGs from the existing models. Blocks use straight top-down views of their symbol faces, with gate outputs pointing right for familiar left-to-right reading. The wire retains its angled view. The basic building block uses its actual gameplay prefab. Lighting and grading match the model-review settings.
- `Assets/UI/InventoryIcons/` contains imported Sprite assets. Images are baked in the Editor and referenced by the catalog; gameplay does not create thumbnail cameras or render models for each UI slot.
- `Assets/Inventory/DefaultInventoryCatalog.asset` connects those images to actual gameplay prefabs.
- `InventoryGrid.RefreshImages` binds catalog entries on enable. `InventorySlotImage.SetItem` changes or clears a slot while preserving its square frame and aspect ratio. Missing entries disable the icon and keep the empty slot visible.
- Slots retain the original 80×80 logical size and 8-pixel gaps. Selection, drag/drop, item movement and persistence of hotbar assignments are separate work.

Regenerate with **SiliconSandbox → UI → Generate inventory block images**. This generates images/catalog entries, attaches the slot views to the existing grid (creating the base grid if missing), and captures `inventory-images.png`. The original empty-grid capture from #94 is retained separately. The alpha-output setting is restored after rendering; project lighting and quality settings are not changed by icon generation.

## Verification

Unity 6000.3.8f1 on macOS:

- EditMode: **41 passed, 20 skipped, 0 failed**. Existing Windows-native tests are skipped on macOS. Tests cover all 13 types, unique IDs/images, valid gameplay prefabs, all eight gates, alpha margins, absence of error-shader pixels, white gate symbols and replacing/clearing slot content.
- PlayMode: **27 passed, 2 skipped, 0 failed**. Existing pause-menu settings tests remain skipped. Tests cover the live 13/23 slot binding, image/prefab correspondence, preserved layout, refresh/reopen behavior, existing inventory input handling and gameplay tests, and the absence of thumbnail cameras in gameplay.
- The macOS development player build succeeded. Local output: `Builds/InventoryImages/SiliconSandbox.app` (ignored). MainMenu loads the integrated gameplay scene containing the catalog and images.
- The Unity screenshot shows the populated UI with top-down block images and the angled wire image. The temporary camera-space capture disables post-processing on the UI so already-graded sprites are not graded twice; the real overlay UI is rendered after gameplay post-processing.
