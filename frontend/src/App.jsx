import React, { useEffect, useState } from 'react';
import { listarOfertas, consultarOferta } from './features/ofertas/api/ofertasApi.js';

export default function App() {
  const [id, setId] = useState(null);
  const [data, setData] = useState(null);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    setData(null); setError('');
    (id === null ? listarOfertas(controller.signal) : consultarOferta(id, controller.signal))
      .then(setData).catch(e => { if (e.name !== 'AbortError') setError(e.message); });
    return () => controller.abort();
  }, [id]);
  return <main>
    <header><h1>Ofertas de comida rápida</h1><p>McDonald’s · Burger King · Mostaza</p></header>
    {id !== null && <button onClick={() => setId(null)}>Volver al listado</button>}
    {error ? <p role="alert">{error} Verificá que el backend y MySQL estén disponibles.</p> : data === null ? <p role="status">Cargando ofertas…</p> : id === null ?
      data.length === 0 ? <p>No hay ofertas disponibles.</p> : <section className="grid" aria-label="Ofertas">{data.map(o => <article key={o.id}><p>{o.marca}</p><h2>{o.nombre}</h2><p>{o.resumen}</p><button onClick={() => setId(o.id)}>Ver detalle</button></article>)}</section> :
      <article><p>{data.marca}</p><h2>{data.nombre}</h2><p>{data.resumen}</p>
        <h3>Beneficios</h3>{data.beneficios.length ? <ul>{data.beneficios.map((b, i) => <li key={i}>{b.descripcion}</li>)}</ul> : <p>No informados.</p>}
        <h3>Condiciones</h3><p>{data.condiciones || 'No informadas por la fuente.'}</p>
        <p>Vigencia hasta: {data.vigenteHasta || 'No informada'}</p>
        <p>Fuente: {data.fuente}</p>
        <p>Última lectura: {new Date(data.ultimaLecturaUtc + (data.ultimaLecturaUtc.endsWith('Z') ? '' : 'Z')).toLocaleString('es-AR')}</p>
      </article>}
  </main>;
}
