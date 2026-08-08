namespace PersistentHomologyCore.Models;

/// <summary>
/// Which ring the boundary matrices and their ranks are computed over.
/// <see cref="Integer"/> keeps the +-1 orientation signs; <see cref="Mod2"/>
/// collapses them to 0/1, matching the convention the persistence path uses.
/// </summary>
public enum Coefficients
{
    Integer,
    Mod2
}
