import React, { useEffect, useState } from 'react';
import OfertaImagen from './features/ofertas/components/OfertaImagen.jsx';
import OfertaDetalle from './features/ofertas/components/OfertaDetalle.jsx';
import { listarOfertas, consultarOferta } from './features/ofertas/api/ofertasApi.js';

function esBancaria(oferta) {
  return /banco|reintegro|santander|macro|provincia/i.test(oferta.nombre);
}
function destacado(oferta) {
  // Destacar solo lo publicado en el nombre, sin deducir precios o beneficios.
  const porcentaje = oferta.nombre.match(/\d+(?:[.,]\d+)?\s*%/);
  if (porcentaje) return porcentaje[0];
  if (/\b2\s*x\s*1\b/i.test(oferta.nombre)) return '2 × 1';
  return esBancaria(oferta) ? 'Beneficio bancario' : 'Oferta publicada';
}

export default function App() {
  const [id, setId] = useState(null);
  const [resultado, setResultado] = useState(null);
  const data = resultado?.id === id ? resultado.value : null;
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    setResultado(null); setError('');
    window.scrollTo(0, 0);
    (id === null ? listarOfertas(controller.signal) : consultarOferta(id, controller.signal))
      .then(value => { if (!controller.signal.aborted) setResultado({ id, value }); })
      .catch(e => { if (!controller.signal.aborted) setError(e.message); });
    return () => controller.abort();
  }, [id]);
  return <main>
    <header className="site-header">
      <div className="identidad"><span className="logo" aria-hidden="true">%</span><span>Cazador<span className="logo-subtitulo">de descuentos</span></span></div>
      <span className="header-nota">Tu próxima pausa, por menos.</span>
    </header>
    {id === null ? <section className="hero" aria-labelledby="hero-titulo">
      <p className="eyebrow">PROMOCIONES EN UN SOLO LUGAR</p>
      <h1 id="hero-titulo">Encontrá tu<br /><span>próxima oferta.</span></h1>
      <p className="hero-descripcion">El beneficio, las condiciones y la publicación original.<br className="solo-escritorio" /> Todo a mano para elegir tu próxima pausa.</p>
      <div className="marcas" aria-label="Marcas del proyecto"><span>McDonald’s</span><span>Burger King</span><span>Mostaza</span></div>
    </section> : <button className="volver" onClick={() => setId(null)}><span aria-hidden="true">←</span> Volver a las ofertas</button>}
    {error ? <div className="estado error" role="alert"><h2>No pudimos cargar las ofertas</h2><p>{error}</p><p>Verificá que el backend esté funcionando.</p></div> : data === null ? <div className="estado" role="status"><span className="cargando" aria-hidden="true" /><p>Buscando las publicaciones…</p></div> : id === null ? <>
      <div className="listado-cabecera"><div><p className="eyebrow">PARA TU PRÓXIMA PAUSA</p><h2>Ofertas publicadas</h2></div><span className="contador">{data.length} {data.length === 1 ? 'publicación' : 'publicaciones'}</span></div>
      {data.length === 0 ? <div className="estado"><h3>Todavía no hay ofertas disponibles</h3><p>Las publicaciones aparecerán cuando se complete su lectura.</p></div> :
      <section className="grid" aria-label="Ofertas">{data.map(o => <article className="oferta-card" key={o.id}>
        <div className="card-visual"><OfertaImagen key={o.imagen?.url || o.id} oferta={o} /><span className="beneficio-badge">{destacado(o)}</span></div>
        <div className="card-contenido"><div className="card-meta"><p className="marca">{o.marca}</p>{esBancaria(o) && <span className="tipo-oferta">Bancaria</span>}</div><h3>{o.nombre}</h3><p className="card-resumen">{o.resumen}</p><button className="card-boton" onClick={() => setId(o.id)} aria-label={`Ver oferta: ${o.nombre}`}>Ver oferta <span aria-hidden="true">↗</span></button></div>
      </article>)}</section>}
    </> : <OfertaDetalle key={data.id} oferta={data} />}
    <footer className="site-footer"><span>Cazador de descuentos</span><p>Antes de usar una oferta, consultá sus condiciones en la publicación original.</p></footer>
  </main>;
}
