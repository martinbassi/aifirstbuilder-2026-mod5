namespace Paretto.Domain.Enums;

/// <summary>
/// Deliberadamente sin `Published` — FEAT-001b (spec Block 1) nunca mueve un mural a publicado (ver
/// "Out of Scope" del PRD); `Published` lo agrega FEAT-001c cuando implemente la aprobación. No es
/// un enum incompleto, es el alcance de este ticket.
/// </summary>
public enum MuralStatus
{
    Pending = 0,
    Rejected = 1
}
