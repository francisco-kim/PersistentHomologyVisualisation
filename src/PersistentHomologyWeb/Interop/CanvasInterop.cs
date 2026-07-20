using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace PersistentHomologyWeb.Interop;

/// <summary>
///     Vector canvas rendering: point/simplex coordinates are handed to
///     JavaScript as views over WASM memory (no serialisation) and drawn with
///     the 2D context directly - cheap at a few thousand primitives per frame,
///     unlike per-pixel RGBA blitting.
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class CanvasInterop
{
    public const string ModuleName = "phCanvas";

    private static Task? _moduleImport;

    public static Task EnsureModuleLoadedAsync() =>
        _moduleImport ??= JSHost.ImportAsync(ModuleName, "../js/phCanvas.js");

    [JSImport("initCanvas", ModuleName)]
    public static partial void InitCanvas(string canvasId, int width, int height);

    [JSImport("clearCanvas", ModuleName)]
    public static partial void ClearCanvas(string canvasId);

    /// <summary>Draws balls (radius epsilon/2), filled triangles, then edges, using the epsilon-prefix of each sorted array.</summary>
    [JSImport("drawComplex", ModuleName)]
    public static partial void DrawComplex(
        string canvasId,
        [JSMarshalAs<JSType.MemoryView>] Span<double> pointXy,
        [JSMarshalAs<JSType.MemoryView>] Span<int> edgePairs,
        int edgeCount,
        [JSMarshalAs<JSType.MemoryView>] Span<int> triangleTriples,
        int triangleCount,
        double epsilon,
        bool showBalls);

    [JSImport("drawPoints", ModuleName)]
    public static partial void DrawPoints(string canvasId, [JSMarshalAs<JSType.MemoryView>] Span<double> pointXy);

    /// <summary>Highlights a representative cycle (H1) or cluster (H0) on the overlay layer.</summary>
    [JSImport("drawHighlight", ModuleName)]
    public static partial void DrawHighlight(
        string canvasId,
        [JSMarshalAs<JSType.MemoryView>] Span<double> pointXy,
        [JSMarshalAs<JSType.MemoryView>] Span<int> highlightVertices,
        [JSMarshalAs<JSType.MemoryView>] Span<int> highlightEdgePairs);

    /// <summary>CSS pixel width of the canvas element, for scaling click coordinates back to logical space.</summary>
    [JSImport("getClientSize", ModuleName)]
    public static partial double GetClientSize(string canvasId);

    // startLoop/stopLoop (the requestAnimationFrame driver for the play
    // button, added in Phase 5) take a DotNetObjectReference, which is
    // marshalled through the standard Microsoft.JSInterop IJSRuntime path
    // rather than [JSImport] - see the module import in Simulator.razor.
}
