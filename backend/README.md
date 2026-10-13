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
Modelo Fuente/Oferta/Beneficio, persistencia y consulta. No hay ofertas precargadas ni carga manual pública. Obtención automática, OCR, documentos, IA, comprobación de enlaces y participación todavía NO están implementados. Carpetas reservadas para próximas etapas; no hay procesos simulados. Faltan confirmar URLs, intervalos y proveedores. Las identidades por publicación deben distinguir varias promociones de una misma página.
