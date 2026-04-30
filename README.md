# 📦 Módulo: GABRFIDLabeler

> **Aplicación de escritorio .NET MAUI (Windows) para impresión y gestión de etiquetas RFID en almacén.**  
> Versión objetivo: `net9.0-windows10.0.19041` | Plataforma: `win-x64`

---

## 🧭 Propósito

GABRFIDLabeler es una aplicación de escritorio Windows construida con .NET MAUI cuyo propósito central es gestionar el ciclo de vida completo de las etiquetas RFID utilizadas en el inventario físico de cajas/cajones del almacén de GAB Irapuato.

La aplicación actúa como puente entre tres elementos: la impresora Zebra (comunicación TCP/IP vía ZPL), el chip RFID embebido en la etiqueta física (escritura EPC + contraseña), y la base de datos SQL Server corporativa (`GAB_Irapuato`), donde se registra cada etiqueta generada.

Opera exclusivamente en estaciones Windows con acceso de red a la impresora Zebra y al servidor SQL.

---

## ⚙️ Responsabilidades

- **Conexión TCP/IP a impresora Zebra** mediante el SDK oficial (`Zebra.Printer.SDK`), utilizando el puerto ZPL estándar.
- **Generación de comandos ZPL** con código QR, número de cajón, fecha de compra y logotipo corporativo; opcionalmente con escritura de EPC y contraseña al chip RFID.
- **Impresión por lote** de N etiquetas consecutivas a partir de un número inicial configurable.
- **Registro masivo en SQL Server** de cada etiqueta generada, usando transacciones para garantizar consistencia, con control de duplicados mediante `IF NOT EXISTS`.
- **Reimpresión con auditoría** de etiquetas ya existentes en catálogo, consultando historial de movimientos de inventario antes de proceder.
- **Reimpresión de etiquetas nuevas** con un diseño alternativo (`PrintRfidAutoAsync`), verificando en base de datos si ya fueron marcadas como `EtiquetaNueva = 1`.
- **Presentación de propuestas** de etiquetas pendientes de primer movimiento, cargadas desde SQL según filtros de estado y fecha.
- **Control de tanda en memoria RAM** (`HashSet<string>`) para evitar reimpresiones duplicadas dentro de la misma sesión.
- **Navegación por modo de operación** mediante RadioButtons que muestran/ocultan secciones de la UI: `Impresion`, `Reimpresion`, `NuevaEtiqueta`.

---

## 🔄 Flujo de Funcionamiento

### Modo Impresión por Lote

```
Usuario ingresa IP de impresora
  → ConnectAsync(ip): abre TcpConnection al puerto ZPL de Zebra
  → Usuario configura fecha, número inicial y cantidad
  → PrintBatchCommand: solicita confirmación al usuario
  → PrintAndWriteRfidBatchAsync(fecha, inicialLabel, totalLabels)
      → GenerateBatchZPL(): construye string ZPL con todos los labels
          → Itera desde (inicialLabel+1) hasta (inicialLabel+totalLabels)
          → Cada label: text="080-M7623-{i:D6}", epc="7623{i:D6}"
          → ZPL incluye: ~SD25, ^PQ2, QR Code, número, fecha, logos GFA
      → printer.SendCommand(zplBatch): envía todo en un solo comando
      → BulkInsertLabels(): abre SqlConnection
          → Inicia SqlTransaction
          → Por cada label: INSERT IF NOT EXISTS en Tb_RFID_Catalogo
              (IdClaveTag="7623{i:D6}00000000000000", IdClaveInt="080-M7623-{i:D6}", IdStatus="1", FechaCompra)
          → CommitAsync() o RollbackAsync() en caso de error
```

### Modo Reimpresión

```
Usuario ingresa número de etiqueta (o escáner lo popula con wedge)
  → ReprintCommand → ReprintLabelAsync()
  → Detecta prefijo "¬" para modo forzado
  → Parsea número → formattedLabel = "080-M7623-{numero:D6}"
  → GetLastMovementAsync(): consulta JOIN entre Tb_RFID_Catalogo, Tb_RFID_Det y Tb_RFID_Mstr
      → Retorna último movimiento (tipo E/S, fecha, status)
  → Muestra alerta de auditoría al usuario (con tiempo transcurrido)
      → Si forzarReimpresion=true, omite alerta
      → Usuario puede cancelar o confirmar
  → Verifica HashSet _reprintedLabels (bloqueo de tanda en RAM)
  → Consulta datos en Tb_RFID_Catalogo
  → Llama PrintAndWriteRfidAsync() × 2 (con pausa de 250ms entre cada una)
  → Registra en _reprintedLabels
  → ReprintFinished event: refoca el Entry y selecciona el texto
```

### Modo Nueva Etiqueta

```
Usuario ingresa número
  → ReprintNCommand → ReprintNewLabelAsync()
  → Detecta prefijo "¬" para modo forzado
  → Verifica _reprintedNewLabels (RAM)
  → Consulta Tb_RFID_Catalogo incluyendo campo EtiquetaNueva
  → Si EtiquetaNueva=1 y !forzar → bloquea con alerta
  → Llama PrintRfidAutoAsync() × 2:
      → ExtraerBigDesdeNumeroDividido(): divide número en big1 y big2 para layout
      → ExtraerMesAnio(): extrae mes (es-MX) y año de la fecha
      → Envía ZPL con diseño alternativo de dos columnas
  → Si !forzar → UPDATE Tb_RFID_Catalogo SET EtiquetaNueva=1
  → Registra en _reprintedNewLabels
  → ReprintNFinished event: refoca el Entry
```

### Carga de Propuestas (modo Reimpresión)

```
Al cambiar SelectedMode a "Reimpresion"
  → CargarSugerenciasAsync()
  → SELECT IdClaveInt FROM Tb_RFID_Catalogo
    WHERE IdStatus=1 AND FechaCompra='MAR-2022'
    AND IdClaveInt NOT IN (SELECT IdClaveInt FROM Tb_RFID_DetInv)
    AND IdClaveInt NOT IN (SELECT IdClaveInt FROM Tb_RFID_Det)
    AND FechaUltimoMovimiento IS NULL
    ORDER BY FechaUltimoMovimiento ASC
  → Popula EtiquetasSugeridas (ObservableCollection)
  → Actualiza TotalPendientes
```

---

## 📐 Reglas de Negocio

### 🔒 Restricciones

| # | Regla | Origen |
|---|-------|--------|
| R1 | Cada etiqueta tiene un identificador único con formato `080-M7623-{NNNNNN}` (6 dígitos con cero a la izquierda). | `GenerateBatchZPL`, `BulkInsertLabels` |
| R2 | El EPC grabado en el chip RFID se construye como `7623{NNNNNN}` (10 caracteres) para ZPL, y `7623{NNNNNN}00000000000000` (24 caracteres) para la base de datos. | `GenerateBatchZPL`, `BulkInsertLabels` |
| R3 | La contraseña de acceso al chip RFID es fija: `C3494D32`. La contraseña por defecto a reemplazar es `00000000`. | `PrintAndWriteNERfidAsync`, `PrintAndWriteNEWRfidAsync`, `PrintRfidAutoAsync` |
| R4 | Una etiqueta no puede reimprimirse más de una vez por sesión sin el símbolo especial `¬` como prefijo en el número ingresado (modo forzado). | `ReprintLabelAsync`, `ReprintNewLabelAsync` |
| R5 | Las etiquetas marcadas como `EtiquetaNueva=1` en base de datos no pueden reimprimirse en modo Nueva Etiqueta sin el modo forzado (`¬`). | `ReprintNewLabelAsync` |
| R6 | El lote de impresión siempre emite impresión doble por etiqueta (dos copias físicas consecutivas). | `ReprintLabelAsync`, `ReprintNewLabelAsync` |

### ✅ Validaciones

| # | Validación | Comportamiento ante fallo |
|---|-----------|--------------------------|
| V1 | El número ingresado debe ser un entero válido (`int.TryParse`). | Alerta al usuario, aborta operación. |
| V2 | La cantidad de etiquetas a imprimir debe ser mayor a 0. | Alerta al usuario, aborta operación. |
| V3 | El EPC debe cumplir el patrón `^[0-9A-F]{24}$`. | Llamada a `EsEpcValido()`, pero **no se lanza excepción** si falla — solo verifica. |
| V4 | El número ingresado para reimpresión no debe estar vacío o en blanco. | Alerta al usuario, aborta operación. |
| V5 | El registro debe existir en `Tb_RFID_Catalogo` antes de reimprimir. | Alerta al usuario, aborta operación. |
| V6 | Se solicita confirmación explícita del usuario antes de ejecutar impresión por lote. | El usuario puede cancelar. |
| V7 | Se muestra alerta de auditoría con historial de movimientos antes de reimprimir (en modo normal). | El usuario puede cancelar. |

### 🔁 Agrupaciones

| # | Regla |
|---|-------|
| A1 | Las etiquetas propuestas para reimpresión se agrupan por: `IdStatus=1`, `FechaCompra='MAR-2022'`, ausencia en `Tb_RFID_Det` y `Tb_RFID_DetInv`, y `FechaUltimoMovimiento IS NULL`. |
| A2 | El número de cajón en el diseño "Auto" se divide en dos fragmentos (`big1`, `big2`) para impresión en tipografía grande, según longitud del número (1→1|1, 2→1|1, 3→2|1, 4→2|2). |
| A3 | La fecha en modo Auto se separa en mes (abreviatura en español, mayúsculas, sin punto) y año, impresos en zonas independientes del label. |

### ⚙️ Reglas Operativas

| # | Regla |
|---|-------|
| O1 | La inserción en base de datos durante impresión por lote está envuelta en una transacción SQL; si alguna inserción falla, se hace rollback completo del lote. |
| O2 | La inserción individual usa `IF NOT EXISTS` para evitar duplicados en `Tb_RFID_Catalogo` por `IdClaveInt`. |
| O3 | Entre dos impresiones consecutivas de la misma etiqueta (doble copia) se introduce un `Task.Delay` de 250ms (o 200ms según el método) para estabilidad de la impresora. |
| O4 | El historial de auditoría presenta al usuario el tipo de movimiento (Entrada/Salida), la fecha, y el tiempo transcurrido calculado en meses, días y horas. |
| O5 | El evento `ReprintFinished` / `ReprintNFinished` refoca automáticamente el campo de entrada y selecciona el texto completo, optimizando flujos con lector de código de barras (keyboard wedge). |
| O6 | Al cambiar al modo Reimpresión, la lista de propuestas se carga automáticamente sin intervención del usuario. |

---

## 🔗 Dependencias

### Librerías NuGet

| Librería | Versión | Uso |
|----------|---------|-----|
| `Zebra.Printer.SDK` | 4.0.3428 / 4.0.3435 (Windows) | Comunicación con impresora, envío de ZPL, escritura RFID |
| `Microsoft.Data.SqlClient` | 7.0.0 | Acceso a SQL Server (`GAB_Irapuato`) |
| `CommunityToolkit.Mvvm` | 8.4.2 | `ObservableObject`, `[ObservableProperty]` |
| `CommunityToolkit.Maui` | 12.1.0 | Extensiones MAUI |
| `Microsoft.Maui.Controls` | 9.0.90 | Framework UI |
| `Microsoft.Extensions.Logging.Debug` | 9.0.7 | Logging en debug |

### Tablas SQL Server (`GAB_Irapuato`)

| Tabla | Rol |
|-------|-----|
| `Tb_RFID_Catalogo` | Catálogo maestro de etiquetas. Campos clave: `IdClaveTag`, `IdClaveInt`, `IdStatus`, `FechaCompra`, `EtiquetaNueva`, `FechaUltimoMovimiento` |
| `Tb_RFID_Det` | Detalle de movimientos de inventario; usado para filtrar etiquetas con actividad |
| `Tb_RFID_DetInv` | Detalle alternativo de inventario; también excluye etiquetas de propuestas |
| `Tb_RFID_Mstr` | Maestro de movimientos; proporciona `TipoMov`, `FechaMov`, `Mstr_Status` para auditoría |

### Infraestructura

| Componente | Detalle |
|-----------|---------|
| Impresora Zebra | Conexión TCP/IP, puerto ZPL por defecto (9100). IP configurable por el usuario. |
| SQL Server | `tcp:189.206.160.206,2352` — **connection string embebida en código** |
| Sistema Operativo | Windows 10/11 x64 (mínimo build 19041) |

---

## ⚠️ Riesgos Técnicos

### 🔴 Críticos

| # | Riesgo | Descripción |
|---|--------|-------------|
| RT1 | **Credenciales SQL en texto plano** | La connection string completa (servidor, usuario, contraseña `Gabira1`) está hardcodeada en `MainPage.xaml.cs` y duplicada en `PrinterViewModel.cs`. Cualquier persona con acceso al binario o al repositorio tiene acceso completo a la base de datos. |
| RT2 | **Contraseña RFID fija y en texto plano** | La contraseña del chip RFID `C3494D32` está embebida en múltiples métodos ZPL. Un cambio de contraseña requiere modificar y redesplegar el código. |
| RT3 | **`EsEpcValido()` no lanza excepción** | El método valida el EPC con regex pero no detiene el flujo si el EPC es inválido. Se puede grabar un EPC corrupto en el chip RFID sin advertencia al usuario. |

### 🟠 Altos

| # | Riesgo | Descripción |
|---|--------|-------------|
| RT4 | **Bloqueo anti-reimpresión solo en RAM** | El `HashSet<string>` que previene duplicados de tanda se pierde al cerrar la aplicación. En reinicios durante una tanda, la protección desaparece. |
| RT5 | **Múltiples versiones de ZPL en producción** | Existen 6+ métodos de impresión (`PrintAndWriteRfidAsync`, `PrintAndWriteNERfidAsync`, `PrintAndWriteNEWRfidAsync`, `PrintRfidAutoAsync`, etc.) con lógica ZPL divergente. Es difícil determinar cuál es el canónico activo. |
| RT6 | **Connection string duplicada** | La misma cadena de conexión aparece en `PrinterViewModel.cs` (campo `_connectionString`) y en `MainPage.xaml.cs`. Un cambio de servidor requiere actualización en múltiples lugares. |
| RT7 | **Sin manejo de estado de conexión a impresora** | No se verifica si la conexión TCP sigue activa antes de enviar comandos. Un desconecte silencioso entre `ConnectAsync` y una operación de impresión causará una excepción no controlada en el nivel de `ZebraPrinterService`. |

### 🟡 Medios

| # | Riesgo | Descripción |
|---|--------|-------------|
| RT8 | **Consulta de propuestas con fecha hardcodeada** | `CargarSugerenciasAsync` filtra por `FechaCompra = 'MAR-2022'`. Etiquetas de otras fechas nunca aparecerán como propuestas sin modificar el código. |
| RT9 | **`PrintMultipleLabelsCommand` no usa `BulkInsertLabels`** | Este comando (aparentemente legacy) realiza inserciones label a label sin transacción y sin control de duplicados, a diferencia del flujo por lote correcto. |
| RT10 | **Impresión no atómica respecto a BD** | `PrintAndWriteRfidBatchAsync` imprime primero y luego inserta en BD. Si la inserción falla, las etiquetas físicas ya fueron impresas sin registro. |
| RT11 | **`using System.Data.SqlClient` mezclado con `Microsoft.Data.SqlClient`** | Ambos namespaces están referenciados en `ZebraPrinterService.cs` y `PrinterViewModel.cs`, generando ambigüedad en el compilador y potencial comportamiento inesperado. |

---

## 🧪 Casos Edge

| # | Escenario | Comportamiento Actual |
|---|-----------|----------------------|
| CE1 | El usuario ingresa `0` como número de etiqueta | `int.TryParse` pasa, se construye `080-M7623-000000` — etiqueta con número inválido dentro del sistema. |
| CE2 | La impresora se desconecta durante un lote | `printer.SendCommand` lanza excepción; el `catch` solo imprime en `Console.WriteLine`. El lote puede quedar parcialmente impreso y parcialmente en BD. |
| CE3 | Número de cajón mayor a 9999 en modo Auto | `ExtraerBigDesdeNumeroDividido` lanza `Exception("El valor BIG debe estar entre 1 y 10000")` — no manejada por el caller en `PrintRfidAutoAsync`. |
| CE4 | Campo `EtiquetaNueva` ausente en la BD (columna no existente) | `SqlDataReader["EtiquetaNueva"]` lanzaría `IndexOutOfRangeException`. |
| CE5 | El usuario ingresa solo `¬` sin número | `cadenaLimpia` queda vacía → `int.TryParse` falla → alerta de error. Comportamiento correcto pero no diferenciado del caso de cadena realmente vacía. |
| CE6 | `FechaCompra` no es parseable como `DateTime` en `ExtraerMesAnio` | Se usa `DateTime.Now` como fallback silencioso — la etiqueta imprimiría la fecha actual sin aviso. |
| CE7 | Número de etiqueta `10000` en `ExtraerBigDesdeNumeroDividido` | La condición es `valor > 10000` (estricto), por lo que `10000` pasa y se divide como `"10" | "00"`. |
| CE8 | La lista de propuestas se carga pero `Tb_RFID_DetInv` no existe en BD | `SqlException` capturada solo con `Debug.WriteLine` — la UI queda con lista vacía sin notificación al usuario. |

---

## 🧱 Suposiciones Detectadas

| # | Suposición |
|---|-----------|
| S1 | La impresora Zebra siempre está accesible en la red local en el IP configurado por el usuario. |
| S2 | El número de cajón tiene correlación directa con el sufijo numérico de `IdClaveInt` (ej. `000042` → cajón 42). |
| S3 | Cada etiqueta física debe imprimirse siempre en doble copia (2 unidades físicas por número lógico). |
| S4 | El formato de fecha para almacenamiento es `MMM-YYYY` en mayúsculas y español (ej. `MAR-2025`). |
| S5 | Los lectores de código de barras operan como dispositivos keyboard wedge, enviando el valor directamente al campo Entry con foco activo. |
| S6 | El EPC grabado en el chip RFID tiene longitud fija de 24 caracteres hexadecimales. |
| S7 | Solo un operador usa la aplicación a la vez por estación. No hay concurrencia multi-usuario considerada. |
| S8 | La tabla `Tb_RFID_Catalogo` tiene la columna `EtiquetaNueva` de tipo `BIT` ya existente en producción. |

---

## 📈 Recomendaciones Técnicas

### 🔐 Seguridad (Prioridad Alta)

1. **Mover la connection string a `appsettings.json` o variables de entorno.** Usar `Microsoft.Extensions.Configuration` en `MauiProgram.cs` y pasar la configuración por inyección de dependencias. Eliminar la cadena duplicada.

2. **Externalizar la contraseña RFID.** Moverla a configuración protegida (archivo cifrado o variable de entorno). Definir una constante o servicio de configuración único.

3. **Ejecutar el audit de `EsEpcValido` antes de cualquier operación de impresión**, y abortar el flujo con mensaje claro al usuario si el EPC no es válido.

### 🏗️ Arquitectura (Prioridad Alta)

4. **Consolidar los métodos ZPL en un único `ZplBuilder` configurable.** El archivo `ZplRfidBuilder.cs` y la clase `RfidSafeLayout` ya apuntan en esta dirección. Eliminar los métodos deprecados (`PrintAndWriteRfidAsync`, `PrintAndWritesRfidAsync`, etc.) y centralizar en `PrintRfidAutoAsync` o el builder.

5. **Implementar el patrón Repository** para todas las operaciones SQL. Extraer la lógica de acceso a datos de `ZebraPrinterService` y `PrinterViewModel` a repositorios dedicados (`RfidCatalogoRepository`, `MovimientoRepository`).

6. **Registrar servicios en el DI container de MAUI.** `ZebraPrinterService` y los repositorios deben registrarse en `MauiProgram.cs` con `builder.Services.AddSingleton<>()` y resolverse por constructor, no instanciarse manualmente en el ViewModel.

### 🛡️ Robustez (Prioridad Media)

7. **Implementar verificación de conexión activa** antes de cada operación de impresión. Agregar un método `IsConnected()` en `ZebraPrinterService` y reconectar automáticamente o notificar al usuario.

8. **Hacer la fecha de propuestas configurable.** Reemplazar el literal `'MAR-2022'` en `CargarSugerenciasAsync` por un parámetro de filtro seleccionable en la UI o un valor en configuración.

9. **Persistir el registro de reimpresiones** entre sesiones. Usar una tabla SQL o archivo local para que el bloqueo anti-duplicado sobreviva reinicios de la aplicación.

10. **Unificar el namespace de SqlClient.** Remover la referencia a `System.Data.SqlClient` del proyecto y usar exclusivamente `Microsoft.Data.SqlClient` en todos los archivos.

11. **Agregar logging estructurado** (reemplazar `Console.WriteLine` en catch blocks) usando `ILogger<T>` de `Microsoft.Extensions.Logging`, integrado con el sistema de logging de MAUI ya configurado en `MauiProgram.cs`.

### 🧹 Calidad de Código (Prioridad Baja)

12. **Eliminar código comentado.** Los múltiples bloques ZPL comentados (`zplCommand`, `zplCommand2`, `zplCommand4`, `zplRFID`) representan deuda técnica y riesgo de confusión. Eliminarlos o moverlos a documentación.

13. **Implementar `IAsyncDisposable`** en `ZebraPrinterService` para garantizar el cierre de la conexión TCP en todos los escenarios, incluyendo excepciones.

---

## 🧾 Resumen Ejecutivo

GABRFIDLabeler es una aplicación de escritorio para el personal operativo de GAB Irapuato que automatiza la producción de etiquetas físicas RFID para cajones de almacén. El operador configura la impresora Zebra por red, especifica cuántas etiquetas necesita y a partir de qué número, y la aplicación genera e imprime el lote completo en un solo comando, registrando simultáneamente cada etiqueta en el sistema de inventario.

La aplicación también permite reimprimir etiquetas dañadas o perdidas, con una capa de seguridad que avisa al operador si la etiqueta ya tuvo movimientos de entrada o salida, reduciendo el riesgo de introducir etiquetas duplicadas en el flujo logístico.

**Estado actual:** La funcionalidad principal opera correctamente en producción. Sin embargo, existen riesgos de seguridad significativos (credenciales expuestas en el código fuente) y fragilidades técnicas (bloqueo de duplicados que no persiste entre reinicios, múltiples versiones de lógica de impresión activas simultáneamente) que deben ser atendidos antes de escalar el sistema a más estaciones o integrar con otros módulos del ERP.

**Impacto en negocio:** Un fallo en este sistema implica etiquetas físicas sin registro en el inventario, o registros en inventario sin etiqueta física válida, lo que compromete la trazabilidad de los cajones en el almacén.

---

*Documentación generada a partir del análisis estático del código fuente del repositorio `GABRFIDLabeler`. Fecha de análisis: Abril 2026.*