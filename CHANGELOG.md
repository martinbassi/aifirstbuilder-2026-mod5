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
