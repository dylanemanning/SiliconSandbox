# Gameplay model integration

This work is separate from model PR #87 and depends on its approved art assets.

## Main game appearance

`Assets/Scenes/SampleScene.unity` is the gameplay scene loaded by MainMenu and included in build settings. Its camera has HDR and post-processing enabled. A scene global Volume uses `Assets/Settings/GameplayAppearance.asset`, copied from the approved review profile: ACES, exposure +0.4155, saturation -25, contrast -8, and Bloom intensity 0.35. The directional key/fill lights and runtime ambient probe use the approved preview values. The world floor uses the matching matte slate material. ModelArtReview remains excluded from builds.

Shared model materials/textures supply the approved matte finish, near-black symbol panels, preserved white symbols, and darker wire/source-pin colors. LED lens finish and emission are retained.

## Gameplay prefabs and placement

- Existing gameplay prefab GUIDs, root Block component IDs, and saved prefab names are preserved. `Voltage Source` and `Ground Source` are legacy save identifiers for Constant Signal high and low; Viewer Block is the six-pin LED.
- Eight gates, wires, Constant Signal variants, LED variants, and Clock have gameplay wrappers around the approved art. Existing hotbar choices stay in their original nine positions; no inventory UI or inventory-image models are added. Other gates and Clock remain available as prefabs for the later inventory work.
- BUFFER/NOT occupy one cell. Other gates occupy a 2×2 horizontal footprint, height one. The root is the local back-left cell, with the model and collider centered at local `(0.5, 0, 0.5)`. Inputs are on the two back cells; output is on the left front cell. Root transforms remain unit scale.
- Placement checks every occupied cell for collision and snaps rotation to 90-degree yaw. Targeting/breaking uses the root Block for the full collider. Hotbar slots now reference the same existing choices as Player's palette, fixing the separate number-key/mouse-wheel selection mismatch.
- The signal registry tracks all occupied cells and clears them when blocks move or are removed. Wires connect only to actual component pin faces. Straight/elbow visuals orient to neighbors; junctions reuse half-length approved wire meshes. Wire/source/LED state visuals use the approved materials/models rather than tinting block bodies. Gate output pins show the existing displayed state; unpowered pins retain their gray finish.
- Clock is a 1 Hz square-wave source by default, with a serialized frequency setting. Inventory access and an in-game frequency settings UI remain later work.

## Existing simulation and saved worlds

The existing circuit behavior remains: wires resolve connected sources and displayed gate outputs; LEDs show the received signal. GateBlock still receives its displayed state from the planned simulation driver (#82); this integration does not implement that driver or the netlist builder. It does not replace the native logic evaluator.

Older wire/source saves may contain legacy visual root scales; the approved gameplay wrappers restore at unit scale. Floor scaling is retained. Existing dense saves containing one-cell two-input gates may need manual spacing adjustments because those gates now physically occupy four cells. Port layouts intentionally change to the approved two-back-input/left-front-output layout.

## Verification

- Unity 6000.3.8f1 imported the gameplay wrappers and independently verified gate body/collider footprints and model-pin/port alignment.
- EditMode: 38 passed, 20 skipped, 0 failed. Skips are the existing Windows-native fixture on macOS. New tests cover all eight gates at four yaw rotations, collision in a far occupied cell, and preserved hotbar choices.
- PlayMode: 23 passed, 2 skipped, 0 failed. The existing pause-menu settings tests remain skipped. New tests cover main gameplay grading/HDR, state visuals, actual pin-face connections, registry cleanup, and elbow/junction orientation.
- `gameplay-models.png` is a Unity capture using the main gameplay lighting and gameplay prefab wrappers. Capture staging objects are temporary and are not saved into gameplay.
- A macOS development player build succeeded, including MainMenu and SampleScene. Local output: `Builds/GameplayIntegration/SiliconSandbox.app` (ignored). CI covers the Windows build. The existing native gate evaluator is Windows-only; this art integration does not add a macOS native plugin or the pending gate simulation driver.

To regenerate wrappers and the scene, use **SiliconSandbox → Art → Integrate approved models into gameplay**. This Editor tool is excluded from player code; the saved scene, assets, and runtime scripts provide the actual build integration.
