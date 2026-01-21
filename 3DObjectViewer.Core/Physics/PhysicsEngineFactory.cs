using _3DObjectViewer.Core.Physics.Abstractions;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Specifies which physics engine implementation to use.
/// </summary>
public enum PhysicsEngineType
{
    /// <summary>
    /// Simple custom physics engine with basic impulse-based dynamics.
    /// Runs on UI thread. Good for simple scenarios.
    /// </summary>
    Custom,

    /// <summary>
    /// Professional physics using BEPUphysics2 on UI thread.
    /// Good compatibility but may affect UI responsiveness with many bodies.
    /// </summary>
    Bepu,

    /// <summary>
    /// Professional physics using BEPUphysics2 on a dedicated background thread.
    /// Best performance and UI responsiveness. Recommended for complex scenes.
    /// </summary>
    BepuThreaded
}

/// <summary>
/// Factory for creating physics engine instances.
/// </summary>
public static class PhysicsEngineFactory
{
    /// <summary>
    /// Creates a physics engine of the specified type.
    /// </summary>
    /// <param name="engineType">The type of physics engine to create.</param>
    /// <returns>A new physics engine instance.</returns>
    public static IPhysicsEngine Create(PhysicsEngineType engineType = PhysicsEngineType.BepuThreaded)
    {
        return engineType switch
        {
            PhysicsEngineType.Custom => new CustomPhysicsEngine(),
            PhysicsEngineType.Bepu => new BepuPhysicsEngine(),
            PhysicsEngineType.BepuThreaded => new ThreadedBepuPhysicsEngine(),
            _ => throw new ArgumentOutOfRangeException(nameof(engineType), engineType, "Unknown physics engine type")
        };
    }
}
