import React, { useEffect, useState } from 'react';
import OfertaImagen from './features/ofertas/components/OfertaImagen.jsx';
import OfertaDetalle from './features/ofertas/components/OfertaDetalle.jsx';
import { filtrarOfertas } from './features/ofertas/utils/filtrarOfertas.js';
import { listarOfertas, consultarOferta } from './features/ofertas/api/ofertasApi.js';

function esBancaria(oferta) {
  return oferta.esBancaria === true;
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
  const [marca, setMarca] = useState('');
  const [tipo, setTipo] = useState('todos');
  const [busqueda, setBusqueda] = useState('');
  const catalogo = id === null && Array.isArray(data) ? data : [];
  const marcas = [...new Set(catalogo.map(o => o.marca))];
  const visibles = filtrarOfertas(catalogo, { marca, tipo, busqueda });
  function limpiar() { setMarca('todas'); setTipo('todos'); setBusqueda(''); }

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
      <div className="hero-texto">
        <p className="eyebrow">PROMOCIONES EN UN SOLO LUGAR</p>
        <h1 id="hero-titulo">Encontrá tu<br /><span>próxima oferta.</span></h1>
        <p className="hero-descripcion">Elegí una marca y encontrá el beneficio para tu próxima pausa.</p>
      </div>
      {data !== null && !error && <>
      <section className="filtros" aria-label="Buscar y filtrar ofertas">
        <fieldset className="filtro-marcas"><legend>Elegí una marca</legend><div className="marca-opciones"><button className={marca === 'todas' ? 'filtro-marca activo' : 'filtro-marca'} aria-pressed={marca === 'todas'} onClick={() => setMarca('todas')}>Todas</button>{marcas.map(m => <button key={m} className={marca === m ? 'filtro-marca activo' : 'filtro-marca'} aria-pressed={marca === m} onClick={() => setMarca(m)}>{m}</button>)}</div></fieldset>
        <div className="filtros-campos"><div className="campo-busqueda"><label htmlFor="buscar-ofertas">¿Qué estás buscando?</label><input id="buscar-ofertas" type="search" value={busqueda} onChange={e => setBusqueda(e.target.value)} placeholder="Producto, banco o condición…" maxLength={200} /></div><div className="campo-tipo"><label htmlFor="tipo-beneficio">Tipo de beneficio</label><select id="tipo-beneficio" value={tipo} onChange={e => setTipo(e.target.value)}><option value="todos">Todos los beneficios</option><option value="productos">Promociones de productos</option><option value="bancarias">Beneficios bancarios</option></select></div>{(marca || busqueda || tipo !== 'todos') && <button className="limpiar-filtros secundario" onClick={limpiar}>Limpiar filtros</button>}</div>
      </section>
      </>}
    </section> : <button className="volver" onClick={() => setId(null)}><span aria-hidden="true">←</span> Volver a las ofertas</button>}
    {error ? <div className="estado error" role="alert"><h2>No pudimos cargar las ofertas</h2><p>{error}</p><p>Verificá que el backend esté funcionando.</p></div> : data === null ? <div className="estado" role="status"><span className="cargando" aria-hidden="true" /><p>Buscando las publicaciones…</p></div> : id === null ? <>
      <div className="listado-cabecera"><div><p className="eyebrow">PARA TU PRÓXIMA PAUSA</p><h2>Ofertas publicadas</h2></div><span className="contador" aria-live="polite" aria-atomic="true">{marca ? `${visibles.length} de ${data.length} publicaciones` : `${data.length} disponibles`}</span></div>
      {!marca && data.length > 0 ? <div className="estado"><h3>Elegí por dónde empezar</h3><p>Seleccioná una marca para ver sus ofertas, o elegí Todas para explorar el catálogo.</p></div> : data.length === 0 ? <div className="estado"><h3>Todavía no hay ofertas disponibles</h3><p>Las publicaciones aparecerán cuando se complete su lectura.</p></div> :
      visibles.length === 0 ? <div className="estado" role="status"><h3>No encontramos ofertas con esos filtros</h3><p>Probá otra palabra o cambiá el tipo de beneficio.</p><button className="secundario" onClick={limpiar}>Ver todas las ofertas</button></div> : <section className="grid" aria-label="Ofertas">{visibles.map(o => <article className="oferta-card" key={o.id}>
        <div className="card-visual"><OfertaImagen key={o.imagen?.url || o.id} oferta={o} /><span className="beneficio-badge">{destacado(o)}</span></div>
        <div className="card-contenido"><div className="card-meta"><p className="marca">{o.marca}</p>{esBancaria(o) && <span className="tipo-oferta">Bancaria</span>}</div><h3>{o.nombre}</h3><p className="card-resumen">{o.resumen}</p><button className="card-boton" onClick={() => setId(o.id)} aria-label={`Ver oferta: ${o.nombre}`}>Ver oferta <span aria-hidden="true">↗</span></button></div>
      </article>)}</section>}
    </> : <OfertaDetalle key={data.id} oferta={data} />}
    <footer className="site-footer"><span>Cazador de descuentos</span><p>Antes de usar una oferta, consultá sus condiciones en la publicación original.</p></footer>
  </main>;
}
