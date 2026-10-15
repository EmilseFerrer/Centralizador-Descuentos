export function normalizarBusqueda(value) {
  return String(value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase();
}

export function filtrarOfertas(ofertas, { marca, tipo, busqueda }) {
  if (!marca) return [];
  const palabras = normalizarBusqueda(busqueda).trim().split(/\s+/).filter(Boolean);
  return ofertas.filter(oferta => {
    if (marca !== 'todas' && oferta.marca !== marca) return false;
    if (tipo === 'bancarias' && oferta.esBancaria !== true) return false;
    if (tipo === 'productos' && oferta.esBancaria !== false) return false;
    const texto = normalizarBusqueda([oferta.nombre, oferta.resumen, oferta.marca, oferta.condiciones].join(' '));
    return palabras.every(palabra => texto.includes(palabra));
  });
}
