using System.Windows.Media;
using _3DObjectViewer.Core.Infrastructure;

namespace _3DObjectViewer.ViewModels;

/// <summary>
/// ViewModel for displaying real-time performance statistics and rendering metrics.
/// </summary>
/// <remarks>
/// This ViewModel provides bindable properties for performance monitoring including:
/// <list type="bullet">
///   <item><description>FPS (frames per second) and frame time</description></item>
///   <item><description>Scene object and triangle counts</description></item>
///   <item><description>Physics simulation statistics</description></item>
///   <item><description>Memory usage</description></item>
///   <item><description>Camera position</description></item>
/// </list>
/// 
/// The ViewModel is designed to be updated from the View's rendering loop
/// (CompositionTarget.Rendering) since timing-critical operations must run
/// on the UI thread. The View calls <see cref="UpdateFps"/> and <see cref="UpdateStatistics"/>
/// at appropriate intervals, keeping timing logic in the View while the ViewModel
/// holds the display state.
/// </remarks>
public class PerformanceStatsViewModel : ViewModelBase
{
    #region Constants

    /// <summary>
    /// Maximum number of data points to retain for graphs.
    /// </summary>
    public const int MaxDataPoints = 60;

    /// <summary>
    /// Width of the graphs in device-independent pixels.
    /// </summary>
    public const double GraphWidth = 150;

    /// <summary>
    /// Height of the graphs in device-independent pixels.
    /// </summary>
    public const double GraphHeight = 40;

    /// <summary>
    /// Maximum FPS value for graph scaling (Y-axis maximum).
    /// </summary>
    public const double MaxFpsValue = 120;
    
    /// <summary>
    /// Maximum memory value for graph scaling in MB (Y-axis maximum).
    /// </summary>
    public const double MaxMemoryMb = 500;

    #endregion

    #region Private Fields

    private string _fpsText = "--";
    private string _frameTimeText = "-- ms";
    private string _objectCountText = "0";
    private string _triangleCountText = "0";
    private string _physicsBodiesText = "0";
    private string _physicsEngineText = "";
    private string _memoryUsageText = "-- MB";
    private string _cameraPositionText = "X: --  Y: --  Z: --";
    private PointCollection _fpsGraphPoints = [];
    private PointCollection _memoryGraphPoints = [];

    // FPS history circular buffer
    private readonly double[] _fpsHistory = new double[MaxDataPoints];
    private int _fpsHistoryIndex;
    private int _fpsHistoryCount;
    
    // Memory history circular buffer
    private readonly double[] _memoryHistory = new double[MaxDataPoints];
    private int _memoryHistoryIndex;
    private int _memoryHistoryCount;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the current FPS value as a formatted string.
    /// </summary>
    public string FpsText
    {
        get => _fpsText;
        private set => SetProperty(ref _fpsText, value);
    }

    /// <summary>
    /// Gets the current frame time in milliseconds as a formatted string.
    /// </summary>
    public string FrameTimeText
    {
        get => _frameTimeText;
        private set => SetProperty(ref _frameTimeText, value);
    }

    /// <summary>
    /// Gets the current scene object count as a formatted string.
    /// </summary>
    public string ObjectCountText
    {
        get => _objectCountText;
        private set => SetProperty(ref _objectCountText, value);
    }

    /// <summary>
    /// Gets the total triangle count as a formatted string.
    /// </summary>
    /// <remarks>
    /// Large values are formatted with K (thousands) or M (millions) suffixes
    /// for readability.
    /// </remarks>
    public string TriangleCountText
    {
        get => _triangleCountText;
        private set => SetProperty(ref _triangleCountText, value);
    }

    /// <summary>
    /// Gets the active physics bodies count as a formatted string.
    /// </summary>
    public string PhysicsBodiesText
    {
        get => _physicsBodiesText;
        private set => SetProperty(ref _physicsBodiesText, value);
    }

    /// <summary>
    /// Gets the name of the current physics engine.
    /// </summary>
    public string PhysicsEngineText
    {
        get => _physicsEngineText;
        private set => SetProperty(ref _physicsEngineText, value);
    }

    /// <summary>
    /// Gets the memory usage in megabytes as a formatted string.
    /// </summary>
    public string MemoryUsageText
    {
        get => _memoryUsageText;
        private set => SetProperty(ref _memoryUsageText, value);
    }

    /// <summary>
    /// Gets the camera position as a formatted string.
    /// </summary>
    public string CameraPositionText
    {
        get => _cameraPositionText;
        private set => SetProperty(ref _cameraPositionText, value);
    }

    /// <summary>
    /// Gets the points collection for the FPS graph polyline.
    /// </summary>
    public PointCollection FpsGraphPoints
    {
        get => _fpsGraphPoints;
        private set => SetProperty(ref _fpsGraphPoints, value);
    }
    
    /// <summary>
    /// Gets the points collection for the memory usage graph polyline.
    /// </summary>
    public PointCollection MemoryGraphPoints
    {
        get => _memoryGraphPoints;
        private set => SetProperty(ref _memoryGraphPoints, value);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the FPS display and graph with new timing data.
    /// </summary>
    /// <param name="fps">The calculated frames per second.</param>
    /// <param name="frameTimeMs">The average frame time in milliseconds.</param>
    public void UpdateFps(double fps, double frameTimeMs)
    {
        FpsText = $"{fps:F1}";
        FrameTimeText = $"{frameTimeMs:F2} ms";

        UpdateFpsGraph(fps);
    }

    /// <summary>
    /// Updates scene statistics (object count, triangle count, physics bodies, memory).
    /// </summary>
    /// <param name="objectCount">Number of objects in the scene.</param>
    /// <param name="triangleCount">Total number of triangles in the scene.</param>
    /// <param name="physicsBodiesCount">Total number of physics bodies.</param>
    /// <param name="activePhysicsBodiesCount">Number of active (non-resting) physics bodies.</param>
    /// <param name="physicsEngineName">Name of the physics engine being used.</param>
    public void UpdateStatistics(int objectCount, int triangleCount, int physicsBodiesCount, int activePhysicsBodiesCount = -1, string? physicsEngineName = null)
    {
        ObjectCountText = objectCount.ToString();
        TriangleCountText = FormatNumber(triangleCount);
        
        // Show active/total if active count is provided
        if (activePhysicsBodiesCount >= 0)
        {
            PhysicsBodiesText = $"{activePhysicsBodiesCount}/{physicsBodiesCount}";
        }
        else
        {
            PhysicsBodiesText = physicsBodiesCount.ToString();
        }

        // Update physics engine name if provided
        if (physicsEngineName is not null)
        {
            PhysicsEngineText = physicsEngineName;
        }

        // Update memory usage
        var memoryBytes = GC.GetTotalMemory(false);
        var memoryMb = memoryBytes / (1024.0 * 1024.0);
        MemoryUsageText = $"{memoryMb:F1} MB";
        
        UpdateMemoryGraph(memoryMb);
    }

    /// <summary>
    /// Updates the camera position display.
    /// </summary>
    /// <param name="x">Camera X position.</param>
    /// <param name="y">Camera Y position.</param>
    /// <param name="z">Camera Z position.</param>
    public void UpdateCameraPosition(double x, double y, double z)
    {
        CameraPositionText = $"X: {x:F1}  Y: {y:F1}  Z: {z:F1}";
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Updates the FPS graph with a new data point.
    /// </summary>
    /// <param name="fps">The current FPS value.</param>
    private void UpdateFpsGraph(double fps)
    {
        // Use circular buffer
        _fpsHistory[_fpsHistoryIndex] = fps;
        _fpsHistoryIndex = (_fpsHistoryIndex + 1) % MaxDataPoints;
        if (_fpsHistoryCount < MaxDataPoints)
            _fpsHistoryCount++;

        // Create new PointCollection for proper binding update
        var points = new PointCollection(_fpsHistoryCount);
        
        int startIndex = _fpsHistoryCount < MaxDataPoints 
            ? 0 
            : _fpsHistoryIndex;
        
        for (int i = 0; i < _fpsHistoryCount; i++)
        {
            int index = (startIndex + i) % MaxDataPoints;
            double x = (double)i / (MaxDataPoints - 1) * GraphWidth;
            double y = GraphHeight - (_fpsHistory[index] / MaxFpsValue * GraphHeight);
            y = Math.Clamp(y, 0, GraphHeight);
            points.Add(new System.Windows.Point(x, y));
        }

        FpsGraphPoints = points;
    }
    
    /// <summary>
    /// Updates the memory graph with a new data point.
    /// </summary>
    /// <param name="memoryMb">The current memory usage in MB.</param>
    private void UpdateMemoryGraph(double memoryMb)
    {
        // Use circular buffer
        _memoryHistory[_memoryHistoryIndex] = memoryMb;
        _memoryHistoryIndex = (_memoryHistoryIndex + 1) % MaxDataPoints;
        if (_memoryHistoryCount < MaxDataPoints)
            _memoryHistoryCount++;

        // Create new PointCollection for proper binding update
        var points = new PointCollection(_memoryHistoryCount);
        
        int startIndex = _memoryHistoryCount < MaxDataPoints 
            ? 0 
            : _memoryHistoryIndex;
        
        for (int i = 0; i < _memoryHistoryCount; i++)
        {
            int index = (startIndex + i) % MaxDataPoints;
            double x = (double)i / (MaxDataPoints - 1) * GraphWidth;
            double y = GraphHeight - (_memoryHistory[index] / MaxMemoryMb * GraphHeight);
            y = Math.Clamp(y, 0, GraphHeight);
            points.Add(new System.Windows.Point(x, y));
        }

        MemoryGraphPoints = points;
    }

    /// <summary>
    /// Formats a number with K/M suffixes for readability.
    /// </summary>
    /// <param name="number">The number to format.</param>
    /// <returns>A formatted string representation.</returns>
    private static string FormatNumber(int number)
    {
        return number switch
        {
            >= 1_000_000 => $"{number / 1_000_000.0:F1}M",
            >= 1_000 => $"{number / 1_000.0:F1}K",
            _ => number.ToString()
        };
    }

    #endregion
}
