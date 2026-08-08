using PersistentHomologyCore.Models;

namespace PersistentHomologyCore.Services;

/// <summary>
/// Exact ranks of the small integer matrices the boundary explorer produces.
/// No floating point anywhere: a rank that is off by one turns a Betti number
/// into a lie, and these matrices are tiny enough that exactness is free.
/// </summary>
public static class MatrixRank
{
    public static int Compute(int[,] matrix, Coefficients coefficients) =>
        coefficients == Coefficients.Mod2 ? ComputeMod2(matrix) : ComputeRational(matrix);

    /// <summary>
    /// Rank over the rationals, by fraction-free (Bareiss) Gaussian
    /// elimination. Every intermediate entry is a minor of the original
    /// matrix, so the divisions are exact and long is far more headroom than
    /// +-1 boundary entries at these sizes need.
    /// </summary>
    public static int ComputeRational(int[,] matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        if (rows == 0 || columns == 0) return 0;

        var a = new long[rows, columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++) a[r, c] = matrix[r, c];
        }

        int rank = 0;
        long previousPivot = 1;

        for (int column = 0; column < columns && rank < rows; column++)
        {
            int pivotRow = -1;
            for (int r = rank; r < rows; r++)
            {
                if (a[r, column] != 0) { pivotRow = r; break; }
            }
            if (pivotRow < 0) continue;

            if (pivotRow != rank)
            {
                for (int c = 0; c < columns; c++) (a[rank, c], a[pivotRow, c]) = (a[pivotRow, c], a[rank, c]);
            }

            long pivot = a[rank, column];
            for (int r = rank + 1; r < rows; r++)
            {
                long factor = a[r, column];
                for (int c = column + 1; c < columns; c++)
                {
                    a[r, c] = ((a[r, c] * pivot) - (factor * a[rank, c])) / previousPivot;
                }
                a[r, column] = 0;
            }

            previousPivot = pivot;
            rank++;
        }
        return rank;
    }

    /// <summary>Rank over F_2, by bitwise row reduction on the parity of each entry.</summary>
    public static int ComputeMod2(int[,] matrix)
    {
        ArgumentNullException.ThrowIfNull(matrix);
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);
        if (rows == 0 || columns == 0) return 0;

        var a = new bool[rows, columns];
        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < columns; c++) a[r, c] = (matrix[r, c] & 1) != 0;
        }

        int rank = 0;
        for (int column = 0; column < columns && rank < rows; column++)
        {
            int pivotRow = -1;
            for (int r = rank; r < rows; r++)
            {
                if (a[r, column]) { pivotRow = r; break; }
            }
            if (pivotRow < 0) continue;

            if (pivotRow != rank)
            {
                for (int c = 0; c < columns; c++) (a[rank, c], a[pivotRow, c]) = (a[pivotRow, c], a[rank, c]);
            }

            for (int r = 0; r < rows; r++)
            {
                if (r == rank || !a[r, column]) continue;
                for (int c = column; c < columns; c++) a[r, c] ^= a[rank, c];
            }
            rank++;
        }
        return rank;
    }
}
