import React, { useEffect, useState } from 'react';
import OfertaImagen from './features/ofertas/components/OfertaImagen.jsx';
import OfertaDetalle from './features/ofertas/components/OfertaDetalle.jsx';
import { listarOfertas, consultarOferta } from './features/ofertas/api/ofertasApi.js';

export default function App() {
  const [id, setId] = useState(null);
  const [resultado, setResultado] = useState(null);
  const data = resultado?.id === id ? resultado.value : null;
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    setResultado(null); setError('');
    (id === null ? listarOfertas(controller.signal) : consultarOferta(id, controller.signal))
      .then(value => { if (!controller.signal.aborted) setResultado({ id, value }); })
      .catch(e => { if (!controller.signal.aborted) setError(e.message); });
    return () => controller.abort();
  }, [id]);
  return <main>
    <header><h1>Ofertas de comida rápida</h1><p>McDonald’s · Burger King · Mostaza</p></header>
    {id !== null && <button onClick={() => setId(null)}>Volver al listado</button>}
    {error ? <p role="alert">{error} Verificá que el backend y MySQL estén disponibles.</p> : data === null ? <p role="status">Cargando ofertas…</p> : id === null ?
      data.length === 0 ? <p>No hay ofertas disponibles.</p> : <section className="grid" aria-label="Ofertas">{data.map(o => <article className="oferta-card" key={o.id}><OfertaImagen key={o.imagen?.url || o.id} oferta={o} /><div className="card-contenido"><p className="marca">{o.marca}</p><h2>{o.nombre}</h2><p>{o.resumen}</p><button onClick={() => setId(o.id)}>Ver detalle</button></div></article>)}</section> :
      <OfertaDetalle key={data.id} oferta={data} />}
  </main>;
}
