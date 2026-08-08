using PersistentHomologyCore.Models;
using PersistentHomologyCore.Services;

namespace PersistentHomologyWeb.Services;

/// <summary>
/// All boundary-explorer state: which complex is loaded, how far it is filled
/// in, whether the matrices are shown transposed, and which simplex is
/// selected. Recompute is cheap enough (the complexes have at most 18
/// simplices) that every control change just rebuilds everything.
/// </summary>
public sealed class BoundaryExplorerState
{
    public const int CanvasWidth = 640;
    public const int CanvasHeight = 460;

    // Defaults to the preset with a hole: landing on beta = (1,0,0) would make
    // the readout look broken rather than informative.
    private BoundaryPresetKind _preset = BoundaryPresetKind.TetrahedronWithOpenTriangle;
    private FillLevel _fillLevel = FillLevel.Solid;
    private Coefficients _coefficients = Coefficients.Integer;

    public BoundaryExplorerState() => Recompute();

    public BoundaryPresetKind Preset
    {
        get => _preset;
        set { if (_preset != value) { _preset = value; Recompute(); } }
    }

    public FillLevel FillLevel
    {
        get => _fillLevel;
        set { if (_fillLevel != value) { _fillLevel = value; Recompute(); } }
    }

    public Coefficients Coefficients
    {
        get => _coefficients;
        set { if (_coefficients != value) { _coefficients = value; Recompute(); } }
    }

    private bool _transposed;

    /// <summary>
    /// Show every matrix transposed - the coboundary operators. Purely a change
    /// of view: it does not touch the complex, and by rank(M) = rank(M^T) it
    /// cannot change the Betti numbers either. It does flip what the selection
    /// highlights, from faces to cofaces.
    /// </summary>
    public bool Transposed
    {
        get => _transposed;
        set { if (_transposed != value) { _transposed = value; RecomputeRelated(); } }
    }

    /// <summary>The selected simplex as (dimension, index within that dimension), or null.</summary>
    public (int Dimension, int Index)? Selection { get; private set; }

    public SimplicialComplex Complex { get; private set; } = null!;
    public IReadOnlyList<Point3D> Coordinates { get; private set; } = [];
    public IReadOnlyList<HomologyDimensionInfo> Homology { get; private set; } = [];

    /// <summary>Boundary matrices indexed by degree, 0 through <see cref="TopDegree"/>.</summary>
    private int[][,] _boundaries = [];

    /// <summary>
    /// Highest degree with a matrix worth showing. Always one past the fill
    /// level, so the empty d_(k+1) that kills the top-dimensional cycles is
    /// visible rather than merely implied.
    /// </summary>
    public int TopDegree => (int)_fillLevel;

    /// <summary>
    /// Highest degree to display. One past the fill level, so the empty
    /// d_(k+1) that kills the top-dimensional cycles is visible rather than
    /// merely implied - but never past dimension 3, where there are no
    /// simplices to speak of at all and the empty matrix would say nothing.
    /// </summary>
    public int HighestDisplayedDegree => Math.Min(TopDegree + 1, OrientedSimplex.MaxDimension);

    // --- flattened arrays for zero-copy interop, ordered to match ByDimension ---

    public double[] VertexXyz { get; private set; } = [];
    public int[] EdgePairs { get; private set; } = [];
    public int[] TriangleTriples { get; private set; } = [];
    public int[] TetrahedronQuads { get; private set; } = [];

    public int[,] Boundary(int degree) =>
        degree < 0 || degree >= _boundaries.Length ? new int[0, 0] : _boundaries[degree];

    /// <summary>
    /// The matrix as displayed: d_k, or its transpose when the coboundary view
    /// is on. Transposing d_(k+1) gives delta^k, which is why the displayed
    /// degree label shifts down by one in that view.
    /// </summary>
    public int[,] Displayed(int degree) =>
        Transposed ? BoundaryMatrixBuilder.Transpose(Boundary(degree)) : Boundary(degree);

    /// <summary>Simplices labelling the rows of the displayed matrix at this degree.</summary>
    public IReadOnlyList<OrientedSimplex> DisplayedRows(int degree) =>
        Complex.ByDimension(Transposed ? degree : degree - 1);

    /// <summary>Simplices labelling the columns of the displayed matrix at this degree.</summary>
    public IReadOnlyList<OrientedSimplex> DisplayedColumns(int degree) =>
        Complex.ByDimension(Transposed ? degree - 1 : degree);

    public OrientedSimplex? SelectedSimplex =>
        Selection is { } selection && selection.Index < Complex.Count(selection.Dimension)
            ? Complex.ByDimension(selection.Dimension)[selection.Index]
            : null;

    /// <summary>
    /// What the selection lights up alongside itself: the selected simplex's
    /// faces in the boundary view, its cofaces in the coboundary view. The
    /// asymmetry is the whole point of the transpose toggle - a column of d
    /// lists faces, a column of d^T lists cofaces.
    /// </summary>
    public int RelatedDimension { get; private set; } = -1;

    public int[] RelatedIndices { get; private set; } = [];

    public void Select(int dimension, int index)
    {
        if (dimension < 0 || index < 0 || index >= Complex.Count(dimension)) { ClearSelection(); return; }

        Selection = (dimension, index);
        RecomputeRelated();
    }

    public void ClearSelection()
    {
        Selection = null;
        RelatedDimension = -1;
        RelatedIndices = [];
    }

    /// <summary>
    /// The worked d(d sigma) = 0 expansion for the selection, or null when the
    /// selected simplex is too low-dimensional to have one.
    /// </summary>
    public DoubleBoundaryExpansion? DoubleBoundary =>
        SelectedSimplex is { Dimension: >= 2 } simplex ? DoubleBoundaryExpander.Expand(simplex) : null;

    public void Recompute()
    {
        var preset = BoundaryPresets.Build(_preset);
        Complex = preset.Complex.Restrict((int)_fillLevel);
        Coordinates = preset.Coordinates;
        Homology = SimplicialHomology.Compute(Complex, _coefficients);

        _boundaries = new int[TopDegree + 2][,];
        for (int k = 0; k <= TopDegree + 1; k++)
        {
            _boundaries[k] = BoundaryMatrixBuilder.Build(Complex, k, _coefficients);
        }

        VertexXyz = Flatten(Coordinates);
        EdgePairs = FlattenIndices(Complex.ByDimension(1), 2);
        TriangleTriples = FlattenIndices(Complex.ByDimension(2), 3);
        TetrahedronQuads = FlattenIndices(Complex.ByDimension(3), 4);

        // A selection cannot survive a change of complex: the indices mean
        // something different afterwards.
        ClearSelection();
    }

    /// <summary>
    /// Faces (boundary view) or cofaces (coboundary view) of the selection,
    /// read straight off the matrix so the highlight and the displayed column
    /// can never disagree.
    /// </summary>
    private void RecomputeRelated()
    {
        if (SelectedSimplex is not { } simplex) { RelatedDimension = -1; RelatedIndices = []; return; }

        int dimension = simplex.Dimension;
        int index = Selection!.Value.Index;

        if (!Transposed)
        {
            // Column `index` of d_dimension: its non-zero rows are the faces.
            RelatedDimension = dimension - 1;
            var boundary = Boundary(dimension);
            RelatedIndices = NonZeroRows(boundary, index);
        }
        else
        {
            // Column `index` of delta^dimension = d_(dimension+1)^T: its
            // non-zero entries are the simplices this one is a face of.
            RelatedDimension = dimension + 1;
            var boundary = Boundary(dimension + 1);
            RelatedIndices = NonZeroColumns(boundary, index);
        }

        if (RelatedDimension < 0 || RelatedDimension > TopDegree) RelatedIndices = [];
    }

    private static int[] NonZeroRows(int[,] matrix, int column)
    {
        if (column < 0 || column >= matrix.GetLength(1)) return [];
        var rows = new List<int>();
        for (int r = 0; r < matrix.GetLength(0); r++)
        {
            if (matrix[r, column] != 0) rows.Add(r);
        }
        return [.. rows];
    }

    private static int[] NonZeroColumns(int[,] matrix, int row)
    {
        if (row < 0 || row >= matrix.GetLength(0)) return [];
        var columns = new List<int>();
        for (int c = 0; c < matrix.GetLength(1); c++)
        {
            if (matrix[row, c] != 0) columns.Add(c);
        }
        return [.. columns];
    }

    private static double[] Flatten(IReadOnlyList<Point3D> points)
    {
        var flat = new double[points.Count * 3];
        for (int i = 0; i < points.Count; i++)
        {
            flat[(3 * i) + 0] = points[i].X;
            flat[(3 * i) + 1] = points[i].Y;
            flat[(3 * i) + 2] = points[i].Z;
        }
        return flat;
    }

    private static int[] FlattenIndices(IReadOnlyList<OrientedSimplex> simplices, int stride)
    {
        var flat = new int[simplices.Count * stride];
        for (int i = 0; i < simplices.Count; i++)
        {
            for (int v = 0; v < stride; v++) flat[(i * stride) + v] = simplices[i][v];
        }
        return flat;
    }
}
