# Plan de pruebas previo a subir RinconV2 al VPS

## Objetivo

Determinar si el código, las migraciones y el paquete Release están listos para ser desplegados. Este plan se ejecuta completamente en desarrollo o preproducción, antes de modificar el VPS.

Quedan fuera de este documento Nginx, `systemd`, dominios, certificados, firewall y pruebas posteriores al despliegue.

## Resultado esperado

Al finalizar deben existir:

- Un paquete Release aprobado.
- Un script idempotente de migraciones revisado.
- Un backup local restaurable.
- Evidencia de las pruebas funcionales críticas.
- Cero errores abiertos relacionados con dinero, stock, cuentas o permisos.
- Una decisión explícita: **LISTO PARA SUBIR** o **NO LISTO**.

## 1. Congelar el candidato

- [ ] Definir el commit o copia exacta que se probará.
- [ ] No agregar funciones nuevas durante esta ronda.
- [ ] Registrar fecha, responsable y versión.
- [ ] Separar defectos bloqueantes de mejoras futuras.
- [ ] Confirmar que los cambios pendientes en Git son conocidos y pertenecen a RinconV2.

| Dato | Valor |
|---|---|
| Fecha | |
| Responsable | |
| Commit/versión | |
| Base de prueba | |

## 2. Controles automáticos

Ejecutar:

```powershell
dotnet restore Rincon.sln
dotnet build Rincon.sln -c Release --no-restore
dotnet build tests/AuditChecks/AuditChecks.csproj -c Release --no-restore
dotnet publish Rincon/Rincon.csproj -c Release --no-restore -o artifacts/publish
```

- [x] Restauración de dependencias correcta.
- [x] Compilación Release: 0 errores y 0 advertencias.
- [x] Proyecto `AuditChecks`: 0 errores y 0 advertencias.
- [x] Publicación Release generada correctamente.
- [x] La publicación inicia en ambiente Production con configuración privada local.
- [x] `/health/live` responde HTTP 200.
- [x] `/health/ready` responde HTTP 200 contra PostgreSQL local.
- [x] No se detectaron contraseñas ni cadenas reales en `appsettings`.

Estos resultados fueron ejecutados el 28/09/2026. Deben repetirse si cambia código posteriormente.

## 3. Preparar una base de prueba realista

- [ ] Crear un backup de la base local antes de probar migraciones.
- [ ] Restaurarlo en una base separada, por ejemplo `RinconV2_PreProd`.
- [ ] Nunca realizar esta prueba destructiva sobre la única copia de datos.
- [ ] Registrar cantidades iniciales de productos, ventas, cuentas, pagos y gastos.
- [ ] Confirmar que la aplicación apunta a la base de prueba.

Consultas de control:

```sql
SELECT COUNT(*) AS productos FROM "Products";
SELECT COUNT(*) AS ventas FROM "DirectSales";
SELECT COUNT(*) AS cuentas FROM "PersonalAccounts";
SELECT COUNT(*) AS pagos FROM "PersonalAccountPayments";
SELECT COUNT(*) AS gastos FROM "Expenses";
```

## 4. Validar migraciones

Generar y revisar el SQL antes de aplicarlo:

```powershell
dotnet ef migrations list --project Rincon.DataAccess --startup-project Rincon
dotnet ef migrations script --idempotent --project Rincon.DataAccess --startup-project Rincon -o artifacts/migrations-pre-vps.sql
```

- [ ] La lista contiene todas las migraciones esperadas.
- [ ] El script se genera sin errores.
- [ ] Se revisaron especialmente las operaciones `DROP`.
- [ ] La limpieza de recambios elimina únicamente columnas obsoletas.
- [ ] `OpeningBalance` se agrega como `numeric(18,2)` con valor predeterminado `0`.
- [ ] Aplicar el script sobre `RinconV2_PreProd`.
- [ ] Ejecutarlo nuevamente; no debe duplicar cambios ni fallar.
- [ ] Las cuentas existentes conservan saldo inicial cero.
- [ ] Las cantidades de registros coinciden con las registradas antes de migrar.
- [ ] La aplicación inicia contra la base migrada.

Controles posteriores:

```sql
SELECT COUNT(*) FROM "PersonalAccounts" WHERE "OpeningBalance" < 0;
SELECT COUNT(*) FROM "Products" WHERE "Quantity" < 0;
SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
```

Los dos primeros resultados deben ser `0`.

## 5. Usuarios y permisos

- [ ] Administrador puede iniciar y cerrar sesión.
- [ ] Empleado activo puede iniciar sesión.
- [ ] Usuario inactivo no puede ingresar.
- [ ] Desactivar un usuario invalida su acceso.
- [ ] Empleado no puede ejecutar acciones exclusivas de administrador.
- [ ] Las páginas protegidas redirigen al login.
- [ ] Los errores mostrados no revelan stack traces, cadenas ni contraseñas en ambiente Production.

## 6. Productos y stock

- [ ] Crear un producto con costo, precio, cantidad e imagen.
- [ ] Editar nombre, costo y precio sin cambiar stock accidentalmente.
- [ ] Ajustar stock con motivo obligatorio.
- [ ] Rechazar stock negativo.
- [ ] Desactivar un producto y confirmar que deja de ofrecerse en ventas.
- [ ] Confirmar que cada cambio de stock registra cantidad anterior, nueva, motivo y usuario.
- [ ] Confirmar que editar un producto desactualizado no pisa una venta registrada mientras tanto.

## 7. Ventas

Usar productos y valores simples que puedan calcularse manualmente.

- [ ] Registrar venta en efectivo.
- [ ] Registrar venta por transferencia.
- [ ] Registrar venta combinada.
- [ ] Rechazar pago combinado si sus partes no coinciden con el total.
- [ ] Registrar venta en cuenta personal.
- [ ] Confirmar descuento exacto de stock.
- [ ] Rechazar cantidad mayor al stock disponible.
- [ ] Rechazar cantidad cero o negativa.
- [ ] Rechazar venta sin productos.
- [ ] Reenviar el mismo formulario y confirmar que no duplica la venta.
- [ ] Cambiar posteriormente el precio del producto y confirmar que la venta conserva valores históricos.

## 8. Anulación total y parcial

Crear una venta con dos remeras y una manta.

- [ ] La única acción disponible es **Anular venta**.
- [ ] Anular una remera con **Devuelve al stock** marcado.
- [ ] El stock aumenta exactamente una unidad.
- [ ] La venta queda como **Anulación parcial**.
- [ ] Anular otra unidad sin devolverla al stock.
- [ ] El stock no cambia para esa unidad.
- [ ] Usar **Seleccionar todo** para anular las cantidades restantes.
- [ ] La venta queda como **Anulada**.
- [ ] No se puede anular nuevamente una cantidad ya anulada.
- [ ] Reenviar una anulación y confirmar que no se duplica.
- [ ] Cada movimiento conserva fecha, usuario, motivo, cantidad y decisión de stock.
- [ ] No existe en ninguna pantalla un flujo independiente de devolución o recambio.
- [ ] Simular un cambio anulando el artículo anterior y creando una venta nueva.

Control monetario:

- [ ] Anulación en efectivo reduce efectivo.
- [ ] Anulación por transferencia reduce transferencia.
- [ ] Anulación en cuenta personal reduce deuda.
- [ ] Una cuenta ya saldada permanece en cero y no genera saldo a favor.
- [ ] La utilidad se reduce por el margen del producto anulado.

## 9. Cuentas personales y saldo inicial

- [ ] Crear una cuenta sin saldo anterior.
- [ ] Crear otra con saldo anterior de `$20.000`.
- [ ] La deuda de la segunda cuenta comienza en `$20.000`.
- [ ] No se crea una venta ficticia ni un producto asociado.
- [ ] El detalle muestra **Saldo inicial cargado**.
- [ ] Registrar pago de `$5.000`; debe quedar `$15.000`.
- [ ] Registrar venta posterior de `$10.000`; debe quedar `$25.000`.
- [ ] Registrar pago de `$20.000`; debe quedar `$5.000`.
- [ ] Intentar pagar más de lo adeudado; debe rechazarse.
- [ ] Los pagos se imputan primero al saldo inicial y luego a ventas.
- [ ] El saldo inicial aparece en pendiente de cobro.
- [ ] El saldo inicial no afecta ventas, stock ni utilidad.
- [ ] Editar nombre, dirección o teléfono no modifica el saldo inicial.
- [ ] Las cuentas anteriores a la migración tienen saldo inicial cero.
- [ ] Las anulaciones aparecen en el historial de la cuenta.

## 10. Gastos y retiros

- [ ] Registrar compra de mercadería en efectivo.
- [ ] Registrar gasto del negocio por transferencia.
- [ ] Registrar retiro personal.
- [ ] Cada movimiento afecta el medio correspondiente.
- [ ] Rechazar importe cero, negativo o con más de dos decimales.
- [ ] Reenviar el formulario y confirmar que no duplica el gasto.
- [ ] Anular un gasto y verificar que deja de impactar.

## 11. Balance

Preparar un período con números conocidos y calcularlo manualmente.

- [ ] Ventas cobradas coincide con ventas inmediatas netas.
- [ ] Cobros de cuentas coincide con pagos recibidos en el período.
- [ ] Pendiente de cobro incluye ventas a cuenta y saldos iniciales.
- [ ] Saldo disponible coincide con ingresos menos anulaciones, compras, gastos y retiros.
- [ ] Efectivo más transferencia coincide con saldo disponible.
- [ ] Utilidad es precio menos costo y descuenta anulaciones.
- [ ] Saldo inicial no suma ventas ni utilidad.
- [ ] Cambiar las fechas produce resultados consistentes.

Registrar en una hoja los valores esperados y observados:

| Indicador | Esperado | Observado | Resultado |
|---|---:|---:|---|
| Ventas cobradas | | | |
| Cobros de cuentas | | | |
| Pendiente de cobro | | | |
| Efectivo disponible | | | |
| Transferencia disponible | | | |
| Utilidad estimada | | | |

## 12. Concurrencia e integridad

- [ ] Dos ventas simultáneas de la última unidad permiten solo una.
- [ ] Dos pagos simultáneos no cobran más que la deuda.
- [ ] Dos anulaciones simultáneas no anulan dos veces la misma cantidad.
- [ ] Un error durante una operación no deja cambios parciales.
- [ ] Ningún producto termina con stock negativo.
- [ ] No existen ventas, pagos, gastos o anulaciones duplicados por doble clic.

## 13. Interfaz y recorridos completos

Probar en escritorio y en una pantalla móvil.

- [ ] No hay textos cortados, botones superpuestos ni tablas inutilizables.
- [ ] Todos los importes usan formato argentino correcto.
- [ ] Fechas y horas son correctas.
- [ ] Los mensajes de éxito y error explican el resultado.
- [ ] Botón atrás, cancelar y volver conducen a pantallas válidas.
- [ ] Búsquedas, orden y filtros funcionan en productos, ventas y cuentas.
- [ ] No aparecen enlaces a módulos eliminados.
- [ ] No hay errores en la consola del navegador.
- [ ] Recargar una pantalla de confirmación no repite operaciones.

## 14. Revisión del paquete que se subirá

- [ ] Generar nuevamente `artifacts/publish` después de la última corrección.
- [ ] No incluir `appsettings.Development.json` con datos sensibles.
- [ ] No incluir archivos temporales, backups ni bases locales.
- [ ] No incluir `bin`, `obj`, `.git` ni secretos de usuario.
- [ ] Incluir `wwwroot`, vistas, DLL y configuraciones necesarias.
- [ ] Guardar checksum o comprimir el paquete aprobado para evitar cambios posteriores.
- [ ] Guardar el SQL idempotente junto al paquete, pero no ejecutarlo todavía en el VPS.

## 15. Registro de defectos

| ID | Caso | Resultado observado | Severidad | Estado |
|---|---|---|---|---|
| | | | Crítica/Alta/Media/Baja | |

Bloquean la subida:

- Diferencias de dinero, deuda, utilidad o saldo.
- Stock negativo o incorrecto.
- Operaciones duplicadas.
- Pérdida o corrupción de datos.
- Permisos incorrectos.
- Migraciones que fallan sobre la copia.
- Excepciones no controladas en recorridos normales.

## 16. Criterio final

Marcar **LISTO PARA SUBIR AL VPS** solamente si:

- [ ] Todas las pruebas críticas fueron aprobadas.
- [ ] La migración funcionó sobre una copia realista.
- [ ] El script idempotente fue revisado.
- [ ] El backup local pudo restaurarse.
- [ ] No hay defectos críticos, altos ni medios vinculados con dinero, stock, permisos o datos.
- [ ] Se regeneró el paquete Release después del último cambio.
- [ ] El responsable funcional comparó manualmente balance, stock y cuentas.

### Decisión

- [ ] **LISTO PARA SUBIR AL VPS**
- [ ] **NO LISTO — requiere correcciones**

| Responsable | Fecha | Firma/observaciones |
|---|---|---|
| | | |
