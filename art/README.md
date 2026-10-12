# SiliconSandbox model sources and Unity handoff

Editable Blender sources live in `art/models/`, outside Unity's `Assets` import tree. Developers and CI consume the 18 FBX assets under `Assets/Models/` without needing Blender. Source and exported models use Git LFS. No inventory-image or analog current-source models are included.

## Model geometry

One Unity unit is one block. Imported model roots have unit scale and local +Y up, +Z output/front, and +X right. The Blender source convention uses +Z up and +X output; `export_models.py` performs the conversion. All dimensions and pins are checked after importing in Unity, rather than inferred from export settings.

| Models | Body footprint and height | Pins |
| --- | --- | --- |
| BUFFER, NOT | 1×1 footprint, height 1 | A centered on -Z, Y centered on +Z |
| AND, NAND, OR, NOR, XOR, XNOR | 2×2 footprint, height 1 | A at `(-0.5,0,-1)`, B at `(0.5,0,-1)`, Y at `(-0.5,0,1)` |
| Constant Signal, Clock | 1×1 footprint, height 1 | Two opposed face-centered terminals on ±Z |
| LED | 1×1 footprint, height 1 | Six inputs, one at every face center |
| Straight and elbow wires | Endpoints half a block from the origin | Diameter 0.3; continuous capped geometry |

Pins are diameter 0.33, protruding 0.0625 beyond the authored face. The larger gates' output is on the left output-side face cell, never between cells. Renderer bounds include pin protrusions; body dimensions are measured separately. Roots are body-centered; placement code must account for the complete footprint. Printed symbols stay inside their panel rings. NAND/NOR/XNOR inversion circles use the approved larger size, and XOR/XNOR inputs stop at the extra curve.

These are visual assets. `Assets/Models/Prefabs/` contains ready material-mapped art prefabs without gameplay scripts or colliders. Existing gameplay gate prefabs and `GatePorts` still use their earlier one-cell layout; integrating the new footprint and pin arrangement into gameplay is subsequent work. Do not scale a 2×2 model down to match those existing one-cell prefabs.

## Materials and LED glow

The FBX importer remaps model material slots to shared native URP Lit materials in `Assets/Models/Materials/`. Gate and source textures use point filtering, no mipmaps, and uncompressed sRGB color. Wires and source pins use muted blue for off and green for on. Constant Signal has separate 0/1 variants; Clock has a square-wave symbol and enabled-state dot.

`SS_YellowLED.mat` enables HDR emission. `SS_FrostedLED.mat` uses a rough transparent surface with opacity 0.4; it approximates the Blender frost without a screen-space refraction shader. Emission makes the LED surface bright; a surrounding glow requires an HDR camera with post-processing enabled and a Volume containing Bloom. Emission alone does not create a realtime local light.

Open `Assets/Scenes/ModelArtReview.unity` for the complete Unity gallery and the Bloom setup. This review scene is excluded from game build settings. Its `ModelReviewBloom.asset` profile belongs to the review scene; integrating glow into a gameplay camera should use that scene's volume and camera settings rather than adding a global volume from an individual LED prefab.

## Export and verify

From the repository root, run Blender with `--background --factory-startup --python art/export_models.py`. The source models contain packed textures; exported Unity textures are maintained under `Assets/Models/Textures/`. Open Unity **6000.3.8f1**, then use **SiliconSandbox → Art → Import and verify approved models**. The editor tool remaps materials, refreshes the art prefabs, checks imported body dimensions/pin centers/counts/diameters, and rebuilds the review scene. Commit the Unity-generated `.meta` files for every asset and asset folder.

`art/review/` includes approved Blender renders, actual Unity renders, and the Unity geometry report. The EditMode model tests verify imported geometry/materials and the absence of `.blend`/inventory-image files inside Assets. The PlayMode art test loads the review scene and checks supported materials and console errors. The existing native simulation tests are intentionally skipped on macOS/Linux because the team's shipped native plugin is Windows-only; these models do not change that limitation.
