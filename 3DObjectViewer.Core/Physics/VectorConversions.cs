using System.Numerics;
using System.Runtime.CompilerServices;
using System.Windows.Media.Media3D;

namespace _3DObjectViewer.Core.Physics;

/// <summary>
/// Extension methods for converting between WPF 3D types and SIMD-optimized types.
/// </summary>
/// <remarks>
/// <para>
/// These conversions enable using <see cref="Vector3"/> for SIMD-accelerated math
/// while maintaining compatibility with WPF's <see cref="Point3D"/> and <see cref="Vector3D"/>.
/// </para>
/// <para>
/// <b>Usage pattern:</b>
/// <list type="number">
///   <item>Convert WPF types to SIMD types using ToSimdVector3</item>
///   <item>Perform calculations using SIMD operations</item>
///   <item>Convert back to WPF types using ToPoint3D/ToVector3D</item>
/// </list>
/// </para>
/// <para>
/// <b>Note:</b> These conversions involve float/double casting. WPF uses double precision,
/// while System.Numerics.Vector3 uses single precision for SIMD efficiency.
/// </para>
/// </remarks>
public static class VectorConversions
{
    /// <summary>
    /// Converts a WPF <see cref="Point3D"/> to a SIMD-optimized <see cref="Vector3"/>.
    /// </summary>
    /// <param name="point">The WPF point to convert.</param>
    /// <returns>A SIMD Vector3 with the same coordinates.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToSimdVector3(this Point3D point)
    {
        return new Vector3((float)point.X, (float)point.Y, (float)point.Z);
    }

    /// <summary>
    /// Converts a WPF <see cref="Vector3D"/> to a SIMD-optimized <see cref="Vector3"/>.
    /// </summary>
    /// <param name="vector">The WPF vector to convert.</param>
    /// <returns>A SIMD Vector3 with the same components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ToSimdVector3(this Vector3D vector)
    {
        return new Vector3((float)vector.X, (float)vector.Y, (float)vector.Z);
    }

    /// <summary>
    /// Converts a SIMD <see cref="Vector3"/> back to a WPF <see cref="Point3D"/>.
    /// </summary>
    /// <param name="vector">The SIMD vector to convert.</param>
    /// <returns>A WPF Point3D with the same coordinates.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Point3D ToPoint3D(this Vector3 vector)
    {
        return new Point3D(vector.X, vector.Y, vector.Z);
    }

    /// <summary>
    /// Converts a SIMD <see cref="Vector3"/> back to a WPF <see cref="Vector3D"/>.
    /// </summary>
    /// <param name="vector">The SIMD vector to convert.</param>
    /// <returns>A WPF Vector3D with the same components.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3D ToVector3D(this Vector3 vector)
    {
        return new Vector3D(vector.X, vector.Y, vector.Z);
    }
}
