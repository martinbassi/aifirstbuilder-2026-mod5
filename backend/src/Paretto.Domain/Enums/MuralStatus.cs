namespace Paretto.Domain.Enums;

/// <summary>
/// `Published` fue agregado por FEAT-001c (spec Block 3) cuando implementó la aprobación admin —
/// hasta entonces (FEAT-001b) ningún mural pasaba de `Pending`/`Rejected`. Sin migración EF Core: la
/// columna `Status` se persiste como `int` plano, sin `HasConversion&lt;string&gt;` ni `CHECK
/// constraint`.
/// </summary>
public enum MuralStatus
{
    Pending = 0,
    Rejected = 1,
    Published = 2
}
