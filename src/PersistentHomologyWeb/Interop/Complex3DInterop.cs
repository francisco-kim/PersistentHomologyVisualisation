using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;

namespace PersistentHomologyWeb.Interop;

/// <summary>
///     3D rendering for the boundary explorer, following the same zero-copy
///     MemoryView pattern as <see cref="CanvasInterop"/>: geometry is handed to
///     JavaScript as views over WASM memory and projected there.
///     <para>
///     Unlike the simulator's canvas, this module keeps the pushed geometry and
///     the camera angles in JavaScript, so drag-rotation redraws locally without
///     a round trip per frame. C# pushes the complex when it changes and the
///     highlight when the selection changes; nothing else crosses the boundary.
///     </para>
/// </summary>
[SupportedOSPlatform("browser")]
public static partial class Complex3DInterop
{
    public const string ModuleName = "phComplex3d";

    private static Task? _moduleImport;

    public static Task EnsureModuleLoadedAsync() =>
        _moduleImport ??= JSHost.ImportAsync(ModuleName, "../js/phComplex3d.js");

    [JSImport("initCanvas", ModuleName)]
    public static partial void InitCanvas(string canvasId, int width, int height);

    /// <summary>Replaces the drawn complex. Vertex positions are model-space; the module scales to fit.</summary>
    [JSImport("setComplex", ModuleName)]
    public static partial void SetComplex(
        string canvasId,
        [JSMarshalAs<JSType.MemoryView>] Span<double> vertexXyz,
        [JSMarshalAs<JSType.MemoryView>] Span<int> edgePairs,
        [JSMarshalAs<JSType.MemoryView>] Span<int> triangleTriples,
        [JSMarshalAs<JSType.MemoryView>] Span<int> tetrahedronQuads,
        bool solid);

    /// <summary>
    ///     Highlights the selected simplex and its related simplices - its faces
    ///     in the boundary view, its cofaces in the coboundary view. Dimensions
    ///     of -1 clear the respective highlight.
    /// </summary>
    [JSImport("setHighlight", ModuleName)]
    public static partial void SetHighlight(
        string canvasId,
        int selectedDimension,
        int selectedIndex,
        int relatedDimension,
        [JSMarshalAs<JSType.MemoryView>] Span<int> relatedIndices);

    /// <summary>CSS pixel width of the canvas element, for scaling pointer coordinates.</summary>
    [JSImport("getClientSize", ModuleName)]
    public static partial double GetClientSize(string canvasId);

    // attachPicker/detachPicker take a DotNetObjectReference, which is
    // marshalled through the standard Microsoft.JSInterop IJSRuntime path
    // rather than [JSImport] - see the module import in BoundaryOperators.razor.
    // Same constraint as startLoop/stopLoop in CanvasInterop.
}
