# 🌌 N-Body Gravity Simulator & Orbital Laboratory
### Final Project — Computer Graphics Course · 2025/2026
**Author:** Diogo Carvalho

An interactive 3D N-body gravitational simulation developed in Unity, focusing on astrophysical visualization and real-time user-controlled orbital mechanics.

Developed for the Computer Graphics Course, this project explores Newtonian physics, procedural rendering, and interactive simulation environments.

---

## ⬇️ Download

[![Windows](https://img.shields.io/badge/Download-Windows-0078D6?style=for-the-badge&logo=windows)](https://github.com/diogoccc45/NBodyRelativitySimulator/releases/download/v1.0/Build_Windows.zip)
[![macOS](https://img.shields.io/badge/Download-macOS-000000?style=for-the-badge&logo=apple)](https://github.com/diogoccc45/NBodyRelativitySimulator/releases/download/v1.0/Build_Mac.app.zip)

---

## 📦 Delivery Contents

| File | Description |
|---|---|
| `Projeto_Final_Build_up202509333.zip` | **Windows** executable — instructions below |
| `Build_Mac.app.zip` | **macOS** executable — instructions below |
| `Projeto_Final_Assets_up20250933.zip` | Full Unity project folder — open with **Unity 6** (6000.3.x) |

> **Windows:** Extract the full `Projeto_Final_Build_up202509333.zip` and run the `.exe`. The executable cannot run alone — it requires the adjacent `_Data` folder and dlls. No installation needed.

> **macOS:** Extract `Build_Mac.app.zip` to get the `.app` file. If a "developer unverified" warning appears, right-click → Open → Open anyway. Compatible with both Intel and Apple Silicon (M1/M2/M3).

> **Running in Unity:** Extract `Projeto_Final_Assets_up20250933.zip` and open the folder via Unity Hub → Add project from disk. Requires Unity 6 (6000.3.x).

---

## 🚀 Key Features

### 1. Dynamic N-Body Simulation
- **Gravitational Interaction:** Full implementation of Newton's Law of Universal Gravitation, where every object exerts force on every other object
- **Procedural Galaxy Spawn:** Generate massive star systems with randomized physical properties (mass, velocity, and distance)

### 2. Orbital Laboratory (Manual Mode)
- **Slingshot Mechanic:** Drag-and-launch system to inject planets into orbit with precise initial velocity vectors
- **Celestial Hierarchy:** Distinction between Stars (high-mass emission sources) and Planets (low-mass reflective bodies)
- **Static Placement:** Use `Shift` + Click to place massive stationary bodies (Suns) to act as gravitational anchors
- **Planetary Collision System:** Multi-mode collision handler including *Fragment by Mass*, *Fragment All*, and *Bounce* modes. Integrates an energy-based fragmentation model and implements debris spawning with randomized trajectories and rocky materials

### 3. General Relativity Scene
- **Spacetime Grid:** Deformable grid visualising spacetime curvature caused by massive bodies
- **Gravitational Waves:** Propagated in real time as a ripple ring each time a mass is placed or moved
- **Trajectory Preview:** Forward simulation in memory before launch, colour-coded by escape risk
- **Orbital Placement:** Press `O` in pause to automatically calculate and apply the correct circular orbit velocity

### 4. Visual & Technical Fidelity
- **Procedural Appearance:** Star colors shift from Red (cool/low mass) to Blue-White (hot/high mass) following the Harvard spectral sequence with HDR Emission
- **Planetary Archetypes:** Procedural color gradients for planets based on mass, mimicking rocky, super-Earth, and ice giant profiles
- **Trail Rendering:** Dynamic paths that visualize orbital trajectories and historical motion
- **Absorption Animation:** 4-phase procedural sequence — accretion spiral → plasma trail → vaporisation → bipolar flare
- **Procedural Main Menu:** Entire UI built in code — animated nebula shader (domain-warped FBM), star field with parallax, and shooting stars with light trails

---

## 🎮 Controls

### Newton Random & Orbital Laboratory

#### 🖱️ Mouse
| Action | Effect |
|---|---|
| **Left click** | Place star or planet in scene |
| **Left click + drag** | Drag a body (in play: launches it with drag velocity) |
| **Left click + drag** (empty space) | Slingshot — defines initial velocity vector |
| **Shift + left click** | Place body with zero velocity (static anchor) |
| **Right click on planet** | Enter aim mode — marks planet as target |
| **Left click** (aim mode) | Launch new planet directly toward target |
| **Right click** (empty / aim mode) | Cancel aim mode |
| **Scroll wheel** | Camera zoom |
| **Middle mouse button** | Toggle orbital ↔ free fly camera |
| **Right button + drag** (free fly) | Rotate camera |

#### ⌨️ Keyboard
| Key | Effect |
|---|---|
| **Space** | Pause / resume simulation |
| **J** *(held)* | Rewind through simulation history |
| **L** *(held)* | Fast-forward through simulation history |
| **C** | Cycle camera modes (Free Fly → Director → Barycentre → Two-Body) |
| **W / A / S / D** | Move camera (free fly mode) |
| **Shift** | Turbo camera speed (free fly mode) |
| **F** | Focus on last spawned object (free fly mode) |
| **O** | Apply perfect circular orbit velocity to planet *(pause + planet mode)* |
| **T** | Toggle distance HUD + grid + minimap |
| **G** | Toggle gravitational force lines |
| **H** | Toggle velocity heatmap on trails |
| **Escape** | Cancel aim mode |

---

### General Relativity

#### 🖱️ Mouse
| Action | Effect |
|---|---|
| **Left click + drag** | Move a mass across the spacetime grid |
| **Right click** | Toggle camera |

#### ⌨️ Keyboard
| Key | Effect |
|---|---|
| **Space** | Pause / resume simulation |
| **O** | Apply orbital velocity to selected planet *(in pause)* |
| **C** | Toggle orbital ↔ free fly camera |
| **W / A / S / D** | Move camera (free fly mode) |
| **Shift** | Turbo camera speed |

---

## 🛠️ Tech Stack

| | |
|---|---|
| **Engine** | Unity 6 (6000.3.x) |
| **Language** | C# |
| **Graphics** | Universal Render Pipeline (URP) |
| **Shaders** | Custom HLSL — `NebulaClouds_UI` (domain-warped FBM, vortex, filaments) |
| **Physics** | Newtonian N-body, O(n²) pairwise force calculation |
| **Input** | Unity Input System (new) |

---

## 🔬 Technical Highlights

- **N-body simulation** with Euler integration and O(n²) pairwise gravitational force calculation
- **Procedural HLSL shader** (`NebulaClouds_UI`) with domain-warped Fractional Brownian Motion, animated vortex and organic filaments — no external textures
- **4-phase absorption sequence** with procedural animation: accretion spiral, plasma trail, vaporization and bipolar flare
- **Collision system** with three configurable modes: fragment by mass ratio, fragment all, and elastic bounce
- **Timeline with rewind** — records simulation snapshots and allows navigation through time
- **Real-time spacetime grid** deformed by masses with gravitational waves propagated as an expanding ring
- **Trajectory preview** in the Relativity scene with forward simulation in memory before launch
- **Stellar colorimetry** based on the Harvard spectral sequence (M-type → O/B-type) as a function of mass
- **Minimap** with procedural compass rose and circular mask
- **Entire main menu generated 100% in code** — no prefabs, no external UI assets

---

## 💻 System Requirements

**Windows**
- Windows 10/11 (64-bit)
- GPU with DirectX 11 support
- 4 GB RAM recommended

**macOS**
- macOS 11 Big Sur or later
- Intel 64-bit or Apple Silicon (M1/M2/M3)
- 4 GB RAM recommended

---

> **📝 Note:** All in-code comments and documentation throughout the project scripts are written in Portuguese. I apologize for any inconvenience this may cause when reading through the source code — it was a conscious choice made during development to keep the technical notes aligned with the course language. The code itself, variable names, and method signatures are in English.
