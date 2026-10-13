# Model import verification

Verified on macOS with Unity **6000.3.8f1** on 12 October 2026. Blender sources were exported with Blender **5.0.1**. These checks concern model assets and their review scene; gameplay prefab integration is separate.

- All 18 FBXs imported successfully without Blender assets under `Assets`.
- Imported body dimensions, +Y up/+Z output axes, pin counts, exact face centers, 0.33 pin diameter, 0.3 straight-wire diameter, and the larger gates' left output-face position passed the independent Unity checks in `ModelArtBuilder.VerifyAll`.
- All model materials are remapped to shared native URP Lit assets. Yellow LED emission is enabled with HDR intensity; off lens uses a rough transparent material. The review scene includes an HDR camera, post-processing, and Bloom.
- Unity-generated metadata accompanies every runtime asset and folder. Removed legacy Blender GUIDs had no references in scenes, prefabs, materials, scripts, or asset records.
- EditMode: **32 passed, 20 skipped, 0 failed**, including the regression check for linear artist palette import. The skips are the existing Windows-native-plugin fixture on macOS.
- PlayMode: **19 passed, 2 skipped, 0 failed**, including the art scene's material/console smoke test. The two existing pause-menu settings tests are skipped because that feature is not implemented. Shader support is checked on a real graphics device; headless CI still checks asset/material identities and console errors.
- Actual Unity captures are `unity-model-gallery.png` and `unity-led-materials.png`; the other numbered images are the approved Blender review renders.
- Appearance follow-up: corrected solid material color conversion and procedural linear texture decoding; group Blender sources mark the packed procedural textures as Non-Color data. The review scene now uses soft shadows, ACES tone mapping, exposure +0.65, saturation -25, and contrast -8. `unity-wires-materials.png` shows the softer pastel presentation requested after the initial Unity comparison. Geometry and pin locations are unchanged.
- The local test run does not validate Windows-native logic; the repository's Windows native-plugin CI job covers its existing binary check.

CI status is recorded on PR #87 for the pushed commit; this file records local verification rather than asserting future CI results.
