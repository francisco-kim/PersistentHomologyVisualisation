namespace PersistentHomologyCore.Models;

public readonly record struct Point3D(double X, double Y, double Z)
{
    public double DistanceSquaredTo(Point3D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        double dz = Z - other.Z;
        return dx * dx + dy * dy + dz * dz;
    }

    public double DistanceTo(Point3D other) => Math.Sqrt(DistanceSquaredTo(other));
}
