# Biblioteca de Descuentos — Frontend

React con JavaScript y Vite. Backend: backend/ de este repositorio (C# y MySQL).

## Ejecutar
1. Instalar Node.js 22.12 o superior compatible con Vite 7.
2. Ejecutar `npm install`.
3. Copiar `.env.example` a `.env.local`; ajustar VITE_API_URL si cambia la dirección de la API.
4. Ejecutar `npm run dev` y abrir http://localhost:5173.
5. Mantener el backend ejecutándose en otra terminal. Solo el backend accede a MySQL.

## Verificar
`npm run build` genera la aplicación en dist.

## Alcance de esta etapa
Listado y ficha con carga, vacío y error. No se muestran promociones ficticias. Si el catálogo está vacío, no hay ofertas disponibles. La base del backend debe configurarse siguiendo su README. Apertura con registro, reportes y confirmación implementados; requieren la migración Participacion del backend. No hay claves de IA ni acceso directo a MySQL en el frontend.
