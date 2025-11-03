# Earthquake Debris Editor Tools

**Location:** `Assets/ARSafe_ModularSystem/Scripts/Editor/EarthquakeDebrisEditorTools.cs`  
**Menu Path:** `ARSafe → Earthquake Debris`

## Overview

Unified editor toolset for creating and configuring earthquake debris particle systems. Consolidates all debris-related functionality into a single, organized menu structure.

## Menu Items

### 1. Complete Setup (Recommended) ⭐
**Path:** `ARSafe → Earthquake Debris → Complete Setup (Recommended)`

**What it does:**
- Generates 5 procedural rock/concrete chunk meshes
- Creates concrete material with realistic properties
- Creates configured particle system GameObject
- Auto-assigns all assets to the particle system

**Result:** Fully configured debris system ready to use immediately.

**Outputs:**
- `Assets/ARSafe_ModularSystem/Models/GeneratedDebris/DebrisChunk_1-5.asset` (meshes)
- `Assets/ARSafe_ModularSystem/Materials/ConcreteDébris.mat` (material)
- `EarthquakeDebris` GameObject in scene (particle system)

---

### 2. Generate Meshes Only
**Path:** `ARSafe → Earthquake Debris → Generate Meshes Only`

**What it does:**
- Generates 5 unique debris chunk meshes using deformed icosphere algorithm
- Saves meshes as `.asset` files

**Use when:** You only need mesh assets or want to regenerate with different seeds.

**Outputs:**
- `Assets/ARSafe_ModularSystem/Models/GeneratedDebris/DebrisChunk_1.asset`
- `Assets/ARSafe_ModularSystem/Models/GeneratedDebris/DebrisChunk_2.asset`
- `Assets/ARSafe_ModularSystem/Models/GeneratedDebris/DebrisChunk_3.asset`
- `Assets/ARSafe_ModularSystem/Models/GeneratedDebris/DebrisChunk_4.asset`
- `Assets/ARSafe_ModularSystem/Models/GeneratedDebris/DebrisChunk_5.asset`

---

### 3. Create Material Only
**Path:** `ARSafe → Earthquake Debris → Create Material Only`

**What it does:**
- Creates URP/Lit material with concrete-like properties
- Configures color (grey-brown), smoothness (0.2), and metallic (0.0)

**Use when:** Material was deleted or you want a fresh material to customize.

**Outputs:**
- `Assets/ARSafe_ModularSystem/Materials/ConcreteDébris.mat`

**Properties:**
- **Color:** Grey-brown concrete (#736B61)
- **Smoothness:** 0.2 (rough surface)
- **Metallic:** 0.0 (non-metallic)
- **Shader:** Universal Render Pipeline/Lit

---

### 4. Create Particle System Only
**Path:** `ARSafe → Earthquake Debris → Create Particle System Only`

**What it does:**
- Creates `EarthquakeDebris` GameObject at `(0, 3, 0)`
- Adds configured `ParticleSystem` component
- Adds `EarthquakeDebrisController` script
- Configures realistic debris falling behavior

**Use when:** You have meshes/materials and just need the particle system setup.

**Output:** `EarthquakeDebris` GameObject in scene

**Manual Assignment Required:**
1. Select `EarthquakeDebris` GameObject
2. Find `ParticleSystemRenderer` component
3. Drag debris meshes into **Mesh** field
4. Drag concrete material into **Material** field

---

## Particle System Configuration

The generated particle system includes:

### Main Module
- **Duration:** 20 seconds (non-looping)
- **Start Lifetime:** 1.5-3 seconds
- **Start Speed:** 0.5-2 m/s
- **Start Size:** 0.1-0.4m (3D randomized)
- **Start Rotation:** Fully randomized 3D rotation
- **Gravity Modifier:** 1.5-2.5 (heavier fall)
- **Simulation Space:** World (survives parent movement)
- **Max Particles:** 150

### Shape Module
- **Type:** Box emitter
- **Size:** 4m × 0.2m × 4m (ceiling area)

### Velocity Over Lifetime
- **Horizontal Drift:** ±0.5 m/s (X/Z axes)
- **Realistic tumbling motion**

### Rotation Over Lifetime
- **Angular Velocity:** ±180°/s on all axes
- **Natural rock tumbling**

### Collision Module
- **Type:** World collision (3D)
- **Dampen:** 0.6 (60% energy loss)
- **Bounce:** 0.3 (moderate bounce)
- **Lifetime Loss:** 0.2 (20% lifetime lost per collision)
- **Layer Mask:** Default layer

### Renderer
- **Mode:** Mesh particles
- **Alignment:** World space
- **Shadows:** Cast + Receive enabled

### Color Gradient
- **Start:** Light concrete (grey-beige)
- **Mid:** Medium concrete (grey-brown)
- **End:** Dark dust (brown)

---

## EarthquakeDebrisController Component

Automatically added to the debris GameObject. Controls emission synchronized with earthquake scenarios.

**Key Settings:**
- **onlyDuringEarthquake:** `true` (spawns only during earthquake disasters)
- **startDelay:** 1 second (delay before debris starts falling)
- **duration:** 15 seconds (how long debris continues)
- **initialEmissionRate:** 5 particles/second
- **peakEmissionRate:** 20 particles/second
- **rampUpTime:** 5 seconds (time to reach peak intensity)

See `EarthquakeDebrisController.cs` documentation for full settings.

---

## Workflow Examples

### Quick Start (Recommended)
```
1. ARSafe → Earthquake Debris → Complete Setup (Recommended)
2. Position EarthquakeDebris above target ceiling area
3. Enter Play Mode
4. Start earthquake scenario
5. Watch debris fall!
```

### Custom Setup
```
1. ARSafe → Earthquake Debris → Generate Meshes Only
2. ARSafe → Earthquake Debris → Create Material Only
3. Customize material in Inspector (textures, colors, etc.)
4. ARSafe → Earthquake Debris → Create Particle System Only
5. Manually assign meshes + material to ParticleSystemRenderer
6. Adjust EarthquakeDebrisController settings
```

### Regenerate Meshes Only
```
1. ARSafe → Earthquake Debris → Generate Meshes Only
2. Meshes overwrite existing ones
3. Particle systems automatically update (references preserved)
```

---

## Technical Details

### Mesh Generation Algorithm
- **Base Shape:** Icosphere (12 vertices, 20 faces)
- **Deformation:** Perlin noise-based radius variation
- **Randomization:** Deterministic seeds for reproducible results
- **Poly Count:** Low poly (~20-40 triangles per chunk)

### File Paths (Constants)
```csharp
DEBRIS_FOLDER = "Assets/ARSafe_ModularSystem/Models/GeneratedDebris"
MATERIAL_PATH = "Assets/ARSafe_ModularSystem/Materials/ConcreteDébris.mat"
```

### Dependencies
- **Namespace:** `ARSafe.ModularEditor`
- **Required Components:** `ParticleSystem`, `ParticleSystemRenderer`, `EarthquakeDebrisController`
- **Unity APIs:** `UnityEditor`, `AssetDatabase`, `Undo`

---

## Troubleshooting

### "No meshes assigned" warning
**Solution:** Run `Complete Setup` or manually assign meshes to `ParticleSystemRenderer > Mesh`

### "No material assigned" warning
**Solution:** Run `Complete Setup` or manually assign material to `ParticleSystemRenderer > Material`

### Debris not spawning during earthquake
**Solution:** 
1. Check `EarthquakeDebrisController.onlyDuringEarthquake` is `true`
2. Verify `DisasterTypeManager.SelectedDisasterType` is set to Earthquake
3. Ensure `EarthquakeScenarioManager` is active in scene

### Debris falls through floor
**Solution:**
1. Check `ParticleSystem > Collision` module is enabled
2. Verify collision layer mask includes floor objects
3. Ensure floor has collider component

### Debris spawns in wrong location
**Solution:**
1. Position `EarthquakeDebris` GameObject above target ceiling area
2. Adjust `ParticleSystem > Shape` module scale for spawn area size
3. Check `simulationSpace` is set to `World` (not `Local`)

---

## Migration Notes

### Replaced Old Tools
This unified tool **replaces and consolidates:**
- ❌ `DebrisMeshGenerator.cs` (deleted)
  - `ARSafe/Generate Debris Meshes`
  - `ARSafe/Create Concrete Material`
  - `ARSafe/Quick Setup: Complete Debris System`
- ❌ `EarthquakeDebrisSetup.cs` (deleted)
  - `ARSafe/Create Earthquake Debris System`
  - `ARSafe/Create Ceiling Debris (Mesh Required)`

### New Menu Structure
```
ARSafe/
└── Earthquake Debris/
    ├── Complete Setup (Recommended)        [Priority 100]
    ├── Generate Meshes Only               [Priority 101]
    ├── Create Material Only               [Priority 102]
    └── Create Particle System Only        [Priority 103]
```

**Benefits:**
- ✅ Single organized submenu instead of scattered top-level items
- ✅ Clear recommended workflow (Complete Setup)
- ✅ Consistent naming and priority ordering
- ✅ Reduced code duplication
- ✅ Easier maintenance and testing

---

## See Also
- `EarthquakeDebrisController.cs` - Runtime debris emission controller
- `EarthquakeScenarioManager.cs` - Earthquake scenario orchestration
- `DisasterTypeManager.cs` - Disaster type selection system
- `.github/CONTEXT_MEMORY.md` - Section 3.23 (Earthquake Scenario System)
