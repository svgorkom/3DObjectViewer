# 3DObjectViewer.Core - Design Document

## Overview

**3DObjectViewer.Core** is a platform-independent class library that provides the core functionality for 3D physics simulation and MVVM infrastructure. This library is designed to be reusable and decoupled from any specific UI framework.

### Library Capabilities

| Feature | Description |
|---------|-------------|
| **Physics Simulation** | SIMD-optimized physics engine with gravity, collision detection, and response |
| **MVVM Infrastructure** | Base classes for ViewModels, commands, and value converters |
| **Renderer Abstractions** | Interfaces for implementing custom 3D renderers |
| **Shared Models** | Common data structures used across the application |

### Target Framework
- .NET 10
- C# 14.0
- Platform-independent (no WPF dependencies)

---

## Project Structure

```mermaid
graph TB
    subgraph Core["3DObjectViewer.Core/"]
        subgraph Physics["Physics/"]
            PE[PhysicsEngine.cs]
            BPE[BepuPhysicsEngine.cs]
            TBPE[ThreadedBepuPhysicsEngine.cs]
            CPE[CustomPhysicsEngine.cs]
            RB[RigidBody.cs]
            CD[CollisionDetector.cs]
            CR[CollisionResolver.cs]
            BCH[BoundaryCollisionHandler.cs]
            FI[ForceIntegrator.cs]
            SG[SpatialGrid.cs]
            PC[PhysicsConstants.cs]
            PEF[PhysicsEngineFactory.cs]
            VC[VectorConversions.cs]
            
            subgraph Abstractions["Abstractions/"]
                IPE[IPhysicsEngine.cs]
            end
            
            subgraph Bepu["Bepu/"]
                NPC[NarrowPhaseCallbacks.cs]
                PIC[PoseIntegratorCallbacks.cs]
            end
        end
        
        subgraph Infrastructure["Infrastructure/"]
            VMB[ViewModelBase.cs]
            RC[RelayCommand.cs]
            subgraph Converters["Converters/"]
                EBC[EnumToBoolConverter.cs]
            end
        end
        
        subgraph Rendering["Rendering/Abstractions/"]
            IR[IRenderer.cs]
            IRF[IRendererFactory.cs]
            ISO[ISceneObject.cs]
            RT[RendererType.cs]
        end
        
        subgraph Models["Models/"]
            LS[LightSource.cs]
            OC[ObjectColor.cs]
        end
        
        subgraph Helpers["Helpers/"]
            DP[DragPlane3D.cs]
        end
    end
```

---

## Architecture

### Layer Diagram

```mermaid
flowchart TB
    subgraph Consumer["Consumer Layer (3DObjectViewer App)"]
        App[WPF Application]
    end
    
    subgraph Core["3DObjectViewer.Core"]
        subgraph InfraLayer["Infrastructure Layer"]
            VMB[ViewModelBase]
            RC[RelayCommand]
            Conv[Converters]
        end
        
        subgraph PhysicsLayer["Physics Layer"]
            IPE[IPhysicsEngine]
            PE[PhysicsEngine]
            BPE[BepuPhysicsEngine]
            TBPE[ThreadedBepuPhysicsEngine]
        end
        
        subgraph RenderingLayer["Rendering Abstractions"]
            IR[IRenderer]
            ISO[ISceneObject]
        end
        
        subgraph ModelsLayer["Models Layer"]
            LS[LightSource]
            OC[ObjectColor]
        end
    end
    
    App --> InfraLayer
    App --> PhysicsLayer
    App --> RenderingLayer
    App --> ModelsLayer
```

---

## Physics System

### Physics Engine Hierarchy

```mermaid
classDiagram
    class IPhysicsEngine {
        <<interface>>
        +Start()
        +Stop()
        +AddBody(RigidBody body)
        +RemoveBody(int bodyId)
        +SetGravity(double gravity)
        +WakeAllBodies()
        +event BodiesUpdated
    }
    
    class PhysicsEngine {
        <<abstract>>
        #bodies: List~RigidBody~
        #gravity: double
        +Start()
        +Stop()
        +AddBody(RigidBody body)
        +RemoveBody(int bodyId)
    }
    
    class CustomPhysicsEngine {
        -collisionDetector: CollisionDetector
        -collisionResolver: CollisionResolver
        -forceIntegrator: ForceIntegrator
        -spatialGrid: SpatialGrid
        +Step(double deltaTime)
    }
    
    class BepuPhysicsEngine {
        -simulation: Simulation
        -bodyHandles: Dictionary
        +Step(double deltaTime)
    }
    
    class ThreadedBepuPhysicsEngine {
        -physicsThread: Thread
        -pendingOperations: Queue
        +Step() runs on background thread
    }
    
    IPhysicsEngine <|.. PhysicsEngine
    PhysicsEngine <|-- CustomPhysicsEngine
    PhysicsEngine <|-- BepuPhysicsEngine
    BepuPhysicsEngine <|-- ThreadedBepuPhysicsEngine
```

### Physics Pipeline

```mermaid
flowchart TB
    subgraph Input["Input Phase"]
        A1[Receive body updates]
        A2[Process pending operations]
    end
    
    subgraph Simulation["Simulation Phase"]
        B1[Apply gravity forces]
        B2[Integrate velocities]
        B3[Broad phase collision detection]
        B4[Narrow phase collision detection]
        B5[Resolve collisions]
        B6[Integrate positions]
        B7[Check boundary collisions]
    end
    
    subgraph Output["Output Phase"]
        C1[Collect body states]
        C2[Fire BodiesUpdated event]
    end
    
    A1 --> A2 --> B1 --> B2 --> B3 --> B4 --> B5 --> B6 --> B7 --> C1 --> C2
```

### RigidBody Class

```mermaid
classDiagram
    class RigidBody {
        +int Id
        +Vector3 Position
        +Vector3 Velocity
        +Vector3 AngularVelocity
        +Quaternion Rotation
        +double Mass
        +double InverseMass
        +double Radius
        +double Bounciness
        +double Friction
        +bool IsStatic
        +bool IsSleeping
        +object Tag
        
        +ApplyForce(Vector3 force)
        +ApplyImpulse(Vector3 impulse)
        +Wake()
        +Sleep()
    }
```

### Collision Detection

```mermaid
flowchart LR
    subgraph BroadPhase["Broad Phase"]
        SG[Spatial Grid]
        AABB[AABB Tests]
    end
    
    subgraph NarrowPhase["Narrow Phase"]
        SS[Sphere-Sphere]
        SP[Sphere-Plane]
        SB[Sphere-Box]
    end
    
    Bodies[All Bodies] --> SG
    SG --> AABB
    AABB -->|Potential pairs| NarrowPhase
    NarrowPhase --> Contacts[Contact Points]
```

### Spatial Grid Optimization

```mermaid
flowchart TB
    subgraph Grid["Spatial Grid"]
        C1[Cell 0,0]
        C2[Cell 1,0]
        C3[Cell 0,1]
        C4[Cell 1,1]
    end
    
    subgraph Bodies["Bodies"]
        B1[Body A]
        B2[Body B]
        B3[Body C]
    end
    
    B1 --> C1
    B2 --> C1
    B3 --> C4
    
    Note1[Only check collisions<br/>within same cell<br/>or neighboring cells]
```

### Threading Model (ThreadedBepuPhysicsEngine)

```mermaid
sequenceDiagram
    participant UI as UI Thread
    participant Queue as Operation Queue
    participant Physics as Physics Thread
    
    UI->>Queue: AddBody(body)
    UI->>Queue: SetGravity(9.81)
    
    loop Every 16.67ms (60 FPS)
        Physics->>Queue: Process pending operations
        Physics->>Physics: Run physics step
        Physics->>Physics: Collect body states
        Physics-->>UI: Dispatcher.BeginInvoke(BodiesUpdated)
    end
    
    UI->>UI: Update visual transforms
```

---

## Infrastructure

### MVVM Base Classes

```mermaid
classDiagram
    class ViewModelBase {
        <<abstract>>
        +event PropertyChanged
        #SetProperty~T~(ref T field, T value, string propertyName)
        #OnPropertyChanged(string propertyName)
    }
    
    class ICommand {
        <<interface>>
        +Execute(object parameter)
        +CanExecute(object parameter) bool
        +event CanExecuteChanged
    }
    
    class RelayCommand {
        -execute: Action~object~
        -canExecute: Func~object, bool~
        +Execute(object parameter)
        +CanExecute(object parameter) bool
        +RaiseCanExecuteChanged()
    }
    
    ICommand <|.. RelayCommand
```

### Value Converters

```mermaid
classDiagram
    class IValueConverter {
        <<interface>>
        +Convert(object value, ...) object
        +ConvertBack(object value, ...) object
    }
    
    class EnumToBoolConverter {
        +Convert(): compares enum to parameter
        +ConvertBack(): returns parameter as enum
    }
    
    IValueConverter <|.. EnumToBoolConverter
```

---

## Rendering Abstractions

### Renderer Interface Hierarchy

```mermaid
classDiagram
    class IRenderer {
        <<interface>>
        +Initialize(FrameworkElement container)
        +CreateBox(dimensions, material) ISceneObject
        +CreateSphere(radius, material) ISceneObject
        +CreateCylinder(radius, height, material) ISceneObject
        +CreateCone(radius, height, material) ISceneObject
        +CreateTorus(radius, tubeRadius, material) ISceneObject
        +RemoveObject(ISceneObject obj)
        +HitTest(Point point) ISceneObject
        +ResetCamera()
    }
    
    class ISceneObject {
        <<interface>>
        +object Visual
        +Transform Transform
        +Material Material
        +SetPosition(Vector3 position)
        +SetRotation(Quaternion rotation)
        +SetScale(Vector3 scale)
    }
    
    class IRendererFactory {
        <<interface>>
        +CreateRenderer(RendererType type) IRenderer
    }
    
    class RendererType {
        <<enumeration>>
        HelixWpf
        SharpDX
        NativeWpf
    }
    
    IRendererFactory --> IRenderer : creates
    IRenderer --> ISceneObject : creates
```

### Renderer Implementation Flow

```mermaid
flowchart TB
    Factory[IRendererFactory]
    
    subgraph Implementations["Possible Implementations"]
        Helix[HelixWpfRenderer]
        SharpDX[SharpDXRenderer]
        Native[NativeWpfRenderer]
    end
    
    Factory -->|Creates| Helix
    Factory -.->|Future| SharpDX
    Factory -.->|Future| Native
    
    Helix --> Scene[3D Scene]
```

---

## Models

### LightSource

```mermaid
classDiagram
    class LightSource {
        +string Name
        +LightType Type
        +Color Color
        +double Intensity
        +Vector3 Position
        +Vector3 Direction
        +double SpotAngle
        +bool IsEnabled
    }
    
    class LightType {
        <<enumeration>>
        Directional
        Point
        Spot
        Ambient
    }
    
    LightSource --> LightType
```

### ObjectColor

```mermaid
classDiagram
    class ObjectColor {
        <<enumeration>>
        Red
        Green
        Blue
        Yellow
        Orange
        Purple
        White
        Random
    }
```

---

## Helpers

### DragPlane3D

Utility class for calculating drag positions in 3D space.

```mermaid
flowchart LR
    subgraph Input
        Mouse[Mouse Position 2D]
        Camera[Camera]
        Plane[Drag Plane]
    end
    
    subgraph DragPlane3D
        Ray[Create Ray from Mouse]
        Intersect[Intersect with Plane]
    end
    
    subgraph Output
        Pos3D[3D Position]
    end
    
    Mouse --> Ray
    Camera --> Ray
    Ray --> Intersect
    Plane --> Intersect
    Intersect --> Pos3D
```

---

## Dependencies

```mermaid
graph TB
    subgraph Core["3DObjectViewer.Core"]
        Physics[Physics Engine]
        Infra[Infrastructure]
        Models[Models]
    end
    
    subgraph External["External Dependencies"]
        Bepu[BepuPhysics 2.x]
        Numerics[System.Numerics]
    end
    
    Physics --> Bepu
    Physics --> Numerics
```

| Package | Purpose |
|---------|---------|
| BepuPhysics | High-performance physics simulation |
| System.Numerics | SIMD-accelerated vector/matrix math |

---

## Design Decisions

### 1. Interface-Based Physics Engine

```mermaid
flowchart TB
    IPE[IPhysicsEngine]
    
    subgraph Implementations
        Custom[CustomPhysicsEngine<br/>Educational/Simple]
        Bepu[BepuPhysicsEngine<br/>High Performance]
        Threaded[ThreadedBepuPhysicsEngine<br/>Non-blocking UI]
    end
    
    IPE --> Custom
    IPE --> Bepu
    IPE --> Threaded
```

**Decision:** Use interface `IPhysicsEngine` for all physics implementations.

**Benefit:** Easily swap physics engines without changing consumer code.

### 2. SIMD Optimization

```mermaid
flowchart LR
    subgraph Traditional["Traditional Math"]
        T1[X = X1 + X2]
        T2[Y = Y1 + Y2]
        T3[Z = Z1 + Z2]
        T4[W = W1 + W2]
    end
    
    subgraph SIMD["SIMD Math"]
        S1["Vector4 = V1 + V2<br/>(Single instruction)"]
    end
    
    Traditional -->|4 operations| Result1[Result]
    SIMD -->|1 operation| Result2[Result]
```

**Decision:** Use `System.Numerics` types for all physics calculations.

**Benefit:** 2-4x performance improvement on vector operations.

### 3. Platform Independence

```mermaid
graph TB
    subgraph Core["3DObjectViewer.Core"]
        NoWPF[No WPF Dependencies]
        NoPlatform[No Platform-Specific Code]
    end
    
    subgraph Consumers["Potential Consumers"]
        WPF[WPF Application]
        MAUI[.NET MAUI App]
        Avalonia[Avalonia App]
        Console[Console App]
    end
    
    Core --> WPF
    Core --> MAUI
    Core --> Avalonia
    Core --> Console
```

**Decision:** Keep Core library free of WPF dependencies.

**Benefit:** Can be reused in other .NET UI frameworks or headless scenarios.

### 4. Factory Pattern for Engine Creation

```mermaid
sequenceDiagram
    participant App as Application
    participant Factory as PhysicsEngineFactory
    participant Engine as IPhysicsEngine
    
    App->>Factory: CreateEngine(EngineType.ThreadedBepu)
    Factory->>Factory: Configure engine options
    Factory-->>Engine: Return configured instance
    App->>Engine: Start()
```

**Decision:** Use factory to create physics engines.

**Benefit:** Centralized configuration and consistent initialization.

---

## Thread Safety

### Physics Engine Threading

```mermaid
flowchart TB
    subgraph UIThread["UI Thread"]
        Add[AddBody]
        Remove[RemoveBody]
        Set[SetGravity]
    end
    
    subgraph Queue["Thread-Safe Queue"]
        Q[Pending Operations]
    end
    
    subgraph PhysicsThread["Physics Thread"]
        Process[Process Queue]
        Simulate[Run Simulation]
        Notify[Notify UI]
    end
    
    Add --> Q
    Remove --> Q
    Set --> Q
    
    Q --> Process
    Process --> Simulate
    Simulate --> Notify
    Notify -->|Dispatcher| UIThread
```

**Key Points:**
- All public methods on `ThreadedBepuPhysicsEngine` are thread-safe
- Operations are queued and processed on the physics thread
- Results are dispatched back to the UI thread

---

## Performance Considerations

### Spatial Partitioning

```mermaid
flowchart TB
    subgraph NaiveApproach["Naive O(n²)"]
        N1[Check every pair]
        N2[100 bodies = 4,950 checks]
    end
    
    subgraph SpatialGrid["Spatial Grid O(n)"]
        S1[Partition into cells]
        S2[Only check neighbors]
        S3[100 bodies ? 200 checks]
    end
    
    NaiveApproach -->|Slow| Performance1[Poor Performance]
    SpatialGrid -->|Fast| Performance2[Good Performance]
```

### Sleep States

```mermaid
stateDiagram-v2
    [*] --> Awake : Body created
    Awake --> Sleeping : Velocity below threshold
    Sleeping --> Awake : Collision or force applied
    Sleeping --> Sleeping : Skip simulation
```

**Benefit:** Sleeping bodies don't participate in simulation, improving performance.

---

## Future Extensions

```mermaid
flowchart TB
    subgraph Current["Current Features"]
        Sphere[Sphere Collisions]
        Gravity[Gravity]
        Basic[Basic Materials]
    end
    
    subgraph Future["Potential Extensions"]
        Mesh[Mesh Collisions]
        Joints[Joints/Constraints]
        Soft[Soft Body Physics]
        Fluid[Fluid Simulation]
    end
    
    Current --> Future
```

Potential future additions:
- Convex mesh collision shapes
- Joint constraints (hinge, ball, slider)
- Soft body simulation
- Fluid dynamics
- Serialization/deserialization of physics state
