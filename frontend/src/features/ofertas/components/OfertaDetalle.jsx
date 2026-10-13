import React, { useState } from 'react';
import { abrirPublicacion, reportarOferta, confirmarHallazgo } from '../api/participacionApi.js';

function fecha(value) {
  if (!value) return 'No informada';
  const iso = /(?:Z|[+-]\d{2}:\d{2})$/.test(value) ? value : value + 'Z';
  const date = new Date(iso);
  return Number.isNaN(date.valueOf()) ? 'No informada' : date.toLocaleString('es-AR');
}

export default function OfertaDetalle({ oferta }) {
  const [apertura, setApertura] = useState(null);
  const [abriendo, setAbriendo] = useState(false);
  const [errorApertura, setErrorApertura] = useState('');
  const [respuesta, setRespuesta] = useState(null);
  const [confirmando, setConfirmando] = useState(false);
  const [errorConfirmacion, setErrorConfirmacion] = useState('');
  const [motivo, setMotivo] = useState('vencida');
  const [reportando, setReportando] = useState(false);
  const [reporte, setReporte] = useState('');
  const [errorReporte, setErrorReporte] = useState('');

  async function abrir() {
    setAbriendo(true); setErrorApertura('');
    // Abrir durante el clic evita que el navegador bloquee la ventana por la espera de la API.
    const tab = window.open('about:blank', '_blank');
    if (tab) tab.opener = null;
    try {
      const result = await abrirPublicacion(oferta.id);
      setApertura(result); setRespuesta(null); setErrorConfirmacion('');
      if (tab && !tab.closed) tab.location.href = result.urlDestino;
    } catch (error) {
      if (tab && !tab.closed) tab.close();
      setErrorApertura(error.message);
    } finally { setAbriendo(false); }
  }
  async function confirmar(encontrada) {
    setConfirmando(true); setErrorConfirmacion('');
    try { await confirmarHallazgo(apertura.id, encontrada); setRespuesta(encontrada); }
    catch (error) { setErrorConfirmacion(error.message); }
    finally { setConfirmando(false); }
  }
  async function reportar(event) {
    event.preventDefault(); setReportando(true); setReporte(''); setErrorReporte('');
    try { await reportarOferta(oferta.id, motivo); setReporte('Reporte recibido. Gracias por avisar.'); }
    catch (error) { setErrorReporte(error.message); }
    finally { setReportando(false); }
  }
  return <article>
    <p>{oferta.marca}</p><h2>{oferta.nombre}</h2><p>{oferta.resumen}</p>
    <h3>Beneficios</h3>{oferta.beneficios?.length ? <ul>{oferta.beneficios.map((b, i) => <li key={i}>{b.descripcion}</li>)}</ul> : <p>No informados.</p>}
    <h3>Condiciones</h3><p className="condiciones">{oferta.condiciones || 'No informadas por la fuente.'}</p>
    <p>Vigencia desde: {oferta.vigenteDesde || 'No informada'}</p>
    <p>Vigencia hasta: {oferta.vigenteHasta || 'No informada'}</p>
    <p>Fuente: {oferta.fuente}</p>
    <p>Última lectura: {fecha(oferta.ultimaLecturaUtc)}</p>
    <section aria-label="Publicación original">
      <h3>Publicación original</h3>
      {oferta.esPaginaGeneral === true && <p>El enlace lleva a una página general. Buscá allí el nombre de esta promoción.</p>}
      {oferta.requiereApp === true && <p>El destino requiere la aplicación de la marca.</p>}
      {oferta.requiereSesion === true && <p>El destino requiere iniciar sesión.</p>}
      {oferta.enlaceDisponible === false && <p>El enlace no estaba disponible en la última comprobación. Podés volver a comprobarlo.</p>}
      <p>Última comprobación del enlace: {fecha(apertura?.ultimaComprobacionUtc || oferta.ultimaComprobacionUtc)}.</p>
      <button disabled={abriendo || !oferta.urlOriginal} onClick={abrir}>{abriendo ? 'Comprobando enlace…' : 'Abrir publicación original'}</button>
      {errorApertura && <p role="alert">{errorApertura}</p>}
      {apertura && <>
        <p>Se registró la solicitud de apertura. Si la nueva pestaña no se abrió, <a href={apertura.urlDestino} target="_blank" rel="noopener noreferrer">continuá a la publicación original</a>.</p>
        <h3>¿Encontraste la oferta en el destino?</h3>
        {respuesta === null ? <div className="acciones">
          <button disabled={confirmando} onClick={() => confirmar(true)}>La encontré</button>
          <button disabled={confirmando} onClick={() => confirmar(false)}>No la encontré</button>
        </div> : <p role="status">Respuesta guardada: {respuesta ? 'La encontré' : 'No la encontré'}.</p>}
        {errorConfirmacion && <p role="alert">{errorConfirmacion}</p>}
      </>}
    </section>
    <section aria-label="Reportar oferta">
      <h3>Reportar un problema</h3>
      <form onSubmit={reportar}>
        <label htmlFor="motivo">Motivo</label>
        <select id="motivo" value={motivo} onChange={e => { setMotivo(e.target.value); setReporte(''); }} disabled={reportando}>
          <option value="vencida">Oferta vencida</option>
          <option value="datos_incorrectos">Datos incorrectos</option>
        </select>
        <button disabled={reportando}>{reportando ? 'Enviando…' : 'Enviar reporte'}</button>
      </form>
      {reporte && <p role="status">{reporte}</p>}
      {errorReporte && <p role="alert">{errorReporte}</p>}
    </section>
  </article>;
}
