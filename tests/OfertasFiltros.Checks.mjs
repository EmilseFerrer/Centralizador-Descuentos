import assert from 'node:assert/strict';
import { filtrarOfertas } from '../frontend/src/features/ofertas/utils/filtrarOfertas.js';
const ofertas = [
 {id:1,marca:'Burger King',nombre:'Combo 30%',resumen:'Hamburguesa',condiciones:'Locales adheridos',esBancaria:false},
 {id:2,marca:'Mostaza',nombre:'Banco Macro',resumen:'30% de ahorro',condiciones:'Lunes y martes con tarjetas Macro',esBancaria:true},
 {id:3,marca:'Mostaza',nombre:'Banco Provincia',resumen:'Reintegro',condiciones:'Pagando con dinero en Cuenta DNI',esBancaria:true},
 {id:4,marca:'Burger King',nombre:'Café del Banco',resumen:'Café y tostado',condiciones:'Retiro',esBancaria:false},
 {id:5,marca:'Mostaza',nombre:'Publicación sin tipo',resumen:'Texto',condiciones:null}
];
const filtrar=(marca='todas',tipo='todos',busqueda='')=>filtrarOfertas(ofertas,{marca,tipo,busqueda}).map(o=>o.id);
assert.deepEqual(filtrar('', 'todos'),[]);
assert.deepEqual(filtrar('Burger King'),[1,4]);
assert.deepEqual(filtrar('Mostaza','bancarias','Macro lunes'),[2]);
assert.deepEqual(filtrar('todas','bancarias','  cuenta   DNI  '),[3]);
assert.deepEqual(filtrar('Burger King','productos','cafe'),[4]);
assert.deepEqual(filtrar('todas','bancarias'),[2,3]);
assert.deepEqual(filtrar('todas','productos'),[1,4]);
assert.deepEqual(filtrar('todas','todos','inexistente'),[]);
assert.deepEqual(filtrar(),[1,2,3,4,5]);
assert.equal(ofertas.length,5);
console.log('OK: marca, tipo estructurado, búsqueda en condiciones, acentos y combinaciones verificados.');
