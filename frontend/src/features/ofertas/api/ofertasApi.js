import { get } from '../../../shared/api/httpClient.js';
export const listarOfertas = signal => get('/api/ofertas', signal);
export const consultarOferta = (id, signal) => get(`/api/ofertas/${id}`, signal);
