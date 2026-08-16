namespace Paretto.Domain.Enums;

/// <summary>
/// El PRD (docs/daw/prd/prd-FEAT-001a.md, FR-07) nombra el rol por defecto "Colaborador/Explorador"
/// en la jerga de producto. `Standard` es la traducción de código acordada en PLAN (ver spec Block 3).
/// </summary>
public enum UserRole
{
    Standard = 0,
    Administrator = 1
}
