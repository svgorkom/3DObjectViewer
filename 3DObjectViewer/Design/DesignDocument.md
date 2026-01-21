# 3DObjectViewer - Design Document

## Overview

**3DObjectViewer** is a WPF desktop application that provides an interactive 3D environment for creating, manipulating, and observing 3D objects with real-time physics simulation. Users can drop objects into a virtual space and watch them fall, bounce, and interact under the influence of gravity.

### Application Capabilities

| Feature | Description |
|---------|-------------|
| **Object Creation** | Add 3D primitives (cubes, spheres, cylinders, cones, toruses) with customizable size and color |
| **Batch Drop** | Drop multiple objects simultaneously (1-20 at once) |
| **Physics Simulation** | Objects fall under gravity, collide with each other and boundaries, and come to rest naturally |
| **Object Manipulation** | Select objects, drag to move them, adjust position/rotation/scale via sliders |
| **Dynamic Lighting** | Multiple light sources with adjustable position, direction, color, and brightness |
| **Performance Monitoring** | Real-time display of FPS, frame time, object count, triangle count, and memory usage |

### Target Framework
- .NET 10 (Windows)
- WPF (Windows Presentation Foundation)
- C# 14.0

---

## Project Structure

```
3DObjectViewer/
+-- ViewModels/                 # MVVM ViewModels
|   +-- MainViewModel.cs        # Root ViewModel, orchestrates child VMs
|   +-- ObjectsViewModel.cs     # Object creation and settings
|   +-- SelectionViewModel.cs   # Selected object manipulation
|   +-- LightingViewModel.cs    # Light source management
|   +-- PerformanceStatsViewModel.cs  # FPS and statistics display
+-- Views/
|   +-- Controls/               # User controls
|       +-- ObjectControlPanel.xaml     # Main control panel
|       +-- LightingControlPanel.xaml   # Lighting controls
+-- Rendering/
|   +-- HelixWpf/               # HelixToolkit.Wpf implementation
|   |   +-- HelixWpfRenderer.cs       # IRenderer implementation
|   |   +-- HelixSceneObject.cs       # ISceneObject wrapper
|   |   +-- HelixSceneObjects.cs      # Static factory methods
|   +-- Services/
|   |   +-- LightingService.cs        # Light management
|   +-- RendererManager.cs      # Renderer lifecycle management
|   +-- RendererFactory.cs      # Factory for creating renderers
+-- Physics/
|   +-- PhysicsHelper.cs        # Visual3D <-> RigidBody bridge
+-- Services/
|   +-- SceneService.cs         # Scene facade (lighting, shadows)
+-- MainWindow.xaml             # Main application window
+-- MainWindow.xaml.cs          # Window code-behind (mouse handling)
+-- App.xaml                    # Application entry point
```

---

## Architecture

### High-Level Architecture

```
+-----------------------------------------------------------------------------+
|                           PRESENTATION LAYER                                |
|  +------------------+  +-----------------------+  +------------------------+|
|  |   MainWindow     |  |  ObjectControlPanel   |  |  LightingControlPanel  ||
|  |   (XAML + C#)    |  |      (XAML)           |  |       (XAML)           ||
|  +------------------+  +-----------------------+  +------------------------+|
+-----------------------------------------------------------------------------+
            |                      |                           |
            | DataContext          | Bindings                  | Bindings
            v                      v                           v
+-----------------------------------------------------------------------------+
|                            VIEWMODEL LAYER                                  |
|  +-----------------------------------------------------------------------+  |
|  |                         MainViewModel                                 |  |
|  |  +------------------+  +-------------------+  +-----------------------+| |
|  |  | ObjectsViewModel |  |SelectionViewModel |  |  LightingViewModel    || |
|  |  | - AddCubeCommand |  | - SelectedObject  |  |  - LightSources       || |
|  |  | - DropCount      |  | - Position X/Y/Z  |  |  - SelectedLight      || |
|  |  | - ObjectSize     |  | - Scale           |  |  - Color, Intensity   || |
|  |  | - SelectedColor  |  | - Rotation X/Y/Z  |  |  - Position           || |
|  |  +------------------+  +-------------------+  +-----------------------+| |
|  |  +-------------------------------------------------------------------+|  |
|  |  |                 PerformanceStatsViewModel                         ||  |
|  |  |  - FPS, FrameTime, ObjectCount, Triangles, Memory, Camera         ||  |
|  |  +-------------------------------------------------------------------+|  |
|  +-----------------------------------------------------------------------+  |
+-----------------------------------------------------------------------------+
            |                                              |
            v                                              v
+-----------------------------------+  +-----------------------------------+
|         RENDERING LAYER           |  |        PHYSICS LAYER              |
|  +-----------------------------+  |  |  +-----------------------------+  |
|  |      RendererManager        |  |  |  |   PhysicsEngine             |  |
|  |  +------------------------+ |  |  |  |  (ThreadedBepuPhysics)      |  |
|  |  |     IRenderer          | |  |  |  +-----------------------------+  |
|  |  |  +-------------------+ | |  |  |  +-----------------------------+  |
|  |  |  | HelixWpfRenderer  | | |  |  |  |   PhysicsHelper             |  |
|  |  |  +-------------------+ | |  |  |  |  (Visual <-> Body bridge)   |  |
|  |  +------------------------+ |  |  |  +-----------------------------+  |
|  +-----------------------------+  |  +-----------------------------------+
|  +-----------------------------+  |
|  |      SceneService           |  |
|  |  - LightingService          |  |
|  +-----------------------------+  |
+-----------------------------------+
```

### MVVM Pattern Implementation

The application follows the MVVM (Model-View-ViewModel) pattern:

| Layer | Components | Responsibilities |
|-------|------------|------------------|
| **View** | MainWindow, Control Panels | UI layout, user input, data binding |
| **ViewModel** | MainViewModel + child VMs | Business logic, state management, commands |
| **Model** | RigidBody, LightSource | Data structures, physics state |

---

## ViewModels

### MainViewModel

The root ViewModel that orchestrates all child ViewModels and services.

**Responsibilities:**
- Manages the `SceneObjects` collection shared by all child VMs
- Owns the `PhysicsEngine` instance
- Handles physics events and updates visual transforms
- Coordinates object creation, selection, and deletion

**Key Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `Objects` | `ObjectsViewModel` | Object creation settings |
| `Selection` | `SelectionViewModel` | Selected object manipulation |
| `Lighting` | `LightingViewModel` | Light source management |
| `PerformanceStats` | `PerformanceStatsViewModel` | Real-time statistics |
| `PhysicsEnabled` | `bool` | Toggle physics simulation |
| `Gravity` | `double` | Gravity strength (m/s^2) |
| `SceneObjects` | `ObservableCollection<Visual3D>` | All 3D objects in scene |

**Commands:**

| Command | Action |
|---------|--------|
| `ClearAllCommand` | Remove all objects from scene |
| `ResetCameraCommand` | Reset camera to default position |
| `TogglePhysicsCommand` | Start/stop physics simulation |
| `DropAllCommand` | Wake all sleeping objects |

### ObjectsViewModel

Manages object creation with configurable settings.

**Key Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `DropCount` | `int` | Number of objects to drop (1-50) |
| `ObjectSize` | `double` | Size multiplier (0.5-3.0) |
| `SelectedColor` | `ObjectColor` | Color for new objects |
| `DropHeight` | `double` | Height to drop from (2-15m) |

**Commands:**

| Command | Creates |
|---------|---------|
| `AddCubeCommand` | BoxVisual3D (N times) |
| `AddSphereCommand` | SphereVisual3D (N times) |
| `AddCylinderCommand` | PipeVisual3D (N times) |
| `AddConeCommand` | TruncatedConeVisual3D (N times) |
| `AddTorusCommand` | TorusVisual3D (N times) |

### SelectionViewModel

Handles selected object manipulation.

**Key Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `SelectedObject` | `Visual3D?` | Currently selected object |
| `HasSelection` | `bool` | Whether an object is selected |
| `SelectedObjectPositionX/Y/Z` | `double` | Position sliders |
| `SelectedObjectScale` | `double` | Scale multiplier |
| `SelectedObjectRotationX/Y/Z` | `double` | Rotation angles |

**Events:**

| Event | Description |
|-------|-------------|
| `SelectionChanged` | Selection changed (update highlight) |
| `ObjectDeleted` | Object was deleted (cleanup physics) |
| `ObjectMoved` | Object position changed (sync physics) |

### LightingViewModel

Manages scene lighting configuration.

**Key Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `LightSources` | `ObservableCollection<LightSource>` | All lights |
| `SelectedLight` | `LightSource?` | Light being edited |
| `AmbientIntensity` | `double` | Ambient light level |

### PerformanceStatsViewModel

Displays real-time performance metrics.

**Displayed Statistics:**

| Statistic | Update Rate | Source |
|-----------|-------------|--------|
| FPS | 500ms | Frame timing |
| Frame Time | 500ms | Frame timing |
| Object Count | 250ms | SceneObjects.Count |
| Triangle Count | 250ms | Visual tree traversal |
| Physics Bodies | 250ms | PhysicsEngine.BodyCount |
| Memory Usage | 250ms | GC.GetTotalMemory() |
| Camera Position | 250ms | HelixViewport.Camera |

---

## Rendering System

### HelixToolkit.Wpf Integration

The application uses HelixToolkit.Wpf for 3D rendering, wrapped behind the `IRenderer` interface for potential future extensibility.

**HelixWpfRenderer** implements:
- Viewport creation and management
- Primitive creation (box, sphere, cylinder, cone, torus)
- Hit testing for object selection
- Camera control

**Supported Visual Types:**

| Visual | HelixToolkit Class | Physics Shape |
|--------|-------------------|---------------|
| Box | `BoxVisual3D` | Sphere (bounding) |
| Sphere | `SphereVisual3D` | Sphere |
| Cylinder | `PipeVisual3D` | Sphere (bounding) |
| Cone | `TruncatedConeVisual3D` | Sphere (bounding) |
| Torus | `TorusVisual3D` | Sphere (bounding) |

### Performance Optimization

**Problem:** HelixToolkit regenerates meshes when visual properties (Center, Origin, Point1/Point2) change.

**Solution:** The `PhysicsHelper` class uses a **normalize-once pattern**:

1. At creation: Move geometry to origin (triggers one mesh rebuild)
2. Every frame: Only update `Transform` property (cheap matrix operation)

```csharp
// EXPENSIVE - triggers mesh rebuild
sphere.Center = newPosition;  // DON'T DO THIS

// CHEAP - just matrix update
sphere.Transform = new TranslateTransform3D(x, y, z);  // DO THIS
```

---

## Physics Integration

### PhysicsHelper

Bridges the gap between physics bodies and visual objects.

**Responsibilities:**
1. Create `RigidBody` from `Visual3D` with correct dimensions
2. Configure physics properties based on object type
3. Apply physics transforms to visuals efficiently

**Object-Specific Physics Properties:**

| Object Type | Bounciness | Friction | Mass |
|-------------|------------|----------|------|
| Sphere | 0.6 | 0.3 | 1.0 |
| Box | 0.3 | 0.6 | 2.0 |
| Cylinder | 0.35 | 0.5 | 1.5 |
| Cone | 0.3 | 0.55 | 1.2 |
| Torus | 0.5 | 0.4 | 0.8 |

### Data Flow: Object Creation

```
User clicks "Add Sphere(s)"
         |
         v
+-------------------------------------------------------------+
| ObjectsViewModel.AddSpheres()                               |
|   for each dropCount:                                       |
|     1. Calculate random drop position                       |
|     2. Create SphereVisual3D with material                  |
|     3. Add to SceneObjects collection                       |
|     4. Fire ObjectAdded event                               |
+-------------------------------------------------------------+
         |
         v
+-------------------------------------------------------------+
| MainViewModel.OnObjectAdded(visual, position)               |
|   1. PhysicsHelper.CreateRigidBody(visual, position)        |
|      - Extracts dimensions from visual                      |
|      - Normalizes visual (move to origin)                   |
|      - Applies initial transform                            |
|      - Returns configured RigidBody                         |
|   2. PhysicsEngine.AddBody(body)                            |
|      - Queued for physics thread processing                 |
|   3. Store visual -> bodyId mapping                         |
+-------------------------------------------------------------+
         |
         v
+-------------------------------------------------------------+
| PhysicsEngine (background thread)                           |
|   1. Process pending operations (add body to simulation)    |
|   2. Run physics timestep                                   |
|   3. Collect body states                                    |
|   4. BeginInvoke(ApplyResults)                              |
+-------------------------------------------------------------+
         |
         v
+-------------------------------------------------------------+
| MainViewModel.OnPhysicsBodiesUpdated(bodies)                |
|   for each body:                                            |
|     PhysicsHelper.ApplyTransformToVisual(body)              |
|       - Updates visual.Transform with position/rotation     |
+-------------------------------------------------------------+
```

---

## User Interface

### Main Window Layout

```
+----------------------------------------------------------------------------+
|  3D Object Viewer                                              [-][o][x]   |
+----------------------------------------------------------------------------+
| +----------------------------------------------------------+ +-----------+ |
| |                                                          | |  Controls | |
| |                                                          | |           | |
| |                                                          | | [Add      | |
| |                      3D Viewport                         | |  Objects] | |
| |                                                          | |           | |
| |                   (HelixViewport3D)                      | | [Settings]| |
| |                                                          | |           | |
| |                                                          | | [Physics] | |
| |                                                          | |           | |
| |                                                          | | [Selected | |
| |                                                          | |  Object]  | |
| |                                                          | |           | |
| | +------------------------------------------------------+ | | [Scene]   | |
| | | FPS: 60 | Frame: 16.7ms | Objects: 25 | Tris: 12.5k  | | |           | |
| | +------------------------------------------------------+ | | [Help]    | |
| +----------------------------------------------------------+ +-----------+ |
+----------------------------------------------------------------------------+
```

### Control Panel Sections

**Add Objects:**
- Drop Count slider (1-20)
- Add Cube/Sphere/Cylinder/Cone/Torus buttons

**New Object Settings:**
- Color selection (Red/Green/Blue/Yellow/Random)
- Size slider (0.5-3.0)
- Drop Height slider (2-15m)

**Physics:**
- Enable Physics checkbox
- Gravity slider (1-30 m/s^2)
- Drop All Objects button

**Selected Object:** (visible when object selected)
- Apply Color button
- Position X/Y/Z sliders
- Scale slider
- Rotation X/Y/Z sliders
- Deselect button
- Delete Object button

**Scene:**
- Clear All Objects button
- Reset Camera button

### Mouse Controls

| Action | Behavior |
|--------|----------|
| Left-click on object | Select object |
| Ctrl + Left-drag | Move selected object |
| Left-drag on background | Rotate camera |
| Right-drag | Pan camera |
| Scroll wheel | Zoom in/out |
| Escape | Deselect object |

---

## Services

### SceneService

Facade that coordinates scene-related services:

```csharp
public class SceneService
{
    public LightingService Lighting { get; }
    
    public void UpdateAllLights(IEnumerable<LightSource> sources);
}
```

### LightingService

Manages light visuals in the 3D scene:

- Creates DirectionalLight, PointLight, SpotLight instances
- Updates light properties when ViewModel changes
- Manages lamp fixture visuals

---

## Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| HelixToolkit.Wpf | 2.x | 3D rendering and viewport |
| 3DObjectViewer.Core | local | Physics engine and infrastructure |

---

## Design Decisions

### 1. Composition over Inheritance for ViewModels

**Decision:** MainViewModel composes child ViewModels rather than inheriting.

**Implementation:**
```csharp
public class MainViewModel : ViewModelBase
{
    public ObjectsViewModel Objects { get; }
    public SelectionViewModel Selection { get; }
    public LightingViewModel Lighting { get; }
}
```

**Benefit:** Each ViewModel has a single responsibility, easier to test and maintain.

### 2. Event-Based Communication

**Decision:** ViewModels communicate via events, not direct method calls.

**Example:**
```csharp
// ObjectsViewModel fires event
ObjectAdded?.Invoke(visual, position);

// MainViewModel subscribes
Objects.ObjectAdded += OnObjectAdded;
```

**Benefit:** Loose coupling, ViewModels don't need references to each other.

### 3. Static PhysicsHelper

**Decision:** PhysicsHelper is a static class with static caches.

**Rationale:**
- Only one physics-visual bridge per application
- Simplifies access from multiple ViewModels
- Caches are cleared when scene is cleared

**Tradeoff:** Harder to unit test, but acceptable for this application scope.

### 4. Transform-Only Visual Updates

**Decision:** Never modify visual geometry properties after creation.

**Implementation:**
- At creation: Normalize geometry to origin
- Every frame: Only update Transform property

**Benefit:** 100x+ performance improvement for physics updates.

### 5. Threaded Physics as Default

**Decision:** Use `ThreadedBepuPhysicsEngine` as the default engine.

**Rationale:**
- Keeps UI responsive even with many objects
- Physics runs at consistent 60 FPS regardless of rendering load
- Better user experience for complex scenes

---

## Future Extensibility

### Multiple Renderer Support

The `IRenderer` interface allows adding alternative rendering backends:

```csharp
public enum RendererType
{
    HelixWpf,      // Current implementation
    SharpDX,       // Potential DirectX backend
    NativeWpf      // Potential Viewport3D backend
}
```

### Additional Object Types

New object types can be added by:
1. Adding creation method in `ObjectsViewModel`
2. Adding dimension extraction in `PhysicsHelper.GetObjectDimensions`
3. Adding normalization logic in `PhysicsHelper.NormalizeVisual`

### Persistence

Future versions could add:
- Save/load scene to file
- Export to 3D formats (OBJ, STL)
- Screenshot/video capture
