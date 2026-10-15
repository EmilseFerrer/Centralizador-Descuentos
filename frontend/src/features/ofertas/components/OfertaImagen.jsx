import React, { useState } from 'react';

export default function OfertaImagen({ oferta }) {
  const [fallo, setFallo] = useState(false);
  const bancaria = /banco|reintegro|santander|macro|provincia/i.test(oferta.nombre);
  const imagen = oferta.imagen;
  const original = imagen?.url && !fallo;
  return <figure className={`oferta-imagen ${bancaria ? 'bancaria' : 'comida'}`}>
    {original ? <img src={imagen.url} alt={imagen.descripcion || `Imagen de ${oferta.nombre}`} loading="lazy" referrerPolicy="no-referrer" onError={() => setFallo(true)} /> :
      <svg viewBox="0 0 480 280" role="img" aria-label={bancaria ? 'Ilustración de beneficio bancario' : 'Ilustración de comida rápida'}>
        <defs><linearGradient id={`fondo-${oferta.id}`} x2="1" y2="1"><stop stopColor={bancaria ? '#fff1bb' : '#ffe5ce'} /><stop offset="1" stopColor={bancaria ? '#ffd982' : '#ffbd85'} /></linearGradient></defs>
        <rect width="480" height="280" fill={`url(#fondo-${oferta.id})`} />
        <circle cx="380" cy="30" r="125" fill="white" opacity=".2" />
        <ellipse cx="240" cy="235" rx="135" ry="15" fill="#402b1b" opacity=".12" />
        {bancaria ? <g transform="rotate(-9 240 140)"><rect x="105" y="62" width="270" height="165" rx="20" fill="#263e3f" /><rect x="105" y="91" width="270" height="31" fill="#152a2b" /><rect x="137" y="146" width="43" height="32" rx="7" fill="#e8bd59" /><path d="M138 199h78m17 0h42" stroke="#f5f0d9" strokeWidth="8" strokeLinecap="round" /><circle cx="333" cy="185" r="24" fill="#ff794d" /><circle cx="307" cy="185" r="24" fill="#f7bd49" opacity=".8" /></g> : <g><path d="M119 122c4-72 238-72 242 0z" fill="#eea54d" /><path d="M121 129l26 13 27-11 26 12 27-11 26 12 27-11 27 12 29-12 26 13" fill="none" stroke="#62a754" strokeWidth="15" strokeLinejoin="round" /><rect x="118" y="147" width="246" height="30" rx="15" fill="#7d3d26" /><path d="M123 166h236l-44 19-27-9-56 10-42-11-41 9z" fill="#ffd251" /><rect x="121" y="189" width="240" height="29" rx="14" fill="#efa34c" /><g stroke="#fff5ca" strokeWidth="5" strokeLinecap="round"><path d="M180 95l5 3m35-11 5 3m35-2 5-2m30 9 5-2m-54 15 5 2" /></g></g>}
      </svg>}
    <figcaption>{original ? 'Imagen de la publicación original' : 'Imagen ilustrativa'}</figcaption>
  </figure>;
}
