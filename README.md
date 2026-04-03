# 🌌 N-Body Gravity Simulator & Orbital Laboratory

An interactive 3D N-body gravitational simulation developed in Unity, focusing on astrophysical visualization and real-time user-controlled orbital mechanics.

Developed for the Computer Graphics Course, this project explores Newtonian physics, procedural rendering, and interactive simulation environments.

## 🚀 Key Features
1. Dynamic N-Body Simulation

    Gravitational Interaction: Full implementation of Newton's Law of Universal Gravitation, where every object exerts force on every other object.

    Procedural Galaxy Spawn: Generate massive star systems with randomized physical properties (mass, velocity, and distance).

2. Orbital Laboratory (Manual Mode)

    Slingshot Mechanic: Drag-and-launch system to inject planets into orbit with precise initial velocity vectors.

    Celestial Hierarchy: Distinction between Stars (High-mass emission sources) and Planets (Low-mass reflective bodies).

    Static Placement: Use Shift + Click to place massive stationary bodies (Suns) to act as gravitational anchors.

3. Visual & Technical Fidelity

    Procedural Appearance: Star colors shift from Red (cool/low mass) to Cyan (hot/high mass) using Mathf.InverseLerp and HDR Emission.

    Planetary Archetypes: Procedural color gradients for planets based on mass, mimicking terrestrial and gas giant profiles.

    Trail Rendering: Dynamic paths that visualize orbital trajectories and historical motion.

## 🛠️ Tech Stack
* Engine: Unity 2022.3+
* Language: C#
* Graphics: Universal Render Pipeline (URP)
* Math: Newtonian Vector Physics, Linear Interpolation (Lerp).

## 🎮 How to Use

### Spawning Objects
- **Slingshot**: Click and drag to define the launch vector for a new body. The further you drag, the higher the initial velocity.
- **Static Placement**: Hold `Shift` + click to place an object with zero initial velocity — ideal for placing a star at the center of a system.
- **Orbital Placement** *(planet mode only)*: Hold `O` + click to automatically calculate and apply the perfect circular orbit velocity around the nearest star.
- **Switch Type**: Use the UI buttons to toggle between spawning Stars and Planets. Each type has its own mass range configurable via the UI slider.

### Trajectory Preview
- While dragging in planet mode, a **trajectory preview line** is displayed showing the predicted path based on current gravitational forces. The preview is hidden when using orbital placement (`O`).

### Camera
- **Free Fly**: Use `WASD` to move through the universe. Hold `Shift` for turbo speed.
- **Look Around**: Hold the **right mouse button** and move the mouse to rotate the camera.
- **Focus**: Press `F` to smoothly travel to the last spawned object.
- **Follow Mode**: `Middle Mouse Button` on any star or planet to follow it in third-person. Right-click to orbit around it. Press `Middle Mouse Button` again to return to free fly.

### Simulation Control
- **Pause / Resume**: Press `Space` to pause and resume the simulation.
- **Rewind**: Hold `J` to step backward through the last 30 seconds of simulation history.
- **Fast-Forward**: Hold `L` to step forward through recorded history.
- **Timeline Scrubber**: Drag the UI slider to jump to any point in the recorded history.
- **Rewrite the Future**: Resuming from a past point discards all future history — the simulation continues from that moment as if nothing else had happened.
