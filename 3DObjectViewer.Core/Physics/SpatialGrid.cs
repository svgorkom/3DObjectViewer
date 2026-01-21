namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Spatial partitioning grid for efficient broad-phase collision detection.
/// </summary>
/// <remarks>
/// <para>
/// Divides 3D space into a grid of cells. Objects are assigned to cells based
/// on their position and bounding radius. Collision detection only checks
/// objects within the same or neighboring cells.
/// </para>
/// <para>
/// <b>Complexity:</b> Reduces collision detection from O(n²) to O(n) average case
/// for uniformly distributed objects.
/// </para>
/// <para>
/// <b>Memory optimization:</b> Uses object pooling for cell lists to minimize
/// garbage collection pressure.
/// </para>
/// </remarks>
public sealed class SpatialGrid
{
    private readonly Dictionary<long, List<RigidBody>> _grid = [];
    private readonly Stack<List<RigidBody>> _listPool = new();
    private readonly long[] _checkedPairs;
    private int _checkedPairsCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpatialGrid"/> class.
    /// </summary>
    public SpatialGrid()
    {
        _checkedPairs = new long[PhysicsConstants.MaxCheckedPairs];

        // Pre-allocate list pool
        for (int i = 0; i < PhysicsConstants.PreAllocatedLists; i++)
        {
            _listPool.Push(new List<RigidBody>(PhysicsConstants.InitialListCapacity));
        }
    }

    /// <summary>
    /// Clears the grid and resets checked pairs for a new frame.
    /// </summary>
    public void Clear()
    {
        foreach (var list in _grid.Values)
        {
            list.Clear();
        }
        _checkedPairsCount = 0;

        // Trim grid if too large
        if (_grid.Count > PhysicsConstants.MaxGridCells)
        {
            TrimEmptyCells();
        }
    }

    /// <summary>
    /// Builds the spatial grid from a collection of rigid bodies.
    /// </summary>
    /// <param name="bodies">The bodies to add to the grid.</param>
    public void Build(IReadOnlyList<RigidBody> bodies)
    {
        Clear();

        for (int i = 0; i < bodies.Count; i++)
        {
            var body = bodies[i];
            if (body.IsKinematic)
            {
                continue;
            }

            InsertBody(body);
        }
    }

    /// <summary>
    /// Inserts a body into the appropriate grid cells.
    /// </summary>
    /// <param name="body">The body to insert.</param>
    private void InsertBody(RigidBody body)
    {
        var pos = body.Position;
        var radius = body.BoundingRadius;

        // Calculate cell range that the body overlaps
        GetCellCoords(pos.X - radius, pos.Y - radius, pos.Z - radius, out int minX, out int minY, out int minZ);
        GetCellCoords(pos.X + radius, pos.Y + radius, pos.Z + radius, out int maxX, out int maxY, out int maxZ);

        // Insert into all overlapped cells
        for (int cx = minX; cx <= maxX; cx++)
        {
            for (int cy = minY; cy <= maxY; cy++)
            {
                for (int cz = minZ; cz <= maxZ; cz++)
                {
                    var key = GetCellKey(cx, cy, cz);
                    var list = GetOrCreateCell(key);
                    list.Add(body);
                }
            }
        }
    }

    /// <summary>
    /// Iterates over all potential collision pairs in the grid.
    /// </summary>
    /// <param name="onPair">Callback for each potential collision pair.</param>
    public void ForEachPotentialPair(Action<RigidBody, RigidBody> onPair)
    {
        foreach (var cell in _grid.Values)
        {
            int count = cell.Count;
            if (count < 2)
            {
                continue;
            }

            for (int i = 0; i < count; i++)
            {
                var bodyA = cell[i];
                bool bodyAActive = !bodyA.IsAtRest;

                for (int j = i + 1; j < count; j++)
                {
                    var bodyB = cell[j];

                    // Skip if both bodies are at rest
                    if (!bodyAActive && bodyB.IsAtRest)
                    {
                        continue;
                    }

                    // Skip if already checked this pair
                    if (IsPairChecked(bodyA, bodyB))
                    {
                        continue;
                    }

                    MarkPairChecked(bodyA, bodyB);
                    onPair(bodyA, bodyB);
                }
            }
        }
    }

    /// <summary>
    /// Checks if a pair of bodies has already been processed this frame.
    /// </summary>
    private bool IsPairChecked(RigidBody bodyA, RigidBody bodyB)
    {
        long pairId = GetPairId(bodyA, bodyB);

        for (int i = 0; i < _checkedPairsCount; i++)
        {
            if (_checkedPairs[i] == pairId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Marks a pair of bodies as checked for this frame.
    /// </summary>
    private void MarkPairChecked(RigidBody bodyA, RigidBody bodyB)
    {
        if (_checkedPairsCount < PhysicsConstants.MaxCheckedPairs)
        {
            _checkedPairs[_checkedPairsCount++] = GetPairId(bodyA, bodyB);
        }
    }

    /// <summary>
    /// Gets a unique ID for a pair of bodies (order-independent).
    /// </summary>
    private static long GetPairId(RigidBody bodyA, RigidBody bodyB)
    {
        int idA = bodyA.GetHashCode();
        int idB = bodyB.GetHashCode();

        return idA < idB
            ? ((long)idA << 32) | (uint)idB
            : ((long)idB << 32) | (uint)idA;
    }

    /// <summary>
    /// Converts world coordinates to cell coordinates.
    /// </summary>
    private static void GetCellCoords(double x, double y, double z, out int cellX, out int cellY, out int cellZ)
    {
        cellX = (int)Math.Floor(x * PhysicsConstants.InverseCellSize);
        cellY = (int)Math.Floor(y * PhysicsConstants.InverseCellSize);
        cellZ = (int)Math.Floor(z * PhysicsConstants.InverseCellSize);
    }

    /// <summary>
    /// Generates a hash key for a cell coordinate.
    /// </summary>
    private static long GetCellKey(int x, int y, int z)
    {
        unchecked
        {
            return (long)x * PhysicsConstants.HashPrimeX ^
                   (long)y * PhysicsConstants.HashPrimeY ^
                   (long)z * PhysicsConstants.HashPrimeZ;
        }
    }

    /// <summary>
    /// Gets or creates a cell list for the given key.
    /// </summary>
    private List<RigidBody> GetOrCreateCell(long key)
    {
        if (!_grid.TryGetValue(key, out var list))
        {
            list = GetPooledList();
            _grid[key] = list;
        }

        return list;
    }

    /// <summary>
    /// Gets a list from the pool or creates a new one.
    /// </summary>
    private List<RigidBody> GetPooledList()
    {
        return _listPool.Count > 0
            ? _listPool.Pop()
            : new List<RigidBody>(PhysicsConstants.InitialListCapacity);
    }

    /// <summary>
    /// Returns a list to the pool.
    /// </summary>
    private void ReturnListToPool(List<RigidBody> list)
    {
        list.Clear();
        if (_listPool.Count < PhysicsConstants.MaxPooledLists)
        {
            _listPool.Push(list);
        }
    }

    /// <summary>
    /// Removes empty cells from the grid.
    /// </summary>
    private void TrimEmptyCells()
    {
        var keysToRemove = new List<long>();

        foreach (var kvp in _grid)
        {
            if (kvp.Value.Count == 0)
            {
                keysToRemove.Add(kvp.Key);
                ReturnListToPool(kvp.Value);
            }
        }

        foreach (var key in keysToRemove)
        {
            _grid.Remove(key);
        }
    }
}
