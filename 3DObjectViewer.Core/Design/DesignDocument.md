# 3DObjectViewer.Core - Design Document

## Overview

**3DObjectViewer.Core** is a platform-independent class library that provides the foundational components for a 3D physics simulation application. This library contains no UI dependencies and can be reused across different presentation frameworks.

### Key Responsibilities

| Area | Description |
|------|-------------|
| **Physics Simulation** | Real-time rigid body physics with multiple engine implementations |
| **MVVM Infrastructure** | Base classes for ViewModels and Commands |
| **Rendering Abstractions** | Interfaces for pluggable 3D rendering backends |
| **Data Models** | Shared model classes for lights, colors, and scene objects |

### Target Framework
- .NET 10 (Windows)
- C# 14.0

---

## Project Structure

```
3DObjectViewer.Core/
+-- Physics/                    # Physics simulation engine
|   +-- Abstractions/           # IPhysicsEngine interface
|   +-- Bepu/                   # BEPUphysics2 callbacks
|   +-- BepuPhysicsEngine.cs    # Single-threaded BEPU implementation
|   +-- ThreadedBepuPhysicsEngine.cs  # Multi-threaded BEPU (recommended)
|   +-- CustomPhysicsEngine.cs  # Simple custom implementation
|   +-- PhysicsEngine.cs        # Facade for engine selection
|   +-- PhysicsEngineFactory.cs # Factory for creating engines
|   +-- PhysicsConstants.cs     # Tunable physics parameters
|   +-- RigidBody.cs            # Physics entity representation
|   +-- CollisionDetector.cs    # Collision detection algorithms
|   +-- CollisionResolver.cs    # Collision response calculations
|   +-- ForceIntegrator.cs      # Gravity, drag, position integration
|   +-- BoundaryCollisionHandler.cs  # Ground and wall collisions
|   +-- SpatialGrid.cs          # Broad-phase optimization
|   +-- VectorConversions.cs    # WPF <-> SIMD type conversions
+-- Rendering/
|   +-- Abstractions/           # IRenderer, ISceneObject interfaces
+-- Infrastructure/             # MVVM base classes
|   +-- ViewModelBase.cs        # INotifyPropertyChanged implementation
|   +-- RelayCommand.cs         # ICommand implementation
|   +-- Converters/             # XAML value converters
+-- Models/                     # Data models
|   +-- LightSource.cs          # Light configuration
|   +-- ObjectColor.cs          # Color enumeration
+-- Helpers/                    # Utility classes
    +-- DragPlane3D.cs          # 3D drag plane calculations
```

---

## Physics System

### Architecture Overview

The physics system uses a **Strategy Pattern** with multiple interchangeable physics engine implementations:

```
+---------------------------------------------------------------------+
|                      IPhysicsEngine                                 |
|  (Start, Stop, AddBody, RemoveBody, ApplyImpulse, etc.)            |
+---------------------------------------------------------------------+
                              ^
                              | implements
        +---------------------+---------------------+
        |                     |                     |
+---------------+   +-----------------+   +---------------------+
|CustomPhysics  |   |BepuPhysics      |   |ThreadedBepuPhysics  |
|Engine         |   |Engine           |   |Engine               |
|(UI Thread)    |   |(UI Thread)      |   |(Background Thread)  |
+---------------+   +-----------------+   +---------------------+
```

### Engine Implementations

#### 1. ThreadedBepuPhysicsEngine (Recommended)

The production-grade physics engine that runs simulation on a dedicated background thread.

**Threading Model:**

```
+---------------------------------------------------------------------+
|  UI Thread                                                          |
|  +---------------------------------------------------------------+  |
|  | ApplyResults()                                                |  |
|  |  - Updates RigidBody.Position, Orientation, Velocity          |  |
|  |  - Fires BodiesUpdated event                                  |  |
|  |  - Subscribers update Visual3D transforms                     |  |
|  +---------------------------------------------------------------+  |
+---------------------------------------------------------------------+
                              ^
                              | Dispatcher.BeginInvoke
                              |
+---------------------------------------------------------------------+
|  Physics Thread (dedicated, AboveNormal priority)                   |
|  +---------------------------------------------------------------+  |
|  | PhysicsLoop()                                                 |  |
|  |  1. ProcessPendingOperations() (add/remove bodies)            |  |
|  |  2. RebuildBoundaries() (if dirty flag set)                   |  |
|  |  3. Simulation.Timestep() -> ThreadDispatcher                 |  |
|  |  4. CollectBodyStates() -> BodyState[] snapshot               |  |
|  |  5. BeginInvoke(ApplyResults)                                 |  |
|  +---------------------------------------------------------------+  |
+---------------------------------------------------------------------+
                              |
                              v
+---------------------------------------------------------------------+
|  ThreadDispatcher (BEPU internal, N-1 worker threads)               |
|  +----------+  +----------+  +----------+  +----------+            |
|  | Worker 1 |  | Worker 2 |  | Worker 3 |  | Worker N |            |
|  | (SIMD)   |  | (SIMD)   |  | (SIMD)   |  | (SIMD)   |            |
|  +----------+  +----------+  +----------+  +----------+            |
+---------------------------------------------------------------------+
```

**Key Features:**
- Operations queue for thread-safe body management
- BodyState record for lock-free result transfer
- No locks on UI thread (avoids deadlocks)
- Graceful shutdown with ManualResetEventSlim

#### 2. BepuPhysicsEngine

Single-threaded variant that runs on the UI thread via DispatcherTimer. Useful for debugging or when background threading causes issues.

#### 3. CustomPhysicsEngine

Simple impulse-based physics engine for educational purposes or simple scenarios. Uses:
- SpatialGrid for broad-phase collision detection
- CollisionResolver for impulse calculations
- ForceIntegrator for gravity and drag

### RigidBody Class

Represents a physics entity with the following properties:

| Property | Type | Description |
|----------|------|-------------|
| `Id` | `Guid` | Unique identifier for correlation with visuals |
| `Position` | `Point3D` | Center position in world coordinates |
| `Velocity` | `Vector3D` | Linear velocity (units/second) |
| `AngularVelocity` | `Vector3D` | Rotational velocity (radians/second) |
| `Orientation` | `Quaternion` | Current rotation |
| `Mass` | `double` | Mass in kilograms |
| `Bounciness` | `double` | Coefficient of restitution (0-1) |
| `Friction` | `double` | Friction coefficient (0-1) |
| `Drag` | `double` | Air resistance coefficient |
| `BoundingRadius` | `double` | Collision sphere radius |
| `Height` | `double` | Object height for ground detection |
| `IsAtRest` | `bool` | Whether object is sleeping |
| `IsKinematic` | `bool` | Whether object is immovable |

### Physics Constants

All tunable physics parameters are centralized in `PhysicsConstants.cs`:

```csharp
public static class PhysicsConstants
{
    // Timing
    public const int TargetFps = 60;
    public const double MaxDeltaTime = 0.1;
    public const int CollisionIterations = 3;
    
    // Rest detection
    public const float RestThreshold = 0.1f;
    public const float GroundedThreshold = 0.05f;
    
    // Collision response
    public const float CollisionDamping = 0.85f;
    public const float WallDamping = 0.8f;
    
    // Default properties
    public const double DefaultGravity = 9.81;
    public const double DefaultBounciness = 0.4;
    public const double DefaultFriction = 0.5;
    public const double DefaultDrag = 0.001;
}
```

### BEPU Integration

The BEPUphysics2 integration uses callback structs for material properties and gravity:

**NarrowPhaseCallbacks** - Configures collision materials:
- Friction coefficient: 0.6 (hard surfaces)
- Spring stiffness: 120 (hard surface feel)
- Maximum recovery velocity: 10 (snappy response)

**PoseIntegratorCallbacks** - Applies gravity and damping:
- Linear damping: 0.995 per second (minimal air resistance)
- Angular damping: 0.99 per second (prevents infinite spinning)
- Gravity applied in -Z direction

---

## MVVM Infrastructure

### ViewModelBase

Base class for all ViewModels providing `INotifyPropertyChanged`:

```csharp
public abstract class ViewModelBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    
    protected bool SetProperty<T>(ref T field, T value, 
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
    
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
```

### RelayCommand

Generic `ICommand` implementation for MVVM:

```csharp
public class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;
    
    public RelayCommand(Action execute, Func<bool>? canExecute = null);
    
    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => _execute();
}
```

---

## Rendering Abstractions

### IRenderer Interface

Defines the contract for 3D rendering backends:

```csharp
public interface IRenderer
{
    string Name { get; }
    FrameworkElement ViewportControl { get; }
    Point3D CameraPosition { get; set; }
    
    void Initialize();
    ISceneObject CreateBox(double width, double height, double length, Material material);
    ISceneObject CreateSphere(double radius, Material material);
    void AddObject(ISceneObject obj);
    void RemoveObject(ISceneObject obj);
    (ISceneObject? obj, Point3D position)? HitTest(Point position);
}
```

### ISceneObject Interface

Represents a renderable 3D object:

```csharp
public interface ISceneObject
{
    Guid Id { get; }
    Point3D Position { get; set; }
    Transform3D Transform { get; set; }
    double BoundingRadius { get; }
    double Height { get; }
    object NativeObject { get; }
}
```

---

## Data Models

### LightSource

Represents a configurable light in the scene:

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Display name |
| `LightType` | `LightType` | Directional, Point, Spot, Ambient |
| `Color` | `Color` | Light color |
| `Intensity` | `double` | Brightness (0-2) |
| `Position` | `Point3D` | World position |
| `Direction` | `Vector3D` | Light direction |
| `IsEnabled` | `bool` | On/off state |

### ObjectColor

Enumeration of available object colors:

```csharp
public enum ObjectColor
{
    Red,
    Green,
    Blue,
    Yellow,
    Random
}
```

---

## Design Decisions

### 1. Guid-Based Object Correlation

**Decision:** Use `Guid` instead of object references for correlating physics bodies with visuals.

**Rationale:**
- Decouples physics from specific renderer implementations
- Survives renderer switches (objects recreated with same ID)
- O(1) lookup via `Dictionary<Guid, RigidBody>`
- No circular references between physics and rendering layers

### 2. Thread Ownership Model

**Decision:** Each thread owns its data exclusively.

**Implementation:**
- Physics thread owns BEPU Simulation state
- UI thread owns RigidBody properties and Visual3D transforms
- Only the operations queue is shared (with minimal lock scope)

**Benefit:** Eliminates deadlocks and reduces lock contention.

### 3. Snapshot-Based State Transfer

**Decision:** Use immutable `BodyState` records for transferring physics results to UI.

```csharp
private readonly record struct BodyState(
    Guid Id,
    Vector3 Position,
    Quaternion Orientation,
    Vector3 LinearVelocity,
    Vector3 AngularVelocity,
    bool IsAwake,
    bool IsGrounded,
    double Height);
```

**Benefit:** No locks needed during result application on UI thread.

### 4. Factory Pattern for Engine Selection

**Decision:** Use factory with enum for physics engine selection.

```csharp
public enum PhysicsEngineType
{
    Custom,       // Simple, UI thread
    Bepu,         // Professional, UI thread
    BepuThreaded  // Professional, background thread (default)
}
```

**Benefit:** Easy to switch engines for testing or performance tuning.

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| BepuPhysics | 2.x | Professional physics simulation |
| BepuUtilities | 2.x | BEPU memory management and threading |

---

## Usage Example

```csharp
// Create physics engine (uses threaded BEPU by default)
var engine = PhysicsEngineFactory.Create(PhysicsEngineType.BepuThreaded);

// Configure
engine.Gravity = 9.81;
engine.GroundLevel = 0;
engine.SetBucketBounds(-10, 10, -10, 10, 4);

// Subscribe to updates
engine.BodiesUpdated += bodies => 
{
    foreach (var body in bodies)
    {
        // Update visual transforms
        UpdateVisual(body.Id, body.Position, body.Orientation);
    }
};

// Add bodies
var body = new RigidBody(Guid.NewGuid(), position, radius: 0.5, height: 1.0);
body.Mass = 1.0;
body.Bounciness = 0.5;
engine.AddBody(body);

// Start simulation
engine.Start();

// Apply forces
engine.ApplyImpulse(body, new Vector3D(10, 0, 5));

// Cleanup
engine.Stop();
engine.Dispose();
