using System;

namespace FloVMP.Core.AntiCheat;

public struct Vector3D
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

    public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
}
