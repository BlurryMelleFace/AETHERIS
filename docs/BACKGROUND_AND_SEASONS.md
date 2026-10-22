# Editable Forest Backdrop

## Architecture
The backdrop is now **native Godot content**, not a flattened image.

`scenes/main.tscn` contains the dedicated `ForestBackdrop` node, driven by `scripts/ForestBackdrop.cs`. It draws:
- a separate `Sky/SkyBackdrop` supplies the animated day/night sky gradient
- jagged mountains with separate snow caps
- two independent conifer forest depth layers
- subtle drifting mist

All components use deterministic geometry and editable exported parameters in the Godot Inspector. Select **ForestBackdrop** to adjust landscape palette, mountain horizon, parallax strengths, dimensions, and fog speed. Select **Sky/SkyBackdrop** for day, night and twilight sky palettes.

## Shared environment
`DayNightCycle.cs` provides a 120-second shared clock and `CanvasModulate` for the world and landscape. The sky uses its own canvas so sun, moon and stars stay luminous. See [DAY_NIGHT_CYCLE.md](DAY_NIGHT_CYCLE.md) for timing and checks. Press **T** to cycle seasonal world color themes. This changes tint now; future seasons should adjust snow coverage, foliage and biome-specific palettes in a central EnvironmentState resource.

## Limitations
The first geometric mountain pass is intentionally a prototype. It does **not** reproduce the detailed generated illustration, and tree silhouettes are still procedural rather than authored pixel-art layers. No external PNG is required. Parallax geometry is currently redrawn with the camera and should be profiled before scaling the game. Adjusting exported values while paused in the scene Inspector requires reopening/running the scene for redraw to preview accurately.

## Test
```powershell
git fetch origin
git switch feature/32px-artwork-integration
git pull
dotnet build
```
Open Godot and press F5. Adjust ForestBackdrop properties from the Inspector; press T in-game to cycle seasonal tint.
