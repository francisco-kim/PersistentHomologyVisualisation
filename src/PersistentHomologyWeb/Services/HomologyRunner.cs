using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyWeb.Services;

/// <summary>
/// All simulator state: the point cloud, the current persistence result, and
/// the UI's view of it (epsilon, hover, toggles). Recompute is synchronous
/// and only runs on point-set changes; moving epsilon never touches it.
/// </summary>
public sealed class HomologyRunner
{
    public const int CanvasWidth = 640;
    public const int CanvasHeight = 420;
    public const double MaxEpsilon = 200;

    private readonly List<Point2D> _points = [];
    private PresetKind _currentPreset = PresetKind.NoisyCircle;
    private int _currentCount = 60;
    private readonly Random _seedSource = new();

    public IReadOnlyList<Point2D> Points => _points;
    public PersistenceResult? Result { get; private set; }
    public double Epsilon { get; set; }
    public bool ShowBalls { get; set; } = true;
    public bool ShowAdvancedView { get; set; }
    public bool IsPlaying { get; set; }
    public PersistencePair? HoveredPair { get; set; }
    public bool RemoveMode { get; set; }
    public double PresetNoise { get; set; } = 0.10;
    public int Seed { get; private set; }

    /// <summary>Flattened [x0,y0,x1,y1,...] for zero-copy interop.</summary>
    public double[] PointXy { get; private set; } = [];

    public void AddPoint(Point2D point)
    {
        if (_points.Count >= PresetGenerator.MaxPoints) return;
        _points.Add(point);
        Recompute();
    }

    public bool RemoveNearest(Point2D point, double hitRadius = 12)
    {
        int nearestIndex = -1;
        double nearestDistanceSquared = hitRadius * hitRadius;
        for (int i = 0; i < _points.Count; i++)
        {
            double d2 = _points[i].DistanceSquaredTo(point);
            if (d2 <= nearestDistanceSquared)
            {
                nearestDistanceSquared = d2;
                nearestIndex = i;
            }
        }

        if (nearestIndex < 0) return false;

        _points.RemoveAt(nearestIndex);
        Recompute();
        return true;
    }

    public void LoadPreset(PresetKind kind, int count)
    {
        _currentPreset = kind;
        _currentCount = count;
        Seed = _seedSource.Next();
        _points.Clear();
        _points.AddRange(PresetGenerator.Generate(kind, count, PresetNoise, Seed, CanvasWidth, CanvasHeight));
        Recompute();
    }

    public void Reseed()
    {
        Seed = _seedSource.Next();
        _points.Clear();
        _points.AddRange(PresetGenerator.Generate(_currentPreset, _currentCount, PresetNoise, Seed, CanvasWidth, CanvasHeight));
        Recompute();
    }

    public void Clear()
    {
        _points.Clear();
        Recompute();
    }

    public void Recompute()
    {
        Result = PersistenceEngine.Compute(_points, MaxEpsilon);
        HoveredPair = null;

        PointXy = new double[_points.Count * 2];
        for (int i = 0; i < _points.Count; i++)
        {
            PointXy[2 * i] = _points[i].X;
            PointXy[2 * i + 1] = _points[i].Y;
        }
    }
}
