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

## OCR de imágenes y documentos PDF

El adaptador interno `OcrSpaceReader` usa OCR.space mediante POST multipart y la clave
`Ocr:ApiKey` en un encabezado. Se registra por inyección de dependencias en el backend;
no requiere paquetes nativos, una instalación de OCR local ni cambios de MySQL.
Configure la clave desde `backend/src/Cazador.Api`:

```powershell
dotnet user-secrets set "Ocr:ApiKey" "TU_CLAVE"
```

Admite PNG/JPEG y PDF de hasta 1 MB; el plan gratuito limita PDF a 3 páginas.
Conserva el texto por página. Rechaza resultados parciales, errores y documentos sin texto;
no interpreta su ausencia como ausencia de ofertas. No crea promociones directamente:
la identificación de beneficios y condiciones corresponde a la etapa de clasificación.
No envía claves al frontend ni registra respuestas del proveedor en logs.

Validación sin clave, desde la raíz:

```powershell
dotnet run --project .\tests\Ocr.Checks
```

Validación real opcional con un archivo público de promoción (PNG/JPEG/PDF), también desde
la raíz; usa los mismos user-secrets del backend y consume una petición del proveedor:

```powershell
dotnet run --project .\tests\Ocr.Checks -- --archivo "C:\ruta\promocion.png"
```

Esto es una herramienta de comprobación para desarrollo, no una carga manual de ofertas.
**Estado:** adaptador validado con una imagen real y conectado al proceso periódico de
Mostaza mediante `MostazaMaterialReader`. Detecta recursos promocionales PNG/JPEG/PDF del
bloque seleccionado. El recorrido completo con descarga y persistencia se prueba con
fuentes simuladas; falta validar un recurso promocional real de ese bloque. La imagen
antigua de prueba no se incorpora al catálogo. OCR no elude bloqueos HTTP de una fuente.

## Clasificar y resumir con IA

`GeminiOfferReader` es un adaptador interno de Gemini REST; modelo predeterminado
`gemini-3.5-flash-lite`, configurable mediante `Ia:Modelo`. Google publica un nivel
 gratuito limitado para este modelo; la disponibilidad y cuota dependen del proyecto.
No se configura facturación ni se cambia automáticamente a otro modelo.

Guarde `Ia:ApiKey` con user-secrets desde `backend/src/Cazador.Api` (o variable
`Ia__ApiKey` en el entorno). La clave se envía como encabezado y no se expone a React.
El adaptador solicita JSON con esquema, clasifica una publicación, propone resumen,
condiciones y beneficios de los tipos del MVP, conservando el texto recibido.
Los campos desconocidos son null. Los beneficios y requisitos necesitan evidencia
literal. Se comprueban importes/porcentajes y fechas completas; un resultado JSON válido
no garantiza que la interpretación sea correcta. Se conservan condiciones originales
para revisar omisiones y errores de OCR. `requiereRevision=true` indica salida dudosa.
No se acepta respuesta truncada, bloqueo ni cuota agotada como lectura exitosa.

Prueba simulada desde la raíz (no usa claves ni red):

```powershell
dotnet run --project .\tests\Ia.Checks
```

Prueba real OCR → IA con el archivo temporal usado antes:

```powershell
dotnet run --project .\tests\Ia.Checks -- --imagen "$env:TEMP\promocion-mostaza.jpg"
```

Consume una solicitud de OCR y una de Gemini, imprime la propuesta y no persiste ofertas.
En la muestra antigua se espera el beneficio principal de 30%, no el 15% de otros cupones;
no inferir que requiere sesión por el encabezado de la página y no inventar vigencia.
Este archivo no es una fuente de ofertas vigentes ni amplía las fuentes del catálogo.
**Estado:** OCR e IA ya se validaron con claves reales en una imagen de prueba, y la IA
actualiza las cinco publicaciones bancarias de Mostaza en MySQL. La lectura de materiales
se integró al proceso periódico. Sigue pendiente validar un recurso multimedia real de
la fuente seleccionada y cubrir McDonald's. Burger King conserva su lector estructurado.

Las evidencias toleran exclusivamente diferencias de espacios y saltos de línea de OCR.
Las palabras, cifras y puntuación deben coincidir. El error identifica el campo rechazado.
El modelo por defecto se actualizó a 3.5 Flash-Lite porque Google limita modelos 2.5
a proyectos que ya los usaban. `Ia:Modelo` en user-secrets sigue teniendo prioridad.

## Actualización periódica de Mostaza con IA

`Obtencion:Mostaza:UseIa=true` habilita la interpretación automática al iniciar y cada seis
horas mientras el backend esté activo. Se usan los mismos user-secrets `Ia:ApiKey` y
`Ia:Modelo` que en la prueba real. No necesita una migración nueva.

El lector divide la página seleccionada en publicaciones bancarias y mantiene su identidad
original. Gemini procesa cada publicación por separado. Solo se actualizan interpretaciones
completas, sin marca de revisión y con porcentajes y fechas conocidas consistentes con el
lector. El resumen se guarda en la ficha, los requisitos respaldados se registran y las
condiciones originales completas permanecen visibles junto con el texto original.
No se aceptan beneficios nuevos introducidos por IA en esta fuente bancaria.

Si falla la IA, falta la clave o se agota la cuota, las fichas afectadas conservan sus datos
y fecha de lectura anteriores; una publicación nueva no se agrega hasta completar la
interpretación. Las demás publicaciones válidas pueden actualizarse. Las transacciones
se abren después de las llamadas externas. No se borra una oferta por su ausencia.

Una caché limitada a 100 interpretaciones evita volver a llamar a IA para texto y modelo
idénticos dentro de una ejecución del backend; al reiniciar se vuelve a interpretar.
Esta caché no es persistente y un cambio de condiciones requiere una llamada nueva.
El log `Mostaza: ... publicaciones actualizadas (IA: True)` permite verificar la ejecución.
Si se desactiva `UseIa`, se vuelve al extracto literal del lector para desarrollo.

OCR ahora participa en la detección periódica de materiales del bloque seleccionado de
Mostaza. Cuando no hay recursos admitidos, registra cero materiales y sigue actualizando
las publicaciones de texto. La muestra antigua de OCR no se incorpora al catálogo.

En el contexto bancario, la IA resume y clasifica las variantes de una publicación; no
redacta un segundo campo de condiciones. Ese campo mantiene íntegro el texto de origen.
Las evidencias de beneficios, porcentajes y requisitos siguen siendo obligatorias y se
validan. Dos reintegros explícitos según medio de pago pueden pertenecer a una sola
publicación (por ejemplo, Cuenta DNI/NFC); se exige conservar ambos porcentajes.
No se fuerza la aceptación de clasificaciones realmente dudosas.

### Diagnosticar la conexión con Aiven

Desde la raíz, con la misma configuración local del backend:

```powershell
dotnet run --project .\tests\Conexion.Checks
```

Abre una conexión nueva sin pool y ejecuta únicamente `SELECT 1`. No modifica datos,
no llama a IA/OCR y no muestra la cadena ni la contraseña. Si falla, imprime código
MySQL, SQLSTATE y tipo de causa; para errores de red, también SocketErrorCode.
Un resultado correcto comprueba acceso en ese momento y no descarta un fallo intermitente.

El esquema de respuesta bancaria restringe los beneficios a PromocionBancaria,
valor numérico entre 0 y 100 y moneda null. La validación literal y la comparación
contra los porcentajes de origen siguen siendo obligatorias; un tope en pesos no se
acepta como porcentaje.

El backend también registra `Diagnóstico MySQL` cuando EF detecta un fallo de conexión.
Incluye códigos MySQL/SQLSTATE, tipo de causa, código de socket y estado de cancelación;
no copia el mensaje de excepción ni datos de acceso. Los mensajes anteriores de EF se
mantienen. Una cancelación confirmada requiere tanto una excepción de cancelación como
un token cancelado; no se clasifica automáticamente un timeout como una cancelación.

Prueba sin base de datos:

```powershell
dotnet run --project .\tests\Conexion.Checks -- --simulada
```

Este registro sirve para identificar los fallos intermitentes observados. No modifica
credenciales, límites del pool, transacciones ni configura reintentos de escrituras.
La causa de los fallos reales sigue pendiente de comprobar con el entorno del desarrollador.

## Materiales promocionales automáticos

`Obtencion:Mostaza:UseMateriales=true` revisa imágenes y enlaces a PDF del mismo bloque
seleccionado al iniciar y cada seis horas. No recorre todo el sitio. Las imágenes deben
estar identificadas como promocionales en su texto alternativo/título; los enlaces a
PDF deben mencionar promociones, beneficios o condiciones. Logos y recursos sin una
señal promocional no se procesan. Se admiten hasta cinco recursos por lectura.

Solo descarga HTTPS del dominio exacto de Mostaza y verifica cada redirección; admite
PNG, JPEG y PDF de hasta 1 MB. PDF conserva el límite de tres páginas del proveedor.
Aplica OCR y clasificación IA antes de persistir. Conserva texto leído y URL del recurso;
la ficha abre la página seleccionada y avisa que es un destino general.
Identifica cada publicación multimedia por la URL del recurso, normalizando fragmentos.
Si la URL cambia, puede generar una publicación distinta; no compara ofertas ni presume
que dos recursos diferentes sean la misma publicación.

El contenido sin cambios reutiliza una caché acotada a 100 entradas durante la ejecución;
al reiniciar puede consumir nuevamente OCR/IA. Si falla descarga, OCR o interpretación,
se mantiene la ficha multimedia anterior. Las ofertas de texto pueden seguir actualizándose.
Si el bloque textual falla pero el bloque seleccionado aún existe, los materiales se
procesan por separado. No se marca como vencida una publicación por no estar en la respuesta.

Pruebas sin claves ni solicitudes reales:

```powershell
dotnet run --project .	ests\Materiales.Checks
dotnet run --project .	ests\MostazaIa.Checks
```

Validan selección limitada, rechazo de destinos y archivos inválidos, PNG/PDF → OCR → IA,
caché y persistencia sin duplicados. Usan respuestas simuladas; MySQL continúa siendo la
base del sistema. La prueba de persistencia usa SQLite solo en el proyecto de pruebas.
La ausencia de un recurso promocional real en la fuente actual impide afirmar todavía
que ese recorrido fue validado con promociones reales. McDonald's sigue pendiente.

## Imágenes de las ofertas

El catálogo devuelve `imagen` (URL y descripción) cuando existe una imagen asociada a
la publicación. Burger King obtiene su imagen localizada en español del mismo documento
público de la promoción; la URL se conserva en `TextoOriginal` y se extrae al consultar.
No hay migración nueva. Las fichas guardadas antes de este cambio mostrarán una ilustración
hasta que el proceso periódico vuelva a leer Burger King.

El frontend muestra fotografía en listado y detalle, carga diferida y un aviso si no
hay imagen o no carga. Solo las promociones bancarias usan ilustraciones identificadas. Las promociones bancarias usan una
ilustración de tarjeta, sin atribuirles un producto que no está publicado. Un material
promocional de Mostaza que sea imagen conserva esa imagen; un PDF no se trata como foto.
Solo se aceptan las URLs del catálogo de imágenes seleccionado de Burger King y del
dominio de Mostaza. El texto original no se expone en la respuesta del listado.

Validación:

```powershell
dotnet run --project .\tests\Imagenes.Checks
```

McDonald's continúa pendiente de seleccionar y validar una fuente pública concreta.
Encontrar beneficios en un programa de socios no implica que todos los usuarios puedan
utilizarlos ni que las ofertas de la app oficial estén disponibles sin registro.
