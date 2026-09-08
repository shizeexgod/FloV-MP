using System;

namespace FloVMP.Core.AntiCheat;

public struct Vector3D : IEquatable<Vector3D>
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }

    public Vector3D(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static readonly Vector3D Zero = new(0f, 0f, 0f);

    public readonly float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

    public float DistanceTo(Vector3D other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        float dz = Z - other.Z;
        return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }

    public float DistanceTo2D(Vector3D other)
    {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public static bool operator ==(Vector3D a, Vector3D b) => a.X == b.X && a.Y == b.Y && a.Z == b.Z;
    public static bool operator !=(Vector3D a, Vector3D b) => !(a == b);
    public override bool Equals(object? obj) => obj is Vector3D other && Equals(other);
    public bool Equals(Vector3D other) => this == other;
    public override int GetHashCode() => HashCode.Combine(X, Y, Z);

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}
