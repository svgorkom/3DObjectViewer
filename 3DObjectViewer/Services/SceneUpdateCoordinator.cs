using System.Windows;
using System.Windows.Threading;
using _3DObjectViewer.Core.Physics;

namespace _3DObjectViewer.Services;

/// <summary>
/// Coordinates physics-to-UI updates with intelligent throttling to prevent UI thread blocking.
/// </summary>
/// <remarks>
/// <para>
/// When many objects are moving, the physics engine can generate updates faster than the UI
/// can render them. This coordinator batches updates and delivers them at an optimal rate:
/// </para>
/// <list type="bullet">
///   <item>~60 FPS for light scenes (&lt;50 bodies)</item>
///   <item>~30 FPS for medium scenes (50-200 bodies)</item>
///   <item>~20 FPS for heavy scenes (&gt;200 bodies)</item>
/// </list>
/// </remarks>
public sealed class SceneUpdateCoordinator : IDisposable
{
    private const int LightLoadThreshold = 50;
    private const int HeavyLoadThreshold = 200;
    
    private static readonly TimeSpan LightLoadInterval = TimeSpan.FromMilliseconds(16);  // ~60 FPS
    private static readonly TimeSpan MediumLoadInterval = TimeSpan.FromMilliseconds(33); // ~30 FPS  
    private static readonly TimeSpan HeavyLoadInterval = TimeSpan.FromMilliseconds(50);  // ~20 FPS

    private readonly Dispatcher _dispatcher;
    private readonly DispatcherTimer _throttleTimer;
    private readonly object _lock = new();
    
    private IReadOnlyList<RigidBody>? _pendingBodies;
    private bool _disposed;

    /// <summary>
    /// Raised on the UI thread when batched physics updates are ready.
    /// </summary>
    public event Action<IReadOnlyList<RigidBody>>? BatchedUpdateReady;

    /// <summary>
    /// Creates a new <see cref="SceneUpdateCoordinator"/>.
    /// </summary>
    public SceneUpdateCoordinator()
    {
        _dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        
        _throttleTimer = new DispatcherTimer(DispatcherPriority.Render, _dispatcher)
        {
            Interval = LightLoadInterval
        };
        _throttleTimer.Tick += OnThrottleTimerTick;
        _throttleTimer.Start();
    }

    /// <summary>
    /// Queues a physics update for batched delivery to the UI thread.
    /// </summary>
    /// <param name="bodies">The updated rigid bodies.</param>
    /// <remarks>
    /// Can be called from any thread. Only the most recent update is kept;
    /// intermediate updates are discarded to prevent queue buildup.
    /// </remarks>
    public void QueuePhysicsUpdate(IReadOnlyList<RigidBody> bodies)
    {
        if (_disposed) return;

        lock (_lock)
        {
            _pendingBodies = bodies;
            
            // Adjust throttle interval based on load
            var targetInterval = bodies.Count switch
            {
                > HeavyLoadThreshold => HeavyLoadInterval,
                > LightLoadThreshold => MediumLoadInterval,
                _ => LightLoadInterval
            };
            
            if (_throttleTimer.Interval != targetInterval)
            {
                _throttleTimer.Interval = targetInterval;
            }
        }
    }

    private void OnThrottleTimerTick(object? sender, EventArgs e)
    {
        if (_disposed) return;

        IReadOnlyList<RigidBody>? bodies;
        lock (_lock)
        {
            bodies = _pendingBodies;
            _pendingBodies = null;
        }

        if (bodies is { Count: > 0 })
        {
            BatchedUpdateReady?.Invoke(bodies);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _throttleTimer.Stop();
        _throttleTimer.Tick -= OnThrottleTimerTick;
        
        lock (_lock)
        {
            _pendingBodies = null;
        }
    }
}
