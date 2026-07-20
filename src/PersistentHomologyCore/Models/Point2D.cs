namespace PersistentHomologyCore.Models;

public readonly record struct Point2D(double X, double Y)
{
    public double DistanceSquaredTo(Point2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return dx * dx + dy * dy;
    }

    public double DistanceTo(Point2D other) => Math.Sqrt(DistanceSquaredTo(other));
}
