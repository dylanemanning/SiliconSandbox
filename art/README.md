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

The FBX importer remaps model material slots to shared native URP Lit materials in `Assets/Models/Materials/`. Solid Blender palette values are converted from linear to sRGB for Unity color properties. The procedural PNGs store raw linear palette values, so their importer disables sRGB decoding; they use point filtering, no mipmaps, and no compression. Wires and source pins use muted blue for off and green for on. Constant Signal has separate 0/1 variants; Clock has a square-wave symbol and enabled-state dot.

The review profile uses ACES tone mapping, exposure +0.4155, saturation -25, and contrast -8, together with ambient lighting and soft key-light shadows. The latest gameplay-preview adjustment reduces exposure by 0.2345 stops (15% less light before tone mapping) and ambient/fill contributions by 20% to darken faces opposite the key light. This gives the blue/green wires and gray blocks the softer, pastel presentation requested after the first Unity review. These settings approximate the approved Blender AgX studio render; they are not a pixel-identical reproduction of Cycles. Gameplay cameras need a compatible lighting/volume setup to reproduce this presentation.

Unity symbol textures use a near-black neutral background calibrated to the Blender reference; white symbol and boundary-ring texels are preserved. Blue/green wire and source-pin material colors are reduced to 75% of the original linear palette to match the darker clock pins in the approved Blender image. These Unity presentation adjustments do not modify the authored Blender source palette.

Non-LED model surfaces use zero smoothness with specular highlights and environment reflections disabled for a diffuse, matte finish. The LED lens materials retain their original smoothness and reflections.

`SS_YellowLED.mat` enables HDR emission. `SS_FrostedLED.mat` uses a rough transparent surface with opacity 0.4; it approximates the Blender frost without a screen-space refraction shader. Emission makes the LED surface bright; a surrounding glow requires an HDR camera with post-processing enabled and a Volume containing Bloom. Emission alone does not create a realtime local light.

Open `Assets/Scenes/ModelArtReview.unity` for the complete Unity gallery and the Bloom setup. This review scene is excluded from game build settings. Its `ModelReviewBloom.asset` profile belongs to the review scene; integrating glow into a gameplay camera should use that scene's volume and camera settings rather than adding a global volume from an individual LED prefab.

## Export and verify

From the repository root, run Blender with `--background --factory-startup --python art/export_models.py`. The source models contain packed textures; exported Unity textures are maintained under `Assets/Models/Textures/`. Open Unity **6000.3.8f1**, then use **SiliconSandbox → Art → Import and verify approved models**. The editor tool remaps materials, refreshes the art prefabs, checks imported body dimensions/pin centers/counts/diameters, and rebuilds the review scene. Commit the Unity-generated `.meta` files for every asset and asset folder.

`art/review/` is ignored local output for renders and geometry reports; generated previews and personal review notes are not versioned. The EditMode model tests verify imported geometry/materials and the absence of `.blend`/inventory-image files inside Assets. The PlayMode art test loads the review scene and checks supported materials and console errors. The existing native simulation tests are intentionally skipped on macOS/Linux because the team's shipped native plugin is Windows-only; these models do not change that limitation.

## Local development outputs

Generated screenshots, reference images, geometry reports and personal review notes under `art/review/` and `art/*-review/` stay local and are ignored by Git. Keep editable Blender sources in `art/models/`, the export script, and these source/import instructions versioned. Runtime textures and inventory/hotbar Sprites under `Assets/` are required game assets and remain versioned.
