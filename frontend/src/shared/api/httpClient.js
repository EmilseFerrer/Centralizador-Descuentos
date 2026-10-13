const baseUrl = (import.meta.env.VITE_API_URL || 'http://localhost:5080').replace(/\/$/, '');
export async function get(path, signal) {
  const response = await fetch(`${baseUrl}${path}`, { signal });
  if (!response.ok) throw new Error(response.status === 404 ? 'La oferta ya no está disponible.' : 'No se pudo consultar el catálogo.');
  return response.json();
}
