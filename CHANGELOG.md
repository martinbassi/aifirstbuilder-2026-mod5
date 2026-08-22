# Changelog

Todos los cambios notables de este proyecto se documentan en este archivo.

El formato sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.0.0/),
y este proyecto adhiere a [Semantic Versioning](https://semver.org/lang/es/).

## [Unreleased]

### Added

- **FEAT-001a — Autenticación básica**: registro y login de usuarios, sesión server-side (token
  opaco de 256 bits, hasheado con SHA-256, sin JWT), logout con invalidación real de la sesión,
  feature `auth` completa en Angular (formularios, guard de rutas, interceptor de sesión) conectada
  al backend vía cliente NSwag generado. Mitigaciones de seguridad incluidas: mensajes genéricos
  anti-enumeración de cuentas, rate limiting básico en `/login`/`/register`, CSP, elevación de
  privilegios prevenida (el rol nunca se toma del payload del cliente).
- **FEAT-001b — Crear mural**: carga de fotos de murales con geolocalización (automática o manual),
  almacenadas en Azure Storage con SAS de solo lectura de corta duración; validación NSFW
  (NsfwSpy) antes de publicar, dejando el mural `Pending`/`Rejected` según el resultado; validación
  de firma de bytes (magic number) en vez de confiar en `Content-Type`/extensión; límite de tamaño
  de request (~11MB) además del límite de 10MB por foto; formulario Angular con reintento sin
  perder los datos ya ingresados; ruta `/murals/new` protegida por sesión. Ningún mural
  `Pending`/`Rejected` es visible para nadie que no sea su dueño (404 genérico anti-enumeración).
- **FEAT-001c — Moderación mínima**: cierra el ciclo de vida del mural agregando el estado
  `Published`. Un Administrador puede listar murales pendientes (paginado server-side), aprobarlos o
  rechazarlos — los tres endpoints gateados con `[Authorize(Roles = "Administrator")]`, sin chequeo
  de rol manual. Pantalla Angular `/moderation` (guard de administrador, listado con paginación
  Anterior/Siguiente, aprobar/rechazar por ítem). El rol del usuario ahora viaja en la respuesta de
  login, únicamente para gatear la UI — la autorización real siempre se re-verifica server-side.
  Corrige además un `NullInjectorError` preexistente de FEAT-001b (`MuralsClient` nunca se había
  registrado en el injector de Angular).

### Fixed

- **FEAT-001b**: reemplazado `InvariantGlobalization=true` (incompatible con
  `Microsoft.Data.SqlClient`) por `CultureInfo.DefaultThreadCurrentCulture` (ver ADR-004).
  Actualizado `Newtonsoft.Json` a 13.0.4 por una vulnerabilidad High transitiva vía
  `NsfwSpy → Microsoft.ML`.
