# EasyToDoList Pixel-Art MVP Assets

Generated on 2026-11-11 with the built-in OpenAI image generation tool for the approved **Guild Quest Board** direction.

The user-provided stock image was used only as a mood/pixel-art reference. It is not included in this repository and must not ship with the application.

## Files

```text
backgrounds/guild-valley.png
ambient/ambient-atlas.png
ambient/clouds/*.png
ambient/flag/flag-00.png ... flag-05.png
ambient/grass/grass-00.png ... grass-05.png
ambient/leaves/leaf-00.png ... leaf-07.png
mascots/knight/knight-spritesheet.png
mascots/knight/knight-spritesheet-normalized.png
mascots/knight/frames/{idle,focus,celebrate,rest}/*.png
previews/*.gif
```

## Runtime animation contract

| Animation | Frames | Playback | Behavior |
|---|---:|---:|---|
| Knight Idle | 6 | 6 fps | Loop |
| Knight Focus | 6 | 6 fps | Loop while focus session runs |
| Knight Celebrate | 6 | 10 fps | Play once, then return to Idle |
| Knight Rest | 6 | 5 fps | Loop during short/long breaks |
| Guild flag | 6 | 6 fps | Loop |
| Grass | 6 | 4 fps | Loop with a random initial offset |
| Leaves | 8 orientations | 2–4 particles | WPF controls falling translation; choose rotation sprite by progress |
| Clouds | 3 static sprites | 8–16 px/s | WPF translates horizontally; wrap outside viewport |

Runtime mascot and flag/grass frames are normalized to `320 × 320` PNGs with alpha. Mascot pivot is `(160, 298)`, flag pivot is `(140, 290)`, and grass pivot is `(180, 270)`. Measured horizontal drift is below `1 px` and vertical drift is `0 px` in every loop. Render with nearest-neighbor scaling.

`knight-spritesheet.png` is the untouched generated source. Runtime code must use the normalized individual frames or `knight-spritesheet-normalized.png`, whose grid is `6 × 4` with `320 × 320` cells.

For Reduced Motion, stop cloud/leaves and use frame `00` for mascot, flag and grass. Gameplay feedback must remain visible through text and color.

## Generation prompts

### Background

Create a clean 16:9 medieval valley in crisp handcrafted 16-bit pixel art: a weathered stone watchtower on the left, a large tree, winding path, flower meadow, distant blue mountains and broad sky. Keep the center/right uncluttered for application panels. Environment only; no people, animals, board, UI, text, logo or watermark.

### Ambient atlas

Create a transparent pixel-art atlas matching the Guild Quest Board palette: three cloud clusters, six sequential frames of a fixed-pole red/gold guild flag waving gently, six sequential frames of a grass tuft swaying with a fixed baseline, and eight small leaves at successive rotation angles. Keep hard pixels, equal cells, gutters and no overlap, text or watermark.

### Knight mascot

Create a transparent exact `4 × 6` sprite sheet for the same brown-haired young knight with silver armor, red scarf/cape and blue/gold shield. Rows are Idle, Focus while reading, Celebrate and Rest. Preserve identity, proportions, costume, light direction, scale and feet baseline across all 24 frames; no background, labels, shadow, text or watermark.

## Production caveat

These generated assets are sufficient for prototyping and MVP integration. The runtime frames were re-sliced and pivot-normalized after the generated atlases showed progressive left drift. Before a public art-quality release, inspect frame-to-frame silhouette consistency and manually clean any colored edge pixels. Do not generate additional mascot sets until the starter knight is validated inside the real WPF layout.
