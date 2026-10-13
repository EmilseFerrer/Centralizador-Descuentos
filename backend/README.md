# Cazador de Descuentos — Backend

ASP.NET Core Web API en C#, monolito modular con Catálogo, Obtención y Participación. Frontend: frontend/ de este repositorio.

## Requisitos
.NET SDK 10, MySQL 8 y acceso a NuGet. Se utiliza EF Core 9 con Pomelo 9; no actualizar solo una de esas dependencias sin verificar compatibilidad.

## Ejecutar
1. Abrir `src/Cazador.Api/Cazador.Api.csproj` en Visual Studio compatible con .NET 10 o usar la CLI.
2. Crear una base vacía en MySQL llamada `descuentos` y un usuario con permisos sobre esa base.
3. Desde la carpeta del proyecto configurar la conexión local (reemplazar valores):
   `dotnet user-secrets set "ConnectionStrings:MySql" "Server=localhost;Database=descuentos;User=TU_USUARIO;Password=TU_CLAVE"`
4. Ajustar `MySql:ServerVersion` a la versión del servidor si corresponde.
5. Instalar herramienta EF compatible: `dotnet tool install --global dotnet-ef --version 9.0.0`.
6. Aplicar la migración inicial incluida: `dotnet ef database update`.
7. Ejecutar: `dotnet run --urls http://localhost:5080`.

La conexión también se puede configurar mediante `ConnectionStrings__MySql`. No subir contraseñas al repositorio. La migración inicial ya está incluida. Las futuras migraciones deben revisarse y versionarse. La aplicación no crea ni modifica automáticamente la base al arrancar.

## Contrato inicial
- GET /api/health: disponibilidad del proceso, no comprueba MySQL.
- GET /api/ofertas: lista de ofertas; fechas de vencimiento desconocidas se conservan.
- GET /api/ofertas/{id}: ficha o 404 si no existe o venció.

Las fechas límite se interpretan con el día de Argentina. Los registros de lectura se guardarán en UTC.

## Alcance de esta etapa
Modelo Fuente/Oferta/Beneficio, persistencia y consulta. No hay ofertas precargadas ni carga manual pública. Obtención automática implementada para los bloques bancarios HTML de Mostaza y el catálogo público de Burger King descritos abajo. OCR, documentos e IA todavía NO están implementados. Participación y comprobación HTTP de enlaces implementadas como se describe abajo. Carpetas reservadas para próximas etapas; no hay procesos simulados. Faltan confirmar URLs, intervalos y proveedores. Las identidades por publicación deben distinguir varias promociones de una misma página.

## Primera obtención automática: Mostaza
Fuente seleccionada: https://www.mostazaweb.com.ar/bases-y-condiciones-promos/. Se leen los bloques bancarios en HTML, conservando todas sus condiciones y sus variantes porcentuales. No se importan concursos ni la página de cupón que actualmente muestra un error. Fechas sin informar permanecen desconocidas; no se confunde el tope de reintegro con un precio o un descuento fijo.

El proceso se ejecuta al iniciar el backend y cada 6 horas mientras esté funcionando. Intervalo propuesto y configurable en Obtencion:Mostaza; Enabled permite desactivarlo. No requiere una nueva migración. Ejecutar una sola instancia del backend para esta etapa. Si la lectura o la persistencia falla, se registra el tipo de fallo y se conserva el catálogo anterior. No se retiran ofertas por una mera ausencia en la página; las fechas conocidas vencidas se excluyen de la consulta existente. La identidad depende del encabezado normalizado: un cambio de encabezado puede producir otra publicación y queda pendiente mejorar esa conciliación.

Esta etapa cubre exclusivamente texto bancario de Mostaza: OCR, documentos, IA y demás marcas siguen pendientes. El resumen es un extracto literal, no una interpretación por IA. La fecha de última lectura acredita lectura del contenido, no garantiza el canje.

Para iniciar desde PowerShell con secretos de usuario:
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
dotnet run --no-launch-profile --urls http://localhost:5080
```
La consola debe registrar el número de publicaciones actualizadas. Consultar /api/ofertas después de finalizar la lectura.

Validar el lector desde la raíz: `dotnet run --project tests/MostazaReader.Checks`. Comprueba segmentación, variantes, fechas, identidad y rechazo de contenido inválido sin conectarse a MySQL.

## Aperturas, reportes y confirmación (HU08–HU10)
- POST `/api/ofertas/{id}/aperturas`: comprueba por HTTP el destino oficial HTTPS, registra la solicitud de apertura y devuelve identificador y URL. No acredita que el navegador haya cargado la oferta o que se haya canjeado. Se permiten hasta cinco redirecciones entre dominios oficiales admitidos. Los errores de red se informan y no se crea una apertura. Un 401/403 no se interpreta automáticamente como requisito de sesión. La comprobación HTTP acredita accesibilidad, no presencia ni vigencia de una promoción dentro de una página general.
- POST `/api/ofertas/{id}/reportes`: recibe motivo `vencida` o `datos_incorrectos`, guarda el reporte sin alterar la oferta y confirma recepción.
- PUT `/api/aperturas/{id}/confirmacion`: recibe `encontrada` booleano obligatorio. Ausencia de respuesta se mantiene nula. Repetir la misma respuesta no agrega registros ni cambia su fecha.

React muestra los avisos conocidos, abre una nueva pestaña y presenta la pregunta de hallazgo en la ficha después de la solicitud de apertura. Si el navegador bloquea la pestaña hay un enlace para continuar. La respuesta queda vinculada a esa apertura. No requiere cuentas de usuario ni recoge datos personales. No se incluye una pantalla de gestión de reportes.

Antes de iniciar esta versión, aplicar la migración `Participacion` desde la carpeta del backend, después de detener el proceso:
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:DOTNET_ROLL_FORWARD = "Major"
.\.ef-tools\dotnet-ef.exe database update
dotnet run
```
La herramienta local EF9 se instaló anteriormente; si no existe, usar `dotnet tool install dotnet-ef --version 9.0.0 --tool-path ./.ef-tools`. No borrar ni recrear la base.

Desde la raíz, `dotnet run --project tests/Participacion.Checks` prueba persistencia en SQLite temporal y validación de entradas, apertura de oferta vencida, destino caído, fallo de red, reporte sin retiro y confirmación idempotente. No reemplaza la validación de la migración en MySQL Aiven.

## Segunda fuente: Burger King Argentina
Página seleccionada: https://burgerking.com.ar/es/rewards/offers. Su HTML es una aplicación JavaScript. El lector usa las consultas públicas de disponibilidad y contenido que la propia web utiliza, sin cuenta, tokens de usuario ni inicio de sesión. Los endpoints se identificaron en sus recursos públicos: gateway `use2-prod-bk-gateway.rbictg.com/graphql` y contenido Sanity del proyecto `czqk28jt`, dataset `prod_bk_ar`. Si cambian, la lectura falla y conserva los registros anteriores. No se extraen borradores, entradas de prueba ni publicaciones exclusivas de delivery identificadas por modalidad o texto.

Se relaciona el identificador de disponibilidad con el identificador de publicación y se usa contenido en español. Se extraen porcentajes y precios explícitos en el nombre, y códigos publicados. Otros beneficios se conservan como descripción original, sin inventar precios ni calcular descuentos. Las fechas técnicas del año 9999 se muestran como no informadas. Los timestamps originales UTC quedan conservados en el texto original; las condiciones muestran las fechas normalizadas; el catálogo usa las fechas calendario UTC publicadas, no horas exactas de caducidad. El texto original conserva la respuesta de origen. La extracción no prueba stock, disponibilidad local ni canje.

Obtencion:BurgerKing habilita la fuente e intervalo (propuesto: 6 horas); corre al iniciar y luego periódicamente mientras la API está activa, independientemente de Mostaza. Se procesa el conjunto devuelto por la consulta pública seleccionada, no se garantiza descubrir todas las ofertas de la marca. Una publicación que desaparece de una lectura no se da por vencida automáticamente. Falta implementar una regla de conciliación de esas ausencias. Requisitos de app o sesión se mantienen desconocidos si la fuente no permite confirmarlos; la página original se entrega como general. La comprobación del enlace se realiza al abrirlo, no se confunde el éxito de la API de contenido con el de la página.

No requiere migración adicional. Desde la raíz: `dotnet run --project tests/BurgerKingReader.Checks`. La opción `-- --live` consulta la fuente pública actual desde .NET sin escribir en MySQL; requiere internet. Se probaron porcentajes, precios argentinos, cupones, beneficios sin monto, fecha técnica, exclusión de pruebas/vencidas/delivery y errores de respuesta.

McDonald’s sigue pendiente: durante la inspección su página respondió 403 y no se confirmó un catálogo público legible. No se usan promociones inventadas ni se eluden accesos privados.
