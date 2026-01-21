# 3D Object Viewer

A WPF desktop application for creating, manipulating, and viewing 3D objects with physics simulation.

## Solution Structure

The solution is organized into two sibling projects:

```mermaid
graph TB
    subgraph Root["repos/"]
        SLN[3DObjectViewer.sln]
        
        subgraph App["3DObjectViewer/"]
            V[Views/]
            VM[ViewModels/]
            R[Rendering/]
            P[Physics/]
            S[Services/]
            D[Design/]
        end
        
        subgraph Core["3DObjectViewer.Core/"]
            I[Infrastructure/]
            CP[Physics/]
            RA[Rendering/Abstractions/]
            M[Models/]
            H[Helpers/]
        end
    end
    
    SLN --> App
    SLN --> Core
    App --> Core
```

### 3DObjectViewer.Core (Platform Library)

A reusable class library containing platform-independent core functionality:

| Folder | Description |
|--------|-------------|
| **Infrastructure/** | MVVM base classes (`ViewModelBase`, `RelayCommand`) and value converters |
| **Physics/** | Physics engine with SIMD-optimized collision detection (`PhysicsEngine`, `RigidBody`, `SimdMath`) |
| **Rendering/Abstractions/** | Renderer interfaces and contracts (`IRenderer`, `ISceneObject`, `IRendererFactory`) |
| **Models/** | Shared data models (`LightSource`, `ObjectColor`) |
| **Helpers/** | Utility classes (`DragPlane3D`) |

### 3DObjectViewer (Application)

The WPF application containing UI and renderer implementations:

| Folder | Description |
|--------|-------------|
| **Views/** | XAML controls and user interfaces |
| **ViewModels/** | Application-specific view models (`MainViewModel`, `ObjectsViewModel`, etc.) |
| **Rendering/HelixWpf/** | HelixToolkit.Wpf renderer implementation |
| **Rendering/Services/** | Lighting and shadow services |
| **Services/** | Scene orchestration (`SceneService`) |
| **Physics/** | Visual-specific physics helpers (`PhysicsHelper`) |

## Features

- **Object Creation**: Add 3D primitives (cubes, spheres, cylinders, cones, toruses)
- **Physics Simulation**: SIMD-accelerated gravity, collisions, and object dynamics
- **Object Manipulation**: Select, drag, rotate, and scale objects
- **Dynamic Lighting**: Multiple configurable light sources with presets
- **Real-time Shadows**: Objects cast shadows onto the ground plane
- **Performance Monitoring**: Live FPS, memory, and triangle count display

## Architecture

### Design Patterns

```mermaid
flowchart TB
    subgraph MVVM["MVVM Pattern"]
        View[Views<br/>XAML]
        ViewModel[ViewModels<br/>Logic]
        Model[Models<br/>Data]
        View <-->|Data Binding| ViewModel
        ViewModel -->|Reads/Writes| Model
    end
    
    subgraph Strategy["Strategy Pattern"]
        IR[IRenderer Interface]
        Helix[HelixWpfRenderer]
        IR --> Helix
    end
    
    subgraph Facade["Facade Pattern"]
        SS[SceneService]
        LS[LightingService]
        ShadowS[ShadowService]
        SS --> LS
        SS --> ShadowS
    end
    
    subgraph Observer["Observer Pattern"]
        Events[Events]
        Handlers[Event Handlers]
        Events -->|Notify| Handlers
    end
```

- **MVVM**: Clean separation of UI (Views), logic (ViewModels), and data (Models)
- **Strategy Pattern**: Swappable renderer implementations via `IRenderer`
- **Facade Pattern**: `SceneService` coordinates lighting and shadow services
- **Observer Pattern**: Event-driven communication between components

### Key Dependencies

```mermaid
graph LR
    App[3DObjectViewer]
    Core[3DObjectViewer.Core]
    Helix[HelixToolkit.Wpf]
    Numerics[System.Numerics]
    
    App --> Core
    App --> Helix
    Core --> Numerics
```

- **HelixToolkit.Wpf**: 3D rendering and viewport controls
- **System.Numerics**: SIMD-accelerated vector math

## Requirements

- .NET 10.0 (Windows)
- Windows OS with WPF support

## Building

```bash
dotnet restore
dotnet build 3DObjectViewer.sln
```

## Running

```bash
dotnet run --project 3DObjectViewer/3DObjectViewer.csproj
```

## Mouse Controls

```mermaid
flowchart LR
    Click[Click] --> Select[Select object]
    CtrlDrag[Ctrl + Drag] --> Move[Move object]
    LeftDrag[Left-drag] --> Rotate[Rotate view]
    RightDrag[Right-drag] --> Pan[Pan view]
    Scroll[Scroll wheel] --> Zoom[Zoom]
    Esc[Escape] --> Deselect[Deselect]
```

| Action | Control |
|--------|---------|
| Select object | Click |
| Move object | Ctrl + Drag |
| Rotate view | Left-drag |
| Pan view | Right-drag |
| Zoom | Scroll wheel |
| Deselect | Escape |

## License

See LICENSE file for details.
