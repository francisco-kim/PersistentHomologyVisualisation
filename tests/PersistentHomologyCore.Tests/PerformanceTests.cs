using System.Diagnostics;
using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyCore.Tests;

public class PerformanceTests
{
    [Fact]
    public void MaxPointCloud_ComputesWithinBudgetAndTimeLimit()
    {
        var points = PresetGenerator.Generate(PresetKind.RandomClusters, count: PresetGenerator.MaxPoints, noise: 0.5, seed: 123);

        var stopwatch = Stopwatch.StartNew();
        var result = PersistenceEngine.Compute(points, maxEpsilon: 260);
        stopwatch.Stop();

        // Generous CI bound (Debug build, interpreted); the published AOT app is far faster.
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"Compute took {stopwatch.Elapsed} for {points.Count} points");

        int totalSimplices = result.Filtration.Simplices.Count;
        Assert.True(totalSimplices <= RipsFiltrationBuilder.DefaultSimplexBudget + points.Count + points.Count * points.Count,
            "simplex count should stay within the budgeted range");
    }
}
