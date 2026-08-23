# Threat Model FEAT-002: Identidad visual (Quicksand, logo, paleta de colores)

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-002 |
| Fecha | 2026-08-23 |
| Diseño analizado | 4 bloques: fuente Quicksand self-hosted, paleta vía `ng-zorro-antd.variable.css` + overrides `:root`, componente `LogoComponent` compartido en login/register, favicon |

## Superficies de ataque identificadas

1. **Nuevos assets estáticos servidos:** archivos de fuente (`.woff2`), imagen del logo (`logo.jpg`),
   favicon (`.ico`) — todos servidos desde `public/`, sin lógica de servidor.
2. **Cambio del bundle de CSS de ng-zorro** (`min.css` → `variable.css`) — cambio de build, sin
   lógica nueva.
3. **`LogoComponent` nuevo** — componente presentacional puro, sin `@Input()` que reciba datos
   externos ni interpolación de contenido dinámico.
4. **Modificación de `login-form`/`register-form`** — solo agrega el import de `LogoComponent` y
   markup; no toca la lógica de autenticación existente (`submit()`, validadores, llamadas al
   backend).

## Trust boundary

No se cruza ningún trust boundary nuevo: todos los assets son estáticos, de origen confiable (el
propio build), servidos desde `'self'`. No hay input de usuario ni datos de un origen distinto
involucrados en este cambio.

## Análisis STRIDE

| Categoría | Aplica | Análisis |
|---|---|---|
| **Spoofing** | No | Sin autenticación ni identidad involucrada en este cambio. |
| **Tampering** | No (superficie genérica, no nueva) | Los assets estáticos podrían alterarse solo con acceso de escritura al servidor — riesgo genérico de cualquier deploy, no introducido por este ticket. |
| **Repudiation** | No | Sin acciones de usuario nuevas ni logging afectado. |
| **Information Disclosure** | Sí, ver R1 | Ver riesgo abajo. |
| **Denial of Service** | Sí, ver R2 | Ver riesgo abajo. |
| **Elevation of Privilege** | No | Sin cambios de autorización. |

## Datos sensibles (F-TM-05)

Ninguno nuevo. Los assets (fuente, logo, favicon) son públicos por naturaleza (se sirven a
cualquier visitante sin sesión, igual que hoy). No aplica cifrado adicional (F-TM-07).

## Riesgos

| Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|
| **R1** — El archivo JPEG del logo, provisto por el usuario desde fuera del repo, podría contener metadata EXIF sensible (coordenadas GPS, modelo de cámara, timestamps) que terminaría publicada en el repositorio al commitear la imagen. | I (Information Disclosure) | Baja | Baja (si existiera) | **Verificado antes de escribir este reporte:** el archivo original no tiene metadata EXIF (`Image.getexif()` vacío — es una imagen generada, no una foto de cámara). Aun así, el Bloque 3 recorta/reprocesa la imagen con PIL para el favicon y copia el logo completo al repo — ambos pasos re-guardan el archivo sin preservar metadata (comportamiento por defecto de PIL al hacer `save()`), así que no hay vector de reintroducción. |
| **R2** — El bundle de producción crece por los archivos de fuente + logo, pudiendo superar el presupuesto de build ya ajustado en FEAT-001d (`maximumError: 1.1MB`). Un build que supera el budget de error rompe el pipeline de CI/deploy. | D (Denial of Service, sobre el proceso de build/deploy, no sobre el sistema en producción) | Media | Media | Quicksand WOFF2 (400+700) pesa típicamente ~40-60KB combinados; el logo a 1024×1024 JPEG puede pesar varios cientos de KB si no se optimiza. Mitigación: (a) comprimir/redimensionar el logo a un tamaño de visualización real (ninguna pantalla lo muestra a 1024px) antes de copiarlo a `public/images/`; (b) medir el build de producción al cierre del Bloque 4 y ajustar si se acerca al límite, documentando la decisión igual que se hizo en FEAT-001d. |

## Mitigaciones a incorporar al spec

1. Bloque 3: redimensionar/comprimir el logo a un tamaño de visualización real (no usar el
   1024×1024 original tal cual) antes de copiarlo a `public/images/logo.jpg` (R2).
2. Bloque 4 (cierre): medir `ng build --configuration production` contra el budget existente antes
   de dar el ticket por completo (R2).
3. Ninguna acción adicional para R1 — ya verificado sin EXIF, y el flujo de procesamiento (PIL
   `save()`) no reintroduce metadata.

---

**Total: 0 CRITICAL, 0 HIGH, 0 MEDIUM, 2 LOW (R1 ya verificado/mitigado, R2 mitigado con
compresión + medición en el spec).**

**Resultado: PASSED** — sin riesgos CRITICAL/HIGH; las mitigaciones de los LOW quedan incorporadas
al spec antes de escribirlo a disco.
