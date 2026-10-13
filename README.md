# Centralizador de Descuentos

Proyecto académico para consultar ofertas de McDonald’s, Burger King y Mostaza y acceder a su publicación original.

## Organización
- `backend/`: API modular en C# y ASP.NET Core, con EF Core y MySQL.
- `frontend/`: React, JavaScript y Vite.

Consultar el README de cada carpeta para ejecutar las aplicaciones. MySQL se alojará en Aiven; las credenciales se configuran mediante secretos de usuario, nunca en Git.

## Estado
Base inicial: modelos y migración, API de listado y detalle y pantallas de consulta. Primera lectura automática de promociones bancarias de Mostaza implementada. OCR, documentos, IA, las otras marcas, reportes y registro de aperturas y confirmaciones están pendientes. Sin compras, pagos, delivery ni comparación de ofertas.

## Procedencia
Esta base se traslada desde las ramas `estructura-inicial` de los forks EmilseFerrer/Cazador-de-Descuentos-Fork y EmilseFerrer/Biblioteca-de-Descuentos. Este repositorio reúne ambas aplicaciones; no incluye el historial anterior.
