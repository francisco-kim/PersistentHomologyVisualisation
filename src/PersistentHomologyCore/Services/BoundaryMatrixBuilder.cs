using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// The signed boundary operator of an explicit complex, as a matrix.
/// <para>
/// Sign convention, with vertex indices ascending:
/// </para>
/// <code>
///     d_k [v_0 ... v_k] = sum_i (-1)^i [v_0 ... v_i-hat ... v_k]
/// </code>
/// <para>
/// Rows are indexed by (k-1)-simplices, columns by k-simplices, both in the
/// complex's lexicographic order - so every <em>column</em> is one simplex's
/// boundary, and every column of the transpose is one simplex's coboundary.
/// </para>
/// </summary>
public static class BoundaryMatrixBuilder
{
    /// <summary>
    /// Builds d_k. Degenerate degrees are returned as correctly-shaped empty
    /// matrices rather than rejected: k == 0 gives 0 x n_0 (the zero map
    /// C_0 -> 0), and k above the complex's top dimension gives n_(k-1) x 0.
    /// Both are meaningful and both are displayed.
    /// </summary>
    public static int[,] Build(SimplicialComplex complex, int k, Coefficients coefficients = Coefficients.Integer)
    {
        ArgumentNullException.ThrowIfNull(complex);
        if (k < 0) throw new ArgumentOutOfRangeException(nameof(k));

        int rows = complex.Count(k - 1);
        int columns = complex.Count(k);
        var matrix = new int[rows, columns];
        if (rows == 0 || columns == 0) return matrix;

        var simplices = complex.ByDimension(k);
        for (int column = 0; column < columns; column++)
        {
            var simplex = simplices[column];
            for (int i = 0; i < simplex.VertexCount; i++)
            {
                int row = complex.IndexOf(simplex.FaceOmitting(i));
                if (row < 0) throw new InvalidOperationException(
                    $"Complex is not closed: {simplex.Label} has a missing face.");

                matrix[row, column] = coefficients == Coefficients.Mod2 ? 1 : (i % 2 == 0 ? 1 : -1);
            }
        }
        return matrix;
    }

    /// <summary>d_k transposed - the coboundary delta^(k-1), mapping cochains up a dimension.</summary>
    public static int[,] Transpose(int[,] matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        var result = new int[columns, rows];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++) result[c, r] = matrix[r, c];
        }
        return result;
    }

    /// <summary>Ordinary matrix product, for asserting d_k . d_(k+1) = 0.</summary>
    public static int[,] Multiply(int[,] left, int[,] right, Coefficients coefficients = Coefficients.Integer)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        int inner = left.GetLength(1);
        if (inner != right.GetLength(0))
            throw new ArgumentException("Inner dimensions do not agree.", nameof(right));

        int rows = left.GetLength(0);
        int columns = right.GetLength(1);
        var result = new int[rows, columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++)
            {
                int sum = 0;
                for (int i = 0; i < inner; i++) sum += left[r, i] * right[i, c];
                result[r, c] = coefficients == Coefficients.Mod2 ? Math.Abs(sum) % 2 : sum;
            }
        }
        return result;
    }
}
