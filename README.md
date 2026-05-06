# 🌌 N-Body Gravity Simulator & Orbital Laboratory

An interactive 3D N-body gravitational simulation developed in Unity, focusing on astrophysical visualization and real-time user-controlled orbital mechanics.

Developed for the Computer Graphics Course, this project explores Newtonian physics, procedural rendering, and interactive simulation environments.

## 🚀 Key Features
1. Dynamic N-Body Simulation

    Gravitational Interaction: Full implementation of Newton's Law of Universal Gravitation, where every object exerts force on every other object;

    Procedural Galaxy Spawn: Generate massive star systems with randomized physical properties (mass, velocity, and distance).

2. Orbital Laboratory (Manual Mode)

    Slingshot Mechanic: Drag-and-launch system to inject planets into orbit with precise initial velocity vectors;

    Celestial Hierarchy: Distinction between Stars (High-mass emission sources) and Planets (Low-mass reflective bodies);

    Static Placement: Use Shift + Click to place massive stationary bodies (Suns) to act as gravitational anchors;

    Planetary Collision System: Developed a multi-mode collision handler including 'Fragment by Mass', 'Fragment All', and 'Bounce' modes. It integrates an energy-based fragmentation model (energy of impact vs. binding energy) and implements debris spawning with randomized trajectories and rocky materials.

3. Visual & Technical Fidelity

    Procedural Appearance: Star colors shift from Red (cool/low mass) to Cyan (hot/high mass) using Mathf.InverseLerp and HDR Emission;

    Planetary Archetypes: Procedural color gradients for planets based on mass, mimicking terrestrial and gas giant profiles;

    Trail Rendering: Dynamic paths that visualize orbital trajectories and historical motion;

    Visualization Tools: Toggle gravitational force lines (**G**) and velocity heat maps on trails (**H**).

## 🛠️ Tech Stack
* Engine: Unity 2022.3+;
* Language: C#;
* Graphics: Universal Render Pipeline (URP);
* Math: Newtonian Vector Physics, Linear Interpolation (Lerp).

## 🎮 How to Use

### 🪐 Spawning Objects
- **Slingshot**: Click and drag to define the launch vector for a new body. The further you drag, the higher the initial velocity;
- **Static Placement**: Hold `Shift` + click to place an object with zero initial velocity — ideal for placing a star at the center of a system;
- **Orbital Placement** *(planet mode only)*: Hold `O` + click to automatically calculate and apply the perfect circular orbit velocity around the nearest star;
- **Switch Type**: Use the UI buttons to toggle between spawning Stars and Planets. Each type has its own mass range configurable via the UI slider;
- **Aim Mode (Direct Collision)**: Right-click an existing planet in the scene to set it as a target (marked by a pulsating ring). Left-click to launch a new planet directly toward it for a guaranteed impact[cite: 3]. Press `Escape` or `Right-click` again to exit this mode;
- **Distance HUD**: Shows cursor-to-nearest-star distance in AU with a dashed line (planet mode only). Press `T` to toggle visibility.

### 🤾🏼 Trajectory Preview
- While dragging in planet mode, a **trajectory preview line** is displayed showing the predicted path based on current gravitational forces. The preview is hidden when using orbital placement (`O`).

### 🎥 Camera
- **Free Fly**: Use `WASD` to move through the universe. Hold `Shift` for turbo speed;
- **Look Around**: Hold the **right mouse button** and move the mouse to rotate the camera;
- **Focus**: Press `F` to smoothly travel to the last spawned object;
- **Follow Mode**: `Middle Mouse Button` (Mouse3) on any star or planet to follow it in third-person. Right-click to orbit around it. Press `Middle Mouse Button` again to return to free fly;
- **Camera Management**: Press `C` to cycle through **CameraFly**, **DirectorCamera** (follows dramatic events), **BarycentreCamera** (orbits center of mass), and **TwoBodyCamera** (frames massive pairs).

### ⚙️ Simulation Control
- **Pause / Resume**: Press `Space` to pause and resume the simulation;
- **Rewind**: Hold `J` to step backward through the last 30 seconds of simulation history;
- **Fast-Forward**: Hold `L` to step forward through recorded history;
- **Timeline Scrubber**: Drag the UI slider to jump to any point in the recorded history;
- **Rewrite the Future**: Resuming from a past point discards all future history — the simulation continues from that moment as if nothing else had happened;
- **Collision Settings**: Use the Settings Panel to toggle between collision modes. In **Fragment by Mass** mode, a real-time tooltip [!] analyzes mass ratios to predict if a pair will fragment or bounce.
