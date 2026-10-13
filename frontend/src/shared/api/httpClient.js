const baseUrl = (import.meta.env.VITE_API_URL || 'http://localhost:5080').replace(/\/$/, '');
async function request(path, options = {}) {
  const response = await fetch(`${baseUrl}${path}`, options);
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.mensaje || (response.status === 404 ? 'La oferta o apertura ya no está disponible.' : 'No se pudo completar la solicitud. Intentá nuevamente.'));
  }
  return response.json();
}
export const get = (path, signal) => request(path, { signal });
export const send = (path, method, body) => request(path, {
  method, headers: { 'Content-Type': 'application/json' }, ...(body === undefined ? {} : { body: JSON.stringify(body) })
});
