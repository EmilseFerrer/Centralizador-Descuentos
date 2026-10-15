import React, { useState } from 'react';

export default function OfertaImagen({ oferta }) {
  const [fallo, setFallo] = useState(false);
  const bancaria = oferta.esBancaria === true || (oferta.esBancaria == null && /banco|reintegro|santander|macro|provincia/i.test(oferta.nombre));
  const imagen = oferta.imagen;
  const original = imagen?.url && !fallo;
  if (!original && !bancaria) {
    return <figure className="oferta-imagen sin-imagen"><p>{fallo ? 'No se pudo cargar la imagen original' : 'Publicación sin imagen disponible'}</p></figure>;
  }
  return <figure className={`oferta-imagen ${bancaria ? 'bancaria' : 'comida'}`}>
    {original ? <img src={imagen.url} alt={imagen.descripcion || `Imagen de ${oferta.nombre}`} loading="lazy" referrerPolicy="no-referrer" onError={() => setFallo(true)} /> :
      <svg viewBox="0 0 480 280" role="img" aria-label="Ilustración de beneficio bancario">
        <defs><linearGradient id={`fondo-${oferta.id}`} x2="1" y2="1"><stop stopColor="#fff1bb" /><stop offset="1" stopColor="#ffd982" /></linearGradient></defs>
        <rect width="480" height="280" fill={`url(#fondo-${oferta.id})`} />
        <circle cx="380" cy="30" r="125" fill="white" opacity=".2" />
        <ellipse cx="240" cy="235" rx="135" ry="15" fill="#402b1b" opacity=".12" />
        <g transform="rotate(-9 240 140)"><rect x="105" y="62" width="270" height="165" rx="20" fill="#263e3f" /><rect x="105" y="91" width="270" height="31" fill="#152a2b" /><rect x="137" y="146" width="43" height="32" rx="7" fill="#e8bd59" /><path d="M138 199h78m17 0h42" stroke="#f5f0d9" strokeWidth="8" strokeLinecap="round" /><circle cx="333" cy="185" r="24" fill="#ff794d" /><circle cx="307" cy="185" r="24" fill="#f7bd49" opacity=".8" /></g>
      </svg>}
    <figcaption>{original ? 'Imagen de la publicación original' : 'Imagen ilustrativa'}</figcaption>
  </figure>;
}
