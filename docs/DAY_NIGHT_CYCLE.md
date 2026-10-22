# Day and night

A full sunrise-to-sunrise loop lasts **120 seconds**: 60 seconds of daylight and 60 seconds of night. The scene starts at sunrise. The sun and moon follow opposite arcs; stars fade in at night. Warm twilight blends into full daylight or readable blue moonlight without a jump at the loop boundary.

`DayNightCycle` owns the clock and world brightness. Change `DayLengthSeconds` or `StartPhase` on that node in the Inspector. Phase 0 is sunrise, 0.25 noon, 0.5 sunset and 0.75 midnight. **T** still cycles seasonal world tints.

`Sky/SkyBackdrop` draws the sky, sun, moon and stars on canvas layer -1. This anchors them to the screen and prevents world dimming from darkening luminous sky objects. The existing mountains, clouds and forest remain on the world canvas; their gentle parallax is retained. Sky palettes are editable on `SkyBackdrop`.

## Verification

```powershell
dotnet build
# Substitute the path to your Godot .NET executable:
godot --headless --path . tests/day_night_smoke.tscn
```

Run the main scene with F5 and watch one two-minute loop:

- 0s: sunrise and warm sky; the sun climbs from the left horizon.
- 30s: noon, bright terrain and sun overhead.
- 60s: sunset; daylight fades and the moon begins rising.
- 90s: midnight, visible stars, moon overhead and readable terrain.
- 120s: the next sunrise, with no lighting flash.

Walk and jump: the sky stays anchored while the scenery retains its slow parallax. Resize the window: the sky fills it and celestial paths adapt. Mining, placement and the HUD should remain usable throughout. **R** resets the scene to sunrise.
