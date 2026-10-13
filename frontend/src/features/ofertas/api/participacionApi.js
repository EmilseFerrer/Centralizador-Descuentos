import { send } from '../../../shared/api/httpClient.js';
export const abrirPublicacion = id => send(`/api/ofertas/${id}/aperturas`, 'POST');
export const reportarOferta = (id, motivo) => send(`/api/ofertas/${id}/reportes`, 'POST', { motivo });
export const confirmarHallazgo = (id, encontrada) => send(`/api/aperturas/${id}/confirmacion`, 'PUT', { encontrada });
