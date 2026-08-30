# Fix QUICK-FIX-003: Agregar entrada de CHANGELOG pendiente de QUICK-FIX-002

- **Bug**: QUICK-FIX-002 (constructor ambiguo en `IdeUruguayAddressProviderClient`, ya mergeado a
  `main` vía PR #21) cerró sin su entrada de `CHANGELOG.md` — el hook `quickfix_scope_denied` de
  DAW bloqueó esa escritura en su momento porque el diff acumulado del branch ya superaba el techo
  de 10 LOC (`CHANGELOG.md` vive fuera de `docs/daw/`, sin la excepción que sí aplica a los
  artefactos del propio pipeline).
- **Change**: `CHANGELOG.md`, sección `### Fixed` — agregar la entrada de QUICK-FIX-002 (texto ya
  redactado y guardado en memoria durante esa sesión, sin cambios).
- **Regression test**: no aplica — cambio de documentación pura, sin comportamiento que verificar.
- **Risk**: none — solo agrega texto a un archivo de documentación, sin tocar código.
