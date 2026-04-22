# 馃摝 M贸dulo: GABRFIDLabeler

> **Aplicaci贸n de escritorio .NET MAUI (Windows) para impresi贸n y gesti贸n de etiquetas RFID en almac茅n.**  
> Versi贸n objetivo: `net9.0-windows10.0.19041` | Plataforma: `win-x64`

---

## 馃Л Prop贸sito

GABRFIDLabeler es una aplicaci贸n de escritorio Windows construida con .NET MAUI cuyo prop贸sito central es gestionar el ciclo de vida completo de las etiquetas RFID utilizadas en el inventario f铆sico de cajas/cajones del almac茅n de GAB Irapuato.

La aplicaci贸n act煤a como puente entre tres elementos: la impresora Zebra (comunicaci贸n TCP/IP v铆a ZPL), el chip RFID embebido en la etiqueta f铆sica (escritura EPC + contrase帽a), y la base de datos SQL Server corporativa (`GAB_Irapuato`), donde se registra cada etiqueta generada.

Opera exclusivamente en estaciones Windows con acceso de red a la impresora Zebra y al servidor SQL.

---

## 鈿欙笍 Responsabilidades

- **Conexi贸n TCP/IP a impresora Zebra** mediante el SDK oficial (`Zebra.Printer.SDK`), utilizando el puerto ZPL est谩ndar.
- **Generaci贸n de comandos ZPL** con c贸digo QR, n煤mero de caj贸n, fecha de compra y logotipo corporativo; opcionalmente con escritura de EPC y contrase帽a al chip RFID.
- **Impresi贸n por lote** de N etiquetas consecutivas a partir de un n煤mero inicial configurable.
- **Registro masivo en SQL Server** de cada etiqueta generada, usando transacciones para garantizar consistencia, con control de duplicados mediante `IF NOT EXISTS`.
- **Reimpresi贸n con auditor铆a** de etiquetas ya existentes en cat谩logo, consultando historial de movimientos de inventario antes de proceder.
- **Reimpresi贸n de etiquetas nuevas** con un dise帽o alternativo (`PrintRfidAutoAsync`), verificando en base de datos si ya fueron marcadas como `EtiquetaNueva = 1`.
- **Presentaci贸n de propuestas** de etiquetas pendientes de primer movimiento, cargadas desde SQL seg煤n filtros de estado y fecha.
- **Control de tanda en memoria RAM** (`HashSet<string>`) para evitar reimpresiones duplicadas dentro de la misma sesi贸n.
- **Navegaci贸n por modo de operaci贸n** mediante RadioButtons que muestran/ocultan secciones de la UI: `Impresion`, `Reimpresion`, `NuevaEtiqueta`.

---

## 馃攧 Flujo de Funcionamiento

### Modo Impresi贸n por Lote

```
Usuario ingresa IP de impresora
  鈫?ConnectAsync(ip): abre TcpConnection al puerto ZPL de Zebra
  鈫?Usuario configura fecha, n煤mero inicial y cantidad
  鈫?PrintBatchCommand: solicita confirmaci贸n al usuario
  鈫?PrintAndWriteRfidBatchAsync(fecha, inicialLabel, totalLabels)
      鈫?GenerateBatchZPL(): construye string ZPL con todos los labels
          鈫?Itera desde (inicialLabel+1) hasta (inicialLabel+totalLabels)
          鈫?Cada label: text="080-M7623-{i:D6}", epc="7623{i:D6}"
          鈫?ZPL incluye: ~SD25, ^PQ2, QR Code, n煤mero, fecha, logos GFA
      鈫?printer.SendCommand(zplBatch): env铆a todo en un solo comando
      鈫?BulkInsertLabels(): abre SqlConnection
          鈫?Inicia SqlTransaction
          鈫?Por cada label: INSERT IF NOT EXISTS en Tb_RFID_Catalogo
              (IdClaveTag="7623{i:D6}00000000000000", IdClaveInt="080-M7623-{i:D6}", IdStatus="1", FechaCompra)
          鈫?CommitAsync() o RollbackAsync() en caso de error
```

### Modo Reimpresi贸n

```
Usuario ingresa n煤mero de etiqueta (o esc谩ner lo popula con wedge)
  鈫?ReprintCommand 鈫?ReprintLabelAsync()
  鈫?Detecta prefijo "卢" para modo forzado
  鈫?Parsea n煤mero 鈫?formattedLabel = "080-M7623-{numero:D6}"
  鈫?GetLastMovementAsync(): consulta JOIN entre Tb_RFID_Catalogo, Tb_RFID_Det y Tb_RFID_Mstr
      鈫?Retorna 煤ltimo movimiento (tipo E/S, fecha, status)
  鈫?Muestra alerta de auditor铆a al usuario (con tiempo transcurrido)
      鈫?Si forzarReimpresion=true, omite alerta
      鈫?Usuario puede cancelar o confirmar
  鈫?Verifica HashSet _reprintedLabels (bloqueo de tanda en RAM)
  鈫?Consulta datos en Tb_RFID_Catalogo
  鈫?Llama PrintAndWriteRfidAsync() 脳 2 (con pausa de 250ms entre cada una)
  鈫?Registra en _reprintedLabels
  鈫?ReprintFinished event: refoca el Entry y selecciona el texto
```

### Modo Nueva Etiqueta

```
Usuario ingresa n煤mero
  鈫?ReprintNCommand 鈫?ReprintNewLabelAsync()
  鈫?Detecta prefijo "卢" para modo forzado
  鈫?Verifica _reprintedNewLabels (RAM)
  鈫?Consulta Tb_RFID_Catalogo incluyendo campo EtiquetaNueva
  鈫?Si EtiquetaNueva=1 y !forzar 鈫?bloquea con alerta
  鈫?Llama PrintRfidAutoAsync() 脳 2:
      鈫?ExtraerBigDesdeNumeroDividido(): divide n煤mero en big1 y big2 para layout
      鈫?ExtraerMesAnio(): extrae mes (es-MX) y a帽o de la fecha
      鈫?Env铆a ZPL con dise帽o alternativo de dos columnas
  鈫?Si !forzar 鈫?UPDATE Tb_RFID_Catalogo SET EtiquetaNueva=1
  鈫?Registra en _reprintedNewLabels
  鈫?ReprintNFinished event: refoca el Entry
```

### Carga de Propuestas (modo Reimpresi贸n)

```
Al cambiar SelectedMode a "Reimpresion"
  鈫?CargarSugerenciasAsync()
  鈫?SELECT IdClaveInt FROM Tb_RFID_Catalogo
    WHERE IdStatus=1 AND FechaCompra='MAR-2022'
    AND IdClaveInt NOT IN (SELECT IdClaveInt FROM Tb_RFID_DetInv)
    AND IdClaveInt NOT IN (SELECT IdClaveInt FROM Tb_RFID_Det)
    AND FechaUltimoMovimiento IS NULL
    ORDER BY FechaUltimoMovimiento ASC
  鈫?Popula EtiquetasSugeridas (ObservableCollection)
  鈫?Actualiza TotalPendientes
```

---

## 馃搻 Reglas de Negocio

### 馃敀 Restricciones

| # | Regla | Origen |
|---|-------|--------|
| R1 | Cada etiqueta tiene un identificador 煤nico con formato `080-M7623-{NNNNNN}` (6 d铆gitos con cero a la izquierda). | `GenerateBatchZPL`, `BulkInsertLabels` |
| R2 | El EPC grabado en el chip RFID se construye como `7623{NNNNNN}` (10 caracteres) para ZPL, y `7623{NNNNNN}00000000000000` (24 caracteres) para la base de datos. | `GenerateBatchZPL`, `BulkInsertLabels` |
| R3 | La contrase帽a de acceso al chip RFID es fija: `C3494D32`. La contrase帽a por defecto a reemplazar es `00000000`. | `PrintAndWriteNERfidAsync`, `PrintAndWriteNEWRfidAsync`, `PrintRfidAutoAsync` |
| R4 | Una etiqueta no puede reimprimirse m谩s de una vez por sesi贸n sin el s铆mbolo especial `卢` como prefijo en el n煤mero ingresado (modo forzado). | `ReprintLabelAsync`, `ReprintNewLabelAsync` |
| R5 | Las etiquetas marcadas como `EtiquetaNueva=1` en base de datos no pueden reimprimirse en modo Nueva Etiqueta sin el modo forzado (`卢`). | `ReprintNewLabelAsync` |
| R6 | El lote de impresi贸n siempre emite impresi贸n doble por etiqueta (dos copias f铆sicas consecutivas). | `ReprintLabelAsync`, `ReprintNewLabelAsync` |

### 鉁?Validaciones

| # | Validaci贸n | Comportamiento ante fallo |
|---|-----------|--------------------------|
| V1 | El n煤mero ingresado debe ser un entero v谩lido (`int.TryParse`). | Alerta al usuario, aborta operaci贸n. |
| V2 | La cantidad de etiquetas a imprimir debe ser mayor a 0. | Alerta al usuario, aborta operaci贸n. |
| V3 | El EPC debe cumplir el patr贸n `^[0-9A-F]{24}$`. | Llamada a `EsEpcValido()`, pero **no se lanza excepci贸n** si falla 鈥?solo verifica. |
| V4 | El n煤mero ingresado para reimpresi贸n no debe estar vac铆o o en blanco. | Alerta al usuario, aborta operaci贸n. |
| V5 | El registro debe existir en `Tb_RFID_Catalogo` antes de reimprimir. | Alerta al usuario, aborta operaci贸n. |
| V6 | Se solicita confirmaci贸n expl铆cita del usuario antes de ejecutar impresi贸n por lote. | El usuario puede cancelar. |
| V7 | Se muestra alerta de auditor铆a con historial de movimientos antes de reimprimir (en modo normal). | El usuario puede cancelar. |

### 馃攣 Agrupaciones

| # | Regla |
|---|-------|
| A1 | Las etiquetas propuestas para reimpresi贸n se agrupan por: `IdStatus=1`, `FechaCompra='MAR-2022'`, ausencia en `Tb_RFID_Det` y `Tb_RFID_DetInv`, y `FechaUltimoMovimiento IS NULL`. |
| A2 | El n煤mero de caj贸n en el dise帽o "Auto" se divide en dos fragmentos (`big1`, `big2`) para impresi贸n en tipograf铆a grande, seg煤n longitud del n煤mero (1鈫?|1, 2鈫?|1, 3鈫?|1, 4鈫?|2). |
| A3 | La fecha en modo Auto se separa en mes (abreviatura en espa帽ol, may煤sculas, sin punto) y a帽o, impresos en zonas independientes del label. |

### 鈿欙笍 Reglas Operativas

| # | Regla |
|---|-------|
| O1 | La inserci贸n en base de datos durante impresi贸n por lote est谩 envuelta en una transacci贸n SQL; si alguna inserci贸n falla, se hace rollback completo del lote. |
| O2 | La inserci贸n individual usa `IF NOT EXISTS` para evitar duplicados en `Tb_RFID_Catalogo` por `IdClaveInt`. |
| O3 | Entre dos impresiones consecutivas de la misma etiqueta (doble copia) se introduce un `Task.Delay` de 250ms (o 200ms seg煤n el m茅todo) para estabilidad de la impresora. |
| O4 | El historial de auditor铆a presenta al usuario el tipo de movimiento (Entrada/Salida), la fecha, y el tiempo transcurrido calculado en meses, d铆as y horas. |
| O5 | El evento `ReprintFinished` / `ReprintNFinished` refoca autom谩ticamente el campo de entrada y selecciona el texto completo, optimizando flujos con lector de c贸digo de barras (keyboard wedge). |
| O6 | Al cambiar al modo Reimpresi贸n, la lista de propuestas se carga autom谩ticamente sin intervenci贸n del usuario. |

---

## 馃敆 Dependencias

### Librer铆as NuGet

| Librer铆a | Versi贸n | Uso |
|----------|---------|-----|
| `Zebra.Printer.SDK` | 4.0.3428 / 4.0.3435 (Windows) | Comunicaci贸n con impresora, env铆o de ZPL, escritura RFID |
| `Microsoft.Data.SqlClient` | 7.0.0 | Acceso a SQL Server (`GAB_Irapuato`) |
| `CommunityToolkit.Mvvm` | 8.4.2 | `ObservableObject`, `[ObservableProperty]` |
| `CommunityToolkit.Maui` | 12.1.0 | Extensiones MAUI |
| `Microsoft.Maui.Controls` | 9.0.90 | Framework UI |
| `Microsoft.Extensions.Logging.Debug` | 9.0.7 | Logging en debug |

### Tablas SQL Server (`GAB_Irapuato`)

| Tabla | Rol |
|-------|-----|
| `Tb_RFID_Catalogo` | Cat谩logo maestro de etiquetas. Campos clave: `IdClaveTag`, `IdClaveInt`, `IdStatus`, `FechaCompra`, `EtiquetaNueva`, `FechaUltimoMovimiento` |
| `Tb_RFID_Det` | Detalle de movimientos de inventario; usado para filtrar etiquetas con actividad |
| `Tb_RFID_DetInv` | Detalle alternativo de inventario; tambi茅n excluye etiquetas de propuestas |
| `Tb_RFID_Mstr` | Maestro de movimientos; proporciona `TipoMov`, `FechaMov`, `Mstr_Status` para auditor铆a |

### Infraestructura

| Componente | Detalle |
|-----------|---------|
| Impresora Zebra | Conexi贸n TCP/IP, puerto ZPL por defecto (9100). IP configurable por el usuario. |
| SQL Server | `tcp:189.206.160.206,2352` 鈥?**connection string embebida en c贸digo** |
| Sistema Operativo | Windows 10/11 x64 (m铆nimo build 19041) |

---

## 鈿狅笍 Riesgos T茅cnicos

### 馃敶 Cr铆ticos

| # | Riesgo | Descripci贸n |
|---|--------|-------------|
| RT1 | **Credenciales SQL en texto plano** | La connection string completa (servidor, usuario, contrase帽a `Gabira1`) est谩 hardcodeada en `MainPage.xaml.cs` y duplicada en `PrinterViewModel.cs`. Cualquier persona con acceso al binario o al repositorio tiene acceso completo a la base de datos. |
| RT2 | **Contrase帽a RFID fija y en texto plano** | La contrase帽a del chip RFID `C3494D32` est谩 embebida en m煤ltiples m茅todos ZPL. Un cambio de contrase帽a requiere modificar y redesplegar el c贸digo. |
| RT3 | **`EsEpcValido()` no lanza excepci贸n** | El m茅todo valida el EPC con regex pero no detiene el flujo si el EPC es inv谩lido. Se puede grabar un EPC corrupto en el chip RFID sin advertencia al usuario. |

### 馃煚 Altos

| # | Riesgo | Descripci贸n |
|---|--------|-------------|
| RT4 | **Bloqueo anti-reimpresi贸n solo en RAM** | El `HashSet<string>` que previene duplicados de tanda se pierde al cerrar la aplicaci贸n. En reinicios durante una tanda, la protecci贸n desaparece. |
| RT5 | **M煤ltiples versiones de ZPL en producci贸n** | Existen 6+ m茅todos de impresi贸n (`PrintAndWriteRfidAsync`, `PrintAndWriteNERfidAsync`, `PrintAndWriteNEWRfidAsync`, `PrintRfidAutoAsync`, etc.) con l贸gica ZPL divergente. Es dif铆cil determinar cu谩l es el can贸nico activo. |
| RT6 | **Connection string duplicada** | La misma cadena de conexi贸n aparece en `PrinterViewModel.cs` (campo `_connectionString`) y en `MainPage.xaml.cs`. Un cambio de servidor requiere actualizaci贸n en m煤ltiples lugares. |
| RT7 | **Sin manejo de estado de conexi贸n a impresora** | No se verifica si la conexi贸n TCP sigue activa antes de enviar comandos. Un desconecte silencioso entre `ConnectAsync` y una operaci贸n de impresi贸n causar谩 una excepci贸n no controlada en el nivel de `ZebraPrinterService`. |

### 馃煛 Medios

| # | Riesgo | Descripci贸n |
|---|--------|-------------|
| RT8 | **Consulta de propuestas con fecha hardcodeada** | `CargarSugerenciasAsync` filtra por `FechaCompra = 'MAR-2022'`. Etiquetas de otras fechas nunca aparecer谩n como propuestas sin modificar el c贸digo. |
| RT9 | **`PrintMultipleLabelsCommand` no usa `BulkInsertLabels`** | Este comando (aparentemente legacy) realiza inserciones label a label sin transacci贸n y sin control de duplicados, a diferencia del flujo por lote correcto. |
| RT10 | **Impresi贸n no at贸mica respecto a BD** | `PrintAndWriteRfidBatchAsync` imprime primero y luego inserta en BD. Si la inserci贸n falla, las etiquetas f铆sicas ya fueron impresas sin registro. |
| RT11 | **`using System.Data.SqlClient` mezclado con `Microsoft.Data.SqlClient`** | Ambos namespaces est谩n referenciados en `ZebraPrinterService.cs` y `PrinterViewModel.cs`, generando ambig眉edad en el compilador y potencial comportamiento inesperado. |

---

## 馃И Casos Edge

| # | Escenario | Comportamiento Actual |
|---|-----------|----------------------|
| CE1 | El usuario ingresa `0` como n煤mero de etiqueta | `int.TryParse` pasa, se construye `080-M7623-000000` 鈥?etiqueta con n煤mero inv谩lido dentro del sistema. |
| CE2 | La impresora se desconecta durante un lote | `printer.SendCommand` lanza excepci贸n; el `catch` solo imprime en `Console.WriteLine`. El lote puede quedar parcialmente impreso y parcialmente en BD. |
| CE3 | N煤mero de caj贸n mayor a 9999 en modo Auto | `ExtraerBigDesdeNumeroDividido` lanza `Exception("El valor BIG debe estar entre 1 y 10000")` 鈥?no manejada por el caller en `PrintRfidAutoAsync`. |
| CE4 | Campo `EtiquetaNueva` ausente en la BD (columna no existente) | `SqlDataReader["EtiquetaNueva"]` lanzar铆a `IndexOutOfRangeException`. |
| CE5 | El usuario ingresa solo `卢` sin n煤mero | `cadenaLimpia` queda vac铆a 鈫?`int.TryParse` falla 鈫?alerta de error. Comportamiento correcto pero no diferenciado del caso de cadena realmente vac铆a. |
| CE6 | `FechaCompra` no es parseable como `DateTime` en `ExtraerMesAnio` | Se usa `DateTime.Now` como fallback silencioso 鈥?la etiqueta imprimir铆a la fecha actual sin aviso. |
| CE7 | N煤mero de etiqueta `10000` en `ExtraerBigDesdeNumeroDividido` | La condici贸n es `valor > 10000` (estricto), por lo que `10000` pasa y se divide como `"10" | "00"`. |
| CE8 | La lista de propuestas se carga pero `Tb_RFID_DetInv` no existe en BD | `SqlException` capturada solo con `Debug.WriteLine` 鈥?la UI queda con lista vac铆a sin notificaci贸n al usuario. |

---

## 馃П Suposiciones Detectadas

| # | Suposici贸n |
|---|-----------|
| S1 | La impresora Zebra siempre est谩 accesible en la red local en el IP configurado por el usuario. |
| S2 | El n煤mero de caj贸n tiene correlaci贸n directa con el sufijo num茅rico de `IdClaveInt` (ej. `000042` 鈫?caj贸n 42). |
| S3 | Cada etiqueta f铆sica debe imprimirse siempre en doble copia (2 unidades f铆sicas por n煤mero l贸gico). |
| S4 | El formato de fecha para almacenamiento es `MMM-YYYY` en may煤sculas y espa帽ol (ej. `MAR-2025`). |
| S5 | Los lectores de c贸digo de barras operan como dispositivos keyboard wedge, enviando el valor directamente al campo Entry con foco activo. |
| S6 | El EPC grabado en el chip RFID tiene longitud fija de 24 caracteres hexadecimales. |
| S7 | Solo un operador usa la aplicaci贸n a la vez por estaci贸n. No hay concurrencia multi-usuario considerada. |
| S8 | La tabla `Tb_RFID_Catalogo` tiene la columna `EtiquetaNueva` de tipo `BIT` ya existente en producci贸n. |

---

## 馃搱 Recomendaciones T茅cnicas

### 馃攼 Seguridad (Prioridad Alta)

1. **Mover la connection string a `appsettings.json` o variables de entorno.** Usar `Microsoft.Extensions.Configuration` en `MauiProgram.cs` y pasar la configuraci贸n por inyecci贸n de dependencias. Eliminar la cadena duplicada.

2. **Externalizar la contrase帽a RFID.** Moverla a configuraci贸n protegida (archivo cifrado o variable de entorno). Definir una constante o servicio de configuraci贸n 煤nico.

3. **Ejecutar el audit de `EsEpcValido` antes de cualquier operaci贸n de impresi贸n**, y abortar el flujo con mensaje claro al usuario si el EPC no es v谩lido.

### 馃彈锔?Arquitectura (Prioridad Alta)

4. **Consolidar los m茅todos ZPL en un 煤nico `ZplBuilder` configurable.** El archivo `ZplRfidBuilder.cs` y la clase `RfidSafeLayout` ya apuntan en esta direcci贸n. Eliminar los m茅todos deprecados (`PrintAndWriteRfidAsync`, `PrintAndWritesRfidAsync`, etc.) y centralizar en `PrintRfidAutoAsync` o el builder.

5. **Implementar el patr贸n Repository** para todas las operaciones SQL. Extraer la l贸gica de acceso a datos de `ZebraPrinterService` y `PrinterViewModel` a repositorios dedicados (`RfidCatalogoRepository`, `MovimientoRepository`).

6. **Registrar servicios en el DI container de MAUI.** `ZebraPrinterService` y los repositorios deben registrarse en `MauiProgram.cs` con `builder.Services.AddSingleton<>()` y resolverse por constructor, no instanciarse manualmente en el ViewModel.

### 馃洝锔?Robustez (Prioridad Media)

7. **Implementar verificaci贸n de conexi贸n activa** antes de cada operaci贸n de impresi贸n. Agregar un m茅todo `IsConnected()` en `ZebraPrinterService` y reconectar autom谩ticamente o notificar al usuario.

8. **Hacer la fecha de propuestas configurable.** Reemplazar el literal `'MAR-2022'` en `CargarSugerenciasAsync` por un par谩metro de filtro seleccionable en la UI o un valor en configuraci贸n.

9. **Persistir el registro de reimpresiones** entre sesiones. Usar una tabla SQL o archivo local para que el bloqueo anti-duplicado sobreviva reinicios de la aplicaci贸n.

10. **Unificar el namespace de SqlClient.** Remover la referencia a `System.Data.SqlClient` del proyecto y usar exclusivamente `Microsoft.Data.SqlClient` en todos los archivos.

11. **Agregar logging estructurado** (reemplazar `Console.WriteLine` en catch blocks) usando `ILogger<T>` de `Microsoft.Extensions.Logging`, integrado con el sistema de logging de MAUI ya configurado en `MauiProgram.cs`.

### 馃Ч Calidad de C贸digo (Prioridad Baja)

12. **Eliminar c贸digo comentado.** Los m煤ltiples bloques ZPL comentados (`zplCommand`, `zplCommand2`, `zplCommand4`, `zplRFID`) representan deuda t茅cnica y riesgo de confusi贸n. Eliminarlos o moverlos a documentaci贸n.

13. **Implementar `IAsyncDisposable`** en `ZebraPrinterService` para garantizar el cierre de la conexi贸n TCP en todos los escenarios, incluyendo excepciones.

---

## 馃Ь Resumen Ejecutivo

GABRFIDLabeler es una aplicaci贸n de escritorio para el personal operativo de GAB Irapuato que automatiza la producci贸n de etiquetas f铆sicas RFID para cajones de almac茅n. El operador configura la impresora Zebra por red, especifica cu谩ntas etiquetas necesita y a partir de qu茅 n煤mero, y la aplicaci贸n genera e imprime el lote completo en un solo comando, registrando simult谩neamente cada etiqueta en el sistema de inventario.

La aplicaci贸n tambi茅n permite reimprimir etiquetas da帽adas o perdidas, con una capa de seguridad que avisa al operador si la etiqueta ya tuvo movimientos de entrada o salida, reduciendo el riesgo de introducir etiquetas duplicadas en el flujo log铆stico.

**Estado actual:** La funcionalidad principal opera correctamente en producci贸n. Sin embargo, existen riesgos de seguridad significativos (credenciales expuestas en el c贸digo fuente) y fragilidades t茅cnicas (bloqueo de duplicados que no persiste entre reinicios, m煤ltiples versiones de l贸gica de impresi贸n activas simult谩neamente) que deben ser atendidos antes de escalar el sistema a m谩s estaciones o integrar con otros m贸dulos del ERP.

**Impacto en negocio:** Un fallo en este sistema implica etiquetas f铆sicas sin registro en el inventario, o registros en inventario sin etiqueta f铆sica v谩lida, lo que compromete la trazabilidad de los cajones en el almac茅n.

---

*Documentaci贸n generada a partir del an谩lisis est谩tico del c贸digo fuente del repositorio `GABRFIDLabeler`. Fecha de an谩lisis: Abril 2026.*
