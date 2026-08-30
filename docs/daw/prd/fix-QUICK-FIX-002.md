# Fix QUICK-FIX-002: Constructor ambiguo en IdeUruguayAddressProviderClient bajo AddHttpClient

- **Bug**: al levantar el backend y navegar al formulario de carga de mural, cualquier llamada a
  `/api/addresses/search` o `/api/addresses/reverse` falla con
  `System.InvalidOperationException: A suitable constructor for type 'IdeUruguayAddressProviderClient'
  could not be located ... Multiple constructors accepting all given argument types have been found`.
  `Program.cs` registra el cliente con `AddHttpClient<IAddressProviderClient,
  IdeUruguayAddressProviderClient>(...)`, cuyo mecanismo de activación (`ActivatorUtilities` con el
  `HttpClient` pasado explícitamente) no logra desambiguar entre el constructor de 2 argumentos
  (`HttpClient, ILogger`) y el de 3 (`HttpClient, ILogger, TimeSpan`) — a diferencia de `AddScoped`
  (usado por `NsfwSpyContentScanner`, con la misma forma de 2 constructores, que sí resuelve bien
  porque su algoritmo de selección descarta constructores cuyos parámetros extra no están
  registrados en el contenedor). Encontrado en prueba manual del usuario tras cerrar FEAT-011; ningún
  test lo detectó porque `AddressesControllerTests` construye el cliente indirectamente a través del
  pipeline HTTP real de `WebApplicationFactory` sin ejercitar el path exacto de activación del typed
  client con ambigüedad, y `IdeUruguayAddressProviderClientTests` instancia la clase directamente con
  `new(...)`, sin pasar por el contenedor de DI en absoluto.
- **Change**: `backend/src/Paretto.Infrastructure/Geocoding/IdeUruguayAddressProviderClient.cs` —
  agregar el atributo `[ActivatorUtilitiesConstructor]` (de
  `Microsoft.Extensions.DependencyInjection`) al constructor de 2 argumentos, indicándole
  explícitamente a `ActivatorUtilities` cuál usar en la activación vía `AddHttpClient`. El
  constructor de 3 argumentos se mantiene sin cambios para el uso directo desde los tests
  (`IdeUruguayAddressProviderClientTests`, que ya lo invocan con `new(...)` y seguirán haciéndolo).
- **Regression test**: nuevo test de integración en `AddressesControllerTests.cs` que golpea
  `GET /api/addresses/search?q=...` a través de la pila real de DI de `WebApplicationFactory` (sin
  mockear `IAddressProviderClient`, dejando que el contenedor real construya
  `IdeUruguayAddressProviderClient` vía `AddHttpClient` tal cual lo arma `Program.cs`) — reproduce el
  `InvalidOperationException` antes del fix y pasa después. Ningún test existente ejercitaba este
  camino: todos usaban un `FakeAddressProviderClient` inyectado por `WebApplicationFactory.
  ConfigureTestServices`, que nunca pasa por la activación real de `AddHttpClient`.
- **Risk**: none — el atributo `[ActivatorUtilitiesConstructor]` es parte del framework
  (`Microsoft.Extensions.DependencyInjection.Abstractions`, ya referenciado transitivamente por
  ASP.NET Core), no agrega dependencias nuevas. No cambia comportamiento observable: mismo
  `HttpClient`/`ILogger` inyectados, mismo timeout de 5s configurado en `Program.cs` para producción;
  el constructor de 3 args sigue disponible para los tests que necesitan un timeout corto inyectado.
