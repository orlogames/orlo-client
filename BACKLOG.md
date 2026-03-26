# Orlo Client — Backlog

Tracked future work, roadmap items, and ideas discussed during development.

## Microparticle System

### Done
- [x] Compute shader GPU particle simulation (scatter, converge, solidify, dissolve phases)
- [x] Mesh surface sampler (area-weighted point distribution across any mesh)
- [x] Billboard particle rendering via DrawMeshInstancedIndirect
- [x] Custom shader with additive glow and soft circular falloff
- [x] Integration with ProceduralEntityFactory (player spawn assembly effect)
- [x] Aether being mode (permanent particle shimmer, never fully solidifies)
- [x] Mesh crossfade (transparent fade-in during solidify, fade-out during dissolve)

### Next — Option B: Full Compute Shader Pipeline
- [ ] Replace DrawMeshInstancedIndirect with custom compute-driven renderer for more control
- [ ] Per-particle mesh instance rendering (tiny mesh fragments instead of quads)
- [ ] Particle-to-particle magnetic attraction for organic clumping during assembly
- [ ] Server-authoritative assembly state (server tells client when assembly is complete)
- [ ] Sync particle phase across clients for multiplayer (other players see your assembly)

### Next — Option C: Hybrid VFX Graph + Compute
- [ ] Evaluate adding VFX Graph package to project (requires URP or HDRP migration assessment)
- [ ] GraphicsBuffer bridge: compute shader writes positions, VFX Graph reads for rendering
- [ ] VFX Graph for secondary effects (sparks, energy trails, glow halos during assembly)
- [ ] Point cache baking for complex meshes (pre-bake surface samples for perf)

### Enhancements
- [ ] LOD particle count — fewer particles at distance, full count close-up
- [ ] Color sampling from mesh vertex colors (particles inherit the color of their target surface)
- [ ] Aether being emotional states — turbulence driven by combat/social state
- [ ] Race-specific particle colors (Korathi = orange/molten, Sylvari = green/organic, Ashborn = red/void)
- [ ] Sound design integration — whoosh/hum audio that scales with particle convergence speed
- [ ] Particle trails during convergence (short trail behind each particle as it spirals in)
- [ ] Ground-flow dissolution (particles flow along terrain surface during dissolve, T-1000 style)
- [ ] Equipment assembly — weapons/armor assemble separately after body, with different particle color
- [ ] Death dissolution — on player/NPC death, mesh dissolves into particles that scatter and fade
- [ ] Teleportation effect — dissolve at origin, particles stream to destination, reassemble

## Rendering Pipeline

### HDRP Migration
- [ ] Evaluate HDRP migration for Arc Raiders-quality rendering
- [ ] Volumetric lighting and god rays
- [ ] Screen-space reflections
- [ ] Subsurface scattering for creature skin/organic materials
- [ ] Temporal anti-aliasing
- [ ] Physically-based bloom and color grading
- [ ] Impact on existing procedural material system (Standard shader → HDRP/Lit)

### Shader System
- [ ] Custom PBR shader for procedural characters (skin, cloth, metal subtypes)
- [ ] Triplanar mapping for terrain (no UV seams on procedural meshes)
- [ ] Emission shader for bioluminescent creatures and energy weapons
- [ ] Reinforcement glow shader (+1 to +15 visual progression)
- [ ] Convergence energy shader (purple distortion field for Nexus Points)
- [ ] Wind sway shader for vegetation (GPU vertex animation)

## Asset Pipeline Integration

### AI-Generated Assets in Client
- [ ] GLTFast package for runtime GLB loading (replace procedural primitives with AI-generated meshes)
- [ ] Addressable asset system for on-demand asset streaming
- [ ] Fallback to procedural primitives when AI assets haven't loaded
- [ ] Asset quality settings (use AI assets on high, procedural on low)
- [ ] CDN integration — download asset bundles from Hetzner Object Storage via Cloudflare

### Procedural → AI Asset Transition
- [ ] Hybrid system: procedural skeleton + AI-generated mesh skins
- [ ] AI mesh LOD chain loading (LOD0 from AI, LOD2 from procedural as fallback)
- [ ] Texture streaming for AI-generated PBR textures
- [ ] Animation retargeting from Meshy auto-rig to procedural bone structure

## World Systems

### Terrain Manipulation Device (TMD)
- [ ] Voxel-enhanced heightmap deformation
- [ ] Visual particle effects when TMD modifies terrain
- [ ] Underground cavity rendering
- [ ] Persistent deformation sync with server

### Seamless Transitions
- [ ] Planet surface → atmosphere transition (no loading screens)
- [ ] Atmosphere → orbit transition
- [ ] Orbit → deep space transition
- [ ] Ship interior portalization (walkable interiors without loading)

### Weather & Climate
- [ ] Convergence storms (reality-warping visual distortion)
- [ ] Volcanic activity particles and heat shimmer
- [ ] Seasonal visual changes (vegetation color shifts, snow coverage)
- [ ] Planetary ring shadows

## Character & Combat

### Awakened Visual Effects
- [ ] Chronist (Human) — time distortion ripples, clock-like particle patterns
- [ ] Rootweaver (Sylvari) — organic vine growth tendrils, leaf particles
- [ ] Forgeborn (Korathi) — molten metal particles, forge-fire glow
- [ ] Voidwalker (Ashborn) — permanent microparticle aether form, void energy crackling

### Combat VFX
- [ ] Momentum-based impact particles (harder hit = more particles)
- [ ] Energy weapon beam rendering
- [ ] Shield generator visual bubble
- [ ] Martial arts style-specific VFX (Wind Step = air trails, Ghost Form = translucency)

## Performance

### Optimization Targets
- [ ] Particle system GPU profiling and budget (max particles per frame across all effects)
- [ ] Compute shader occupancy optimization
- [ ] Instanced rendering batching for multiple simultaneous assemblies
- [ ] Object pool integration for MicroparticleSystem buffers (reuse GPU buffers)
- [ ] Async GPU readback for server-side assembly state validation
