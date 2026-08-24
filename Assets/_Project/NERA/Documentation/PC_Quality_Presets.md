# NERA PC Quality Presets

Updated: 2026-08-25
Target: Standalone PC, URP 17 / Unity 6000.0

## Preset baseline

These are conservative starting points for profiling, not final hardware
requirements.

| Setting | Low | Medium | High |
|---|---:|---:|---:|
| URP render scale | 0.80 (FSR) | 0.90 (FSR) | 1.00 |
| MSAA | Off | 2x | 4x |
| Main shadow atlas | 1024 | 2048 | 2048 |
| Shadow distance | 25 m | 40 m | 60 m |
| Cascades | 1 | 2 | 4 |
| Additional-light shadows | Off | Off | On |
| Soft shadows | Off | Low | High |
| Post-processing | Off | Authored state | Authored state |
| Particle emission/max count | 50% | 75% | 100% |
| Texture mipmap limit | Half resolution | Full resolution | Full resolution |
| LOD bias | 0.7 | 1.0 | 1.5 |

Standalone defaults to High. Mobile remains a separate quality level excluded
from Standalone.

The `NERA -> Build -> Windows x64` command additionally embeds the
`NERA_WINDOWS_HIGH_100_FPS` build define. On player startup it explicitly
selects High, disables VSync and sets `Application.targetFrameRate = 100`.
This keeps the menu build on the intended preset even if the active Editor
quality level was Low or Medium before building.

The split follows Unity's URP quality guidance: shadow support and distance,
additional lights, MSAA, LOD bias, particle budget and LUT size are the primary
scalers. Render scale was added as a PC fallback because it has a predictable
GPU impact and does not affect simulation.

References:

- [Unity URP recommended quality presets](https://docs.unity3d.com/6000.0/Documentation/Manual/urp/birp-onboarding/quality-presets.html)
- [Unity PC and console graphics optimization](https://unity.com/how-to/performance-optimization-high-end-graphics)
- [Unity GPU optimization guidance](https://unity.com/how-to/gpu-optimization)

## Runtime rules

`PCQualityRuntimeController` applies only visual settings:

- Low disables post-processing on cameras where it was authored;
- Medium and High restore the authored camera state;
- scene-owned particle systems receive the preset density multiplier;
- `SetQualityLevel("Low" | "Medium" | "High")` is the supported entry point
  for a future settings menu.

The controller is a static scene-load handler. It does not create a persistent
GameObject or use `DontDestroyOnLoad`.

Presets must never change enemy counts, AI intervals, damage, physics, scan
timers, research timers, or other gameplay logic.

## LOD authoring policy

All PC presets keep `maximumLODLevel = 0`. This is intentional: Low must not
globally discard LOD0, because close-up silhouettes and authored transitions
would change unexpectedly.

Create LOD0/LOD1 first for:

- large station or expedition objects visible across long distances;
- repeated secondary props whose combined vertex cost is meaningful;
- animated or VFX-heavy objects only after profiler evidence shows a benefit.

Do not add LODGroups to small one-off props merely to satisfy a blanket rule.
Use two authored levels plus culling first; add more levels only when screen
coverage and profiling justify them. Verify transitions in motion and keep
cross-fade enabled.

## Profiling gate

Текущий Editor baseline после runtime-оптимизации находится в
`Runtime_Performance_Baseline_2026-08-24.md`. Он подтверждает снижение
`BehaviourUpdate` и GC, но не заменяет player-build GPU baseline. В частности,
Editor Render Thread показал высокую вариативность при неизменных draw calls.

Before changing these values globally:

1. Profile a standalone Development Build, not only the Editor.
2. Capture Station, Expedition combat and the heaviest terminal/VFX view.
3. Record CPU frame time, GPU frame time, batches, triangles, memory and
   1% low frame time at each preset.
4. Adjust one costly family at a time. Prefer shadows/render scale before
   reducing gameplay-readable effects.
