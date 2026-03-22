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
* Stellar Spawn: Click and drag to define the launch vector for a new body.
* Static Mode: Hold Shift to place an object with zero initial velocity.
* Configuration: Use the UI Sliders to adjust mass in real-time before spawning.
* Camera: The simulation supports free-roaming exploration of the generated systems.
