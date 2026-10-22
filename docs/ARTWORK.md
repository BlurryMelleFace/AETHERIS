# AETHERIS — Artwork Integration I

This first pass replaces the simple code-drawn foreground trees and ground colors with reusable **pixel-grid artwork**.

## Assets

- `assets/tiles/grass_top_32.svg` — 32×32 ground / grass edge
- `assets/tiles/dirt_32.svg` — 32×32 soil
- `assets/tiles/stone_32.svg` — 32×32 deep stone
- `assets/foliage/grass_clump_32.svg` — 32×32 foliage overlay
- `assets/foliage/pine_64x128.svg` — 64×128 layered pine sprite, based on the same 2-pixel art grid

These **SVG source assets** contain hard-edged, pixel-aligned rectangles. Godot rasterizes them into textures upon import; they are not PNG files yet. The world renderer enables nearest-neighbor filtering to preserve pixel edges.

The game still uses **24 world units per block**. Texture resolution and collision grid resolution are deliberately separate for this experiment. Do not change the block size to 32 unless intentionally migrating physics and camera composition.

## Test

1. Switch to `feature/32px-artwork-integration`.
2. Let Godot import the new SVGs.
3. Build the C# project and press F5.
4. Verify textured grass, dirt, stone, pixel pine trees and foliage.
5. Verify mine/place continues to update the drawn terrain.
6. Check Godot Output for missing-resource warnings.

## Next art tasks

Add variation and neighbor-aware auto-tiling, real parallax, texture atlases, cave-wall textures and advanced lighting. For now the visual overhaul's existing sky, hills and day/night system remain in place.

**Limitations:** this is a first artwork pass, not a finished hand-painted tileset. No Godot runtime verification was performed in the authoring environment.
