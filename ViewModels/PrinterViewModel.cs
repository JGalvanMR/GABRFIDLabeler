using Microsoft.Data.SqlClient;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Zebra.Sdk.Comm;
using GABRFIDLabeler.Models;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Collections.ObjectModel;

namespace GABRFIDLabeler.ViewModels;

public class PrinterViewModel : INotifyPropertyChanged
{
    //private readonly ZebraPrinterService _printerService = new ZebraPrinterService();
    private readonly ZebraPrinterService _printerService;
    private string _printerIp;
    private string _labelText;
    private string _epcCode;
    private string _date;
    private string _name;
    private int _labelQuantity;
    private int _labelsToPrint;
    private int _labelPrint;
    private readonly string _connectionString = "Server=tcp:189.206.160.206,2352;Database=GAB_Irapuato;User Id=sa;Password=Gabira1;Encrypt=True;TrustServerCertificate=True;";
    private bool _isSinglePrintEnabled;

    public PrinterViewModel(string dbConnectionString)
    {
        _printerService = new ZebraPrinterService(dbConnectionString);
        EmpresaSeleccionada = Empresa.GAB; // Valor por defecto
        LabelHelper.EmpresaActual = Empresa.GAB;
    }

    private string _selectedMode = "Impresion";
    public string SelectedMode
    {
        get => _selectedMode;
        set
        {
            if (_selectedMode != value)
            {
                _selectedMode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsImpresionVisible));
                OnPropertyChanged(nameof(IsReimpresionVisible));
                OnPropertyChanged(nameof(IsNuevaEtiquetaVisible));

                // Si el usuario entra a Reimpresión, cargamos sugerencias automáticamente
                if (value == "Reimpresion")
                {
                    _ = CargarSugerenciasAsync(); // El '_' indica que es una tarea disparada sin esperar (Fire and forget)
                }
            }
        }
    }

    public bool IsImpresionVisible => SelectedMode == "Impresion";
    public bool IsReimpresionVisible => SelectedMode == "Reimpresion";
    public bool IsNuevaEtiquetaVisible => SelectedMode == "NuevaEtiqueta";


    public string PrinterIp
    {
        get => _printerIp;
        set { _printerIp = value; OnPropertyChanged(); }
    }

    public string LabelText
    {
        get => _labelText;
        set { _labelText = value; OnPropertyChanged(); }
    }

    public string EpcCode
    {
        get => _epcCode;
        set { _epcCode = value; OnPropertyChanged(); }
    }

    public string Date
    {
        get => _date;
        set { _date = value; OnPropertyChanged(); }
    }

    public string Name
    {
        get => _name;
        set { _name = value; OnPropertyChanged(); }
    }

    //public int LabelQuantity
    //{
    //    get => _labelQuantity;
    //    set { _labelQuantity = value; OnPropertyChanged(); }
    //}

    public int LabelsToPrint
    {
        get => _labelsToPrint;
        set { _labelsToPrint = value; OnPropertyChanged(); }
    }

    public int LabelPrint
    {
        get => _labelPrint;
        set { _labelPrint = value; OnPropertyChanged(); }
    }

    public bool IsSinglePrintEnabled
    {
        get => _isSinglePrintEnabled;
        set
        {
            _isSinglePrintEnabled = value;
            OnPropertyChanged(nameof(IsSinglePrintEnabled));
        }
    }

    private string _epcToReprint;
    public string EpcToReprint
    {
        get => _epcToReprint;
        set { _epcToReprint = value; OnPropertyChanged(); }
    }

    private string _epcToReprintN;
    public string EpcToReprintN
    {
        get => _epcToReprintN;
        set { _epcToReprintN = value; OnPropertyChanged(); }
    }


    public ICommand ConnectCommand => new Command(async () =>
    {
        bool connected = await _printerService.ConnectAsync(PrinterIp);
        await Application.Current.MainPage.DisplayAlert("Conexión", connected ? "Conectado" : "Error al conectar", "OK");
    });

    public ICommand PrintSingleCommand => new Command(async () =>
    {
        await _printerService.PrintAndWriteRfidAsync(LabelText, EpcCode, Date, Name);
        await Application.Current.MainPage.DisplayAlert("Impresión", "Etiqueta impresa", "OK");
    });



    public ICommand PrintBatchCommand => new Command(async () =>
    {
        if (LabelsToPrint <= 0)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Ingrese un número válido de etiquetas", "OK");
            return;
        }

        // Validación específica para AGUILARES
        if (EmpresaSeleccionada == Empresa.AGUILARES)
        {
            if (LabelPrint < 100000)
            {
                await Application.Current.MainPage.DisplayAlert("Error",
                    "Para AGUILARES el número de inicio debe ser ≥ 100000", "OK");
                return;
            }
            int final = LabelPrint + LabelsToPrint - 1;
            if (final < 100000)
            {
                await Application.Current.MainPage.DisplayAlert("Error",
                    "Todos los números del lote deben ser ≥ 100000 para AGUILARES", "OK");
                return;
            }
        }

        bool confirm = await Application.Current.MainPage.DisplayAlert("Confirmar",
            $"¿Está seguro que desea imprimir {LabelsToPrint} etiquetas?", "Sí", "No");

        if (confirm)
        {
            try
            {
                await _printerService.PrintAndWriteRfidBatchAsync(Date, LabelPrint, LabelsToPrint);
                await Application.Current.MainPage.DisplayAlert("Éxito",
                    $"Se han impreso {LabelsToPrint} etiquetas", "OK");
            }
            catch (Exception ex)
            {
                await Application.Current.MainPage.DisplayAlert("Error",
                    $"Ocurrió un error: {ex.Message}", "OK");
            }
        }
    });

    public event PropertyChangedEventHandler PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public ICommand PrintCommand => new Command(async () =>
    {
        await _printerService.PrintAndWriteRfidAsync(LabelText, EpcCode, Date, Name);
        await Application.Current.MainPage.DisplayAlert("Impresión", "Etiqueta impresa", "OK");
    });

    public ICommand PrintMultipleLabelsCommand => new Command(async () =>
    {
        if (_labelQuantity <= 0)
        {
            await Application.Current.MainPage.DisplayAlert("Error", "Por favor, ingrese una cantidad válida de etiquetas", "OK");
            return;
        }

        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                for (int i = 1; i <= _labelQuantity; i++)
                {
                    //string text = $"080-M7623-{i:D6}";
                    //string epc = $"7623{i:D6}";
                    string text = LabelHelper.GetIdClaveInt(i);
                    string epc = LabelHelper.GetIdClaveTag(i);
                    string date = "MAY 2026";
                    string formattedDate = "MAY-2026";
                    //string name = $"Cajon {i}";
                    string name = $"{i}";

                    // Print first label
                    await _printerService.PrintAndWriteRfidAsync(text, epc, date, name);

                    // Insert first label into database
                    string insertQuery = "INSERT INTO Tb_RFID_Catalogo (IdClaveTag, IdClaveInt, IdStatus, FechaCompra) VALUES (@IdClaveTag, @IdClaveInt, @IdStatus, @FechaCompra)";
                    using (SqlCommand command = new SqlCommand(insertQuery, connection))
                    {
                        command.Parameters.AddWithValue("@IdClaveTag", epc);
                        command.Parameters.AddWithValue("@IdClaveInt", text);
                        command.Parameters.AddWithValue("@IdStatus", "1");
                        command.Parameters.AddWithValue("@FechaCompra", formattedDate);
                        await command.ExecuteNonQueryAsync();
                    }

                    // Print duplicate label
                    await _printerService.PrintAndWriteRfidAsync(text, epc, date, name);

                    //// Insert duplicate label into database
                    //using (SqlCommand command = new SqlCommand(insertQuery, connection))
                    //{
                    //    command.Parameters.AddWithValue("@IdClaveTag", epc);
                    //    command.Parameters.AddWithValue("@IdClaveInt", text);
                    //    command.Parameters.AddWithValue("@IdStatus", "1");
                    //    command.Parameters.AddWithValue("@FechaCompra", formattedDate);
                    //    await command.ExecuteNonQueryAsync();
                    //}
                }

                await Application.Current.MainPage.DisplayAlert("Impresión", $"Se imprimieron {2 * _labelQuantity} etiquetas", "OK");
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Error al imprimir o insertar en la base de datos: {ex.Message}", "OK");
        }
    });


    #region REIMPRESION DE ETIQUETAS
    public ICommand ReprintCommand => new Command<string>(async (param) =>
    {
        if (!string.IsNullOrEmpty(param))
            EpcToReprint = param; // Si viene de la lista, lo cargamos

        await ReprintLabelAsync();
    });

    public event Action ReprintFinished;
    // Agregar este campo en tu clase
    private readonly HashSet<string> _reprintedLabels = new HashSet<string>();

    private async Task ReprintLabelAsyncOG()
    {
        if (string.IsNullOrWhiteSpace(EpcToReprint))
        {
            await Application.Current.MainPage.DisplayAlert("Atención",
                "Ingrese el EPC o número de etiqueta a reimprimir.", "OK");
            return;
        }

        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                string query = @"SELECT IdClaveInt, FechaCompra, IdClaveTag 
                             FROM Tb_RFID_Catalogo 
                             WHERE IdClaveInt = @EPC";

                using SqlCommand cmd = new SqlCommand(query, connection);

                // Formatear correctamente el valor de búsqueda
                if (int.TryParse(EpcToReprint, out int numero))
                {
                    // Busca por el mismo formato que se usa al imprimir originalmente
                    string formattedLabel = $"080-M7623-{numero:D6}";
                    cmd.Parameters.AddWithValue("@EPC", formattedLabel);

                    // 🔹 Verificar si ya se reimprimió en esta tanda
                    if (_reprintedLabels.Contains(formattedLabel))
                    {
                        await Application.Current.MainPage.DisplayAlert("Atención",
                            $"La etiqueta {formattedLabel} ya fue reimpresa en esta tanda.", "OK");
                        ReprintFinished?.Invoke();
                        return;
                    }
                }
                else
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "El valor ingresado no es un número válido.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "No se encontró información para ese EPC.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                await reader.ReadAsync();

                string labelText = reader["IdClaveInt"].ToString();    // "080-M7623-000001"
                string date = reader["FechaCompra"].ToString();         // "MAR-2025"
                string name = reader["IdClaveTag"]?.ToString() ?? "";   // "7623000001"

                // 🔹 Reconstruir EPC correcto (para grabar en el chip RFID)
                // Si IdClaveTag viene vacío o mal, lo regeneramos
                string epc = !string.IsNullOrWhiteSpace(name)
                    ? name
                    : $"7623{numero:D6}";

                // 🔹 Reimprimir con los mismos parámetros que en el alta original
                //await _printerService.PrintAndWriteRfidAsync(labelText, epc, date, $"Cajon {numero}");
                //await _printerService.PrintAndWriteRfidAsync(labelText, epc, date, $"Cajon {numero}");
                await _printerService.PrintAndWriteRfidAsync(labelText, epc, date, $"{numero}");
                await _printerService.PrintAndWriteRfidAsync(labelText, epc, date, $"{numero}");

                // 🔹 Registrar en el HashSet para bloquear futuras reimpresiones
                _reprintedLabels.Add(labelText);

                await Application.Current.MainPage.DisplayAlert("Éxito",
                    $"Etiqueta {labelText} reimpresa correctamente.", "OK");

                ReprintFinished?.Invoke();

            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Ocurrió un error: {ex.Message}", "OK");
        }
    }

    private async Task ReprintLabelAsyncLEGACY()
    {
        // 1. Validación inicial
        if (string.IsNullOrWhiteSpace(EpcToReprint))
        {
            await Application.Current.MainPage.DisplayAlert("Atención",
                "Ingrese el EPC o número de etiqueta a reimprimir.", "OK");
            return;
        }

        // 2. Detectar carácter de forzado y limpiar la cadena
        bool forzarReimpresion = EpcToReprint.Contains("¬");
        string cadenaLimpia = EpcToReprint.Replace("¬", "").Trim();

        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 3. Validar que la cadena limpia sea un número
                if (!int.TryParse(cadenaLimpia, out int numero))
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "El valor ingresado no es un número válido.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                string formattedLabel = $"080-M7623-{numero:D6}";

                // 4. Validar bloqueo de tanda actual (Memoria RAM)
                // Si forzarReimpresion es true, ignoramos el .Contains
                if (_reprintedLabels.Contains(formattedLabel) && !forzarReimpresion)
                {
                    await Application.Current.MainPage.DisplayAlert("Atención",
                        $"La etiqueta {formattedLabel} ya fue reimpresa en esta tanda.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                string query = @"SELECT IdClaveInt, FechaCompra, IdClaveTag 
                             FROM Tb_RFID_Catalogo 
                             WHERE IdClaveInt = @EPC";

                using SqlCommand cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@EPC", formattedLabel);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "No se encontró información para ese EPC.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                await reader.ReadAsync();

                string labelText = reader["IdClaveInt"].ToString();
                string date = reader["FechaCompra"].ToString();
                string name = reader["IdClaveTag"]?.ToString() ?? "";

                string epc = !string.IsNullOrWhiteSpace(name)
                    ? name
                    : $"7623{numero:D6}";

                reader.Close();

                // 5. Impresión doble
                await _printerService.PrintAndWriteRfidAsync(labelText, epc, date, $"{numero}");
                await Task.Delay(200); // Pequeña pausa opcional entre impresiones
                await _printerService.PrintAndWriteRfidAsync(labelText, epc, date, $"{numero}");

                // 6. Registrar en el HashSet (si no estaba ya)
                if (!_reprintedLabels.Contains(labelText))
                {
                    _reprintedLabels.Add(labelText);
                }

                await Application.Current.MainPage.DisplayAlert("Éxito",
                    $"Etiqueta {labelText} reimpresa correctamente.", "OK");

                ReprintFinished?.Invoke();
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Ocurrió un error: {ex.Message}", "OK");
        }
    }

    private async Task ReprintLabelAsyncV1()
    {
        // 1. Validación inicial de entrada vacía
        if (string.IsNullOrWhiteSpace(EpcToReprint))
        {
            await Application.Current.MainPage.DisplayAlert("Atención",
                "Ingrese el EPC o número de etiqueta a reimprimir.", "OK");
            return;
        }

        // 2. Detectar carácter de forzado y limpiar la cadena
        bool forzarReimpresion = EpcToReprint.Contains("¬");
        string cadenaLimpia = EpcToReprint.Replace("¬", "").Trim();

        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 3. Validar que la cadena limpia sea un número
                if (!int.TryParse(cadenaLimpia, out int numero))
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "El valor ingresado no es un número válido.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                string formattedLabel = $"080-M7623-{numero:D6}";

                // --- INICIO DE VALIDACIÓN DE MOVIMIENTOS ---
                // Llamamos a la auditoría antes de proceder
                var audit = await GetLastMovementAsync(formattedLabel, connection);

                // --- DENTRO DE ReprintLabelAsync, después de obtener 'audit' ---

                if (audit != null)
                {
                    string mensaje;

                    if (audit.TieneMovimientos)
                    {
                        string nombreMov = audit.TipoMov == "E" ? "📥 ENTRADA" : (audit.TipoMov == "S" ? "📤 SALIDA" : audit.TipoMov);
                        string tiempo = audit.ObtenerTiempoTranscurrido();

                        mensaje = "⚠️ HISTORIAL ENCONTRADO ⚠️\n\n" +
                                  $"📌 Movimiento: {nombreMov}\n" +
                                  $"📅 Fecha: {audit.FechaMov:dd/MM/yyyy HH:mm}\n" +
                                  $"⏳ Antigüedad: Hace {tiempo}\n" +
                                  $"📋 Estado: {audit.MstrStatus}\n\n" +
                                  "Esta etiqueta ya ha tenido actividad operativa.\n" +
                                  "¿ESTÁ SEGURO QUE DESEA REIMPRIMIR?";
                    }
                    else
                    {
                        mensaje = "✅ SIN MOVIMIENTOS\n\n" +
                                  "La etiqueta no tiene historial de movimientos.\n" +
                                  "¿Desea proceder con la reimpresión?";
                    }

                    if (!forzarReimpresion)
                    {
                        // Cambiamos el título según la gravedad
                        string titulo = audit.TieneMovimientos ? "Confirmación Crítica" : "Confirmación de Impresión";

                        bool continuar = await Application.Current.MainPage.DisplayAlert(
                            titulo,
                            mensaje,
                            "SÍ, REIMPRIMIR",
                            "CANCELAR");

                        if (!continuar) return;
                    }
                }
                // --- FIN DE VALIDACIÓN DE MOVIMIENTOS ---

                // 4. Validar bloqueo de tanda actual (Memoria RAM)
                if (_reprintedLabels.Contains(formattedLabel) && !forzarReimpresion)
                {
                    await Application.Current.MainPage.DisplayAlert("Atención",
                        $"La etiqueta {formattedLabel} ya fue reimpresa en esta tanda.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                // 5. Consultar datos para la impresión
                string query = @"SELECT IdClaveInt, FechaCompra, IdClaveTag 
                             FROM Tb_RFID_Catalogo 
                             WHERE IdClaveInt = @EPC";

                using SqlCommand cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@EPC", formattedLabel);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "No se encontró información en el catálogo para ese EPC.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                await reader.ReadAsync();

                string labelText = reader["IdClaveInt"].ToString();
                string date = reader["FechaCompra"].ToString();
                string name = reader["IdClaveTag"]?.ToString() ?? "";

                string epc = !string.IsNullOrWhiteSpace(name)
                    ? name
                    : $"7623{numero:D6}";

                reader.Close(); // Importante cerrar antes de las llamadas async del servicio

                // 6. Impresión doble
                await _printerService.PrintAndWriteRfidAsync(labelText, epc, date, $"{numero}");
                await Task.Delay(250);
                await _printerService.PrintAndWriteRfidAsync(labelText, epc, date, $"{numero}");

                // 7. Registrar en el HashSet
                if (!_reprintedLabels.Contains(labelText))
                {
                    _reprintedLabels.Add(labelText);
                }

                await Application.Current.MainPage.DisplayAlert("Éxito",
                    $"Etiqueta {labelText} reimpresa correctamente.", "OK");

                ReprintFinished?.Invoke();
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Ocurrió un error en el proceso: {ex.Message}", "OK");
        }
    }

    private async Task ReprintLabelAsync()
    {
        if (string.IsNullOrWhiteSpace(EpcToReprint))
        {
            await Application.Current.MainPage.DisplayAlert("Atención", "Ingrese el EPC o número de etiqueta.", "OK");
            return;
        }

        bool forzarReimpresion = EpcToReprint.Contains("¬");
        string cadenaLimpia = EpcToReprint.Replace("¬", "").Trim();

        try
        {
            using SqlConnection connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            if (!int.TryParse(cadenaLimpia, out int numero))
            {
                await Application.Current.MainPage.DisplayAlert("Error", "El valor ingresado no es un número válido.", "OK");
                ReprintFinished?.Invoke();
                return;
            }

            // Prepara los dos posibles formatos
            string formattedLabelGAB = $"080-M7623-{numero:D6}";
            string formattedLabelAguilares = $"02-M7275-{numero}";
            string formattedLabel = null;

            // Intenta buscar según el formato actual primero
            string query = "SELECT IdClaveInt, FechaCompra, IdClaveTag FROM Tb_RFID_Catalogo WHERE IdClaveInt = @EPC";
            using SqlCommand cmd = new SqlCommand(query, connection);

            if (EmpresaSeleccionada == Empresa.GAB)
                formattedLabel = formattedLabelGAB;
            else
                formattedLabel = formattedLabelAguilares;

            cmd.Parameters.AddWithValue("@EPC", formattedLabel);
            using SqlDataReader reader = await cmd.ExecuteReaderAsync();

            if (!reader.HasRows)
            {
                // Si no se encuentra, probar con el otro formato
                reader.Close();
                cmd.Parameters["@EPC"].Value = (EmpresaSeleccionada == Empresa.GAB) ? formattedLabelAguilares : formattedLabelGAB;
                using SqlDataReader reader2 = await cmd.ExecuteReaderAsync();
                if (!reader2.HasRows)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "No se encontró información para ese EPC.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }
                await reader2.ReadAsync();
                formattedLabel = reader2["IdClaveInt"].ToString();
                string date = reader2["FechaCompra"].ToString();
                string name = reader2["IdClaveTag"]?.ToString() ?? "";
                // Extraer el número real (puede venir de cualquier empresa)
                if (!LabelHelper.TryParseClaveInt(formattedLabel, out int realNumber, out _))
                    realNumber = numero;
                string epc = !string.IsNullOrWhiteSpace(name) ? name : LabelHelper.GetIdClaveTag(realNumber);
                reader2.Close();

                // Verificar bloqueo de tanda
                if (_reprintedLabels.Contains(formattedLabel) && !forzarReimpresion)
                {
                    await Application.Current.MainPage.DisplayAlert("Atención", $"La etiqueta {formattedLabel} ya fue reimpresa en esta tanda.", "OK");
                    ReprintFinished?.Invoke();
                    return;
                }

                // Impresión doble
                await _printerService.PrintAndWriteRfidAsync(formattedLabel, epc, date, $"{realNumber}");
                await Task.Delay(250);
                await _printerService.PrintAndWriteRfidAsync(formattedLabel, epc, date, $"{realNumber}");

                if (!_reprintedLabels.Contains(formattedLabel))
                    _reprintedLabels.Add(formattedLabel);

                await Application.Current.MainPage.DisplayAlert("Éxito", $"Etiqueta {formattedLabel} reimpresa correctamente.", "OK");
                ReprintFinished?.Invoke();
                return;
            }

            // Si encontramos con el primer intento
            await reader.ReadAsync();
            string labelText = reader["IdClaveInt"].ToString();
            string dateFound = reader["FechaCompra"].ToString();
            string nameFound = reader["IdClaveTag"]?.ToString() ?? "";
            reader.Close();

            // Validar bloqueo de tanda
            if (_reprintedLabels.Contains(labelText) && !forzarReimpresion)
            {
                await Application.Current.MainPage.DisplayAlert("Atención", $"La etiqueta {labelText} ya fue reimpresa en esta tanda.", "OK");
                ReprintFinished?.Invoke();
                return;
            }

            string epcFinal = !string.IsNullOrWhiteSpace(nameFound) ? nameFound : LabelHelper.GetIdClaveTag(numero);

            await _printerService.PrintAndWriteRfidAsync(labelText, epcFinal, dateFound, $"{numero}");
            await Task.Delay(250);
            await _printerService.PrintAndWriteRfidAsync(labelText, epcFinal, dateFound, $"{numero}");

            if (!_reprintedLabels.Contains(labelText))
                _reprintedLabels.Add(labelText);

            await Application.Current.MainPage.DisplayAlert("Éxito", $"Etiqueta {labelText} reimpresa correctamente.", "OK");
            ReprintFinished?.Invoke();
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error", $"Ocurrió un error: {ex.Message}", "OK");
        }
    }

    private async Task<LabelAuditInfo> GetLastMovementAsync(string formattedLabel, SqlConnection connection)
    {
        // Buscamos el último registro en Detalle asociado a un Maestro de movimientos
        string query = @"
        SELECT TOP 1 
            C.IdClaveInt, 
            M.TipoMov, 
            M.FechaMov, 
            M.Mstr_Status
        FROM Tb_RFID_Catalogo C
        LEFT JOIN Tb_RFID_Det D ON C.IdClaveInt = D.IdClaveInt
        LEFT JOIN Tb_RFID_Mstr M ON D.IdConseInv = M.IdConse
        WHERE C.IdClaveInt = @EPC
        ORDER BY M.FechaMov DESC";

        using SqlCommand cmd = new SqlCommand(query, connection);
        cmd.Parameters.AddWithValue("@EPC", formattedLabel);

        using SqlDataReader reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new LabelAuditInfo
            {
                IdClaveInt = reader["IdClaveInt"].ToString(),
                TipoMov = reader["TipoMov"]?.ToString(),
                FechaMov = reader["FechaMov"] != DBNull.Value ? (DateTime?)reader["FechaMov"] : null,
                MstrStatus = reader["Mstr_Status"]?.ToString()
            };
        }
        return null;
    }
    public class LabelAuditInfo
    {
        public string IdClaveInt { get; set; }
        public string TipoMov { get; set; }
        public DateTime? FechaMov { get; set; }
        public string MstrStatus { get; set; }
        public bool TieneMovimientos => FechaMov.HasValue;

        // Método para obtener el tiempo transcurrido formateado
        public string ObtenerTiempoTranscurrido()
        {
            if (!FechaMov.HasValue) return "N/A";

            TimeSpan diferencia = DateTime.Now - FechaMov.Value;

            int meses = (int)(diferencia.Days / 30.44); // Promedio de días al mes
            int dias = diferencia.Days % 30;
            int horas = diferencia.Hours;

            string resultado = "";
            if (meses > 0) resultado += $"{meses} {(meses == 1 ? "mes" : "meses")}, ";
            if (dias > 0) resultado += $"{dias} {(dias == 1 ? "día" : "días")} y ";
            resultado += $"{horas} {(horas == 1 ? "hr" : "hrs")}";

            return resultado;
        }
    }

    #endregion


    #region REIMPRESION DE NUEVAS ETIQUETAS
    public ICommand ReprintNCommand => new Command(async () => await ReprintNewLabelAsync());

    public event Action ReprintNFinished;
    // Agregar este campo en tu clase
    private readonly HashSet<string> _reprintedNewLabels = new HashSet<string>();

    private async Task ReprintNewLabelAsync()
    {
        // 1. Validación inicial de entrada vacía
        if (string.IsNullOrWhiteSpace(EpcToReprintN))
        {
            await Application.Current.MainPage.DisplayAlert("Atención",
                "Ingrese el EPC o número de etiqueta a reimprimir.", "OK");
            return;
        }

        // 2. Detectar el carácter especial '¬' y limpiar la cadena
        // Guardamos si existe el símbolo y luego lo removemos para que int.TryParse no falle
        bool forzarReimpresion = EpcToReprintN.Contains("¬");
        string cadenaLimpia = EpcToReprintN.Replace("¬", "").Trim();

        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // 3. Validar que lo que quedó sea un número válido
                if (!int.TryParse(cadenaLimpia, out int numero))
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "El valor ingresado no es un número válido.", "OK");
                    ReprintNFinished?.Invoke();
                    return;
                }

                // Formatear la etiqueta para la consulta
                string formattedLabel = $"080-M7623-{numero:D6}";

                // 4. Verificar si ya se reimprimió en esta tanda (Memoria RAM)
                // Si 'forzarReimpresion' es true, ignoramos este bloqueo
                if (_reprintedNewLabels.Contains(formattedLabel) && !forzarReimpresion)
                {
                    await Application.Current.MainPage.DisplayAlert("Atención",
                        $"La etiqueta {formattedLabel} ya fue reimpresa en esta tanda.", "OK");
                    ReprintNFinished?.Invoke();
                    return;
                }

                string query = @"SELECT IdClaveInt, FechaCompra, IdClaveTag, EtiquetaNueva 
                             FROM Tb_RFID_Catalogo 
                             WHERE IdClaveInt = @EPC";

                using SqlCommand cmd = new SqlCommand(query, connection);
                cmd.Parameters.AddWithValue("@EPC", formattedLabel);

                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    await Application.Current.MainPage.DisplayAlert("Error",
                        "No se encontró información para ese EPC.", "OK");
                    ReprintNFinished?.Invoke();
                    return;
                }

                await reader.ReadAsync();

                // 5. Validar si ya fue reimpresa en la Base de Datos
                bool yaReimpresaBD = reader["EtiquetaNueva"] != DBNull.Value && Convert.ToBoolean(reader["EtiquetaNueva"]);

                // Si ya está marcada en BD y NO traemos el símbolo de forzado, bloqueamos
                if (yaReimpresaBD && !forzarReimpresion)
                {
                    await Application.Current.MainPage.DisplayAlert("Atención",
                        $"La etiqueta {cadenaLimpia} ya fue reimpresa como nueva en el sistema.", "OK");
                    ReprintNFinished?.Invoke();
                    return;
                }

                // 6. Preparar datos para la impresión
                string labelText = reader["IdClaveInt"].ToString();    // "080-M7623-000001"
                string date = reader["FechaCompra"].ToString();         // "MAR-2025"
                string name = reader["IdClaveTag"]?.ToString() ?? "";   // "7623000001"

                // Reconstruir EPC para el chip RFID
                string epc = !string.IsNullOrWhiteSpace(name)
                             ? name
                             : $"7623{numero:D6}";

                reader.Close(); // Cerramos el reader antes de proceder con otras tareas

                // 7. Proceso de Impresión (se hace doble por seguridad física)
                for (int i = 0; i < 2; i++)
                {
                    await _printerService.PrintRfidAutoAsync(labelText, epc, date, $"{numero}");
                    await Task.Delay(200);
                }

                // 8. Actualizar Base de Datos (solo si no es un forzado, o según tu lógica de negocio)
                // Normalmente, si es forzado, ya está marcada, así que no hace falta volver a marcar.
                if (!forzarReimpresion)
                {
                    await MarcarEtiquetaComoReimpresaAsync(epc);
                }

                // Registrar en la lista de la tanda actual
                if (!_reprintedNewLabels.Contains(labelText))
                {
                    _reprintedNewLabels.Add(labelText);
                }

                await Application.Current.MainPage.DisplayAlert("Éxito",
                    $"Etiqueta {labelText} reimpresa correctamente.", "OK");

                ReprintNFinished?.Invoke();
            }
        }
        catch (Exception ex)
        {
            await Application.Current.MainPage.DisplayAlert("Error",
                $"Ocurrió un error inesperado: {ex.Message}", "OK");
        }
    }

    public async Task MarcarEtiquetaComoReimpresaAsync(string epc)
    {
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            string query = @"
            UPDATE Tb_RFID_Catalogo
            SET EtiquetaNueva = 1
            WHERE IdClaveTag = @EPC";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@EPC", epc);

                await conn.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }

    private async Task<bool> YaFueReimpresaEnBDAsync(string epc)
    {
        using (SqlConnection conn = new SqlConnection(_connectionString))
        {
            string query = @"SELECT EtiquetaNueva 
                         FROM Tb_RFID_Catalogo 
                         WHERE IdClaveTag = @EPC";

            using SqlCommand cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@EPC", epc);

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();

            return result != null && Convert.ToBoolean(result);
        }
    }


    #endregion

    #region PROPUESTAS PARA REIMPRESIÓN DE ETIQUETAS CON HISTORIAL DE MOVIMIENTOS
    private ObservableCollection<string> _etiquetasSugeridas;
    public ObservableCollection<string> EtiquetasSugeridas
    {
        get => _etiquetasSugeridas;
        set { _etiquetasSugeridas = value; OnPropertyChanged(); }
    }

    // Comando para cargar las etiquetas desde la BD
    // Comando para cuando se toca una etiqueta de la lista de propuestas
    public ICommand ReprintFromListCommand => new Command<string>(async (idSeleccionado) =>
    {
        if (!string.IsNullOrEmpty(idSeleccionado))
        {
            // Extraemos el número final para que ReprintLabelAsync lo procese (ej: 000001)
            string[] partes = idSeleccionado.Split('-');
            if (partes.Length > 0)
            {
                EpcToReprint = partes[^1];

                // Ejecutamos tu lógica existente de impresión y auditoría
                await ReprintLabelAsync();

                // REQUERIMIENTO: Sumar al contador de la tanda actual
                TotalReimpresosTanda++;

                // REQUERIMIENTO: Refrescar la lista para que el registro desaparezca 
                // (Ya que ahora tendrá historial o estará en el HashSet)
                await CargarSugerenciasAsync();
            }
        }
    });
    public ICommand CargarSugerenciasCommand => new Command(async () => await CargarSugerenciasAsync());

    private async Task CargarSugerenciasAsync()
    {
        try
        {
            using (SqlConnection connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // Consulta que une el catálogo con IdStatus=1 y filtra las que NO están en las tablas de detalle/inventario
                string query = @"
                SELECT IdClaveInt 
                FROM Tb_RFID_Catalogo 
                WHERE IdStatus = 1 --AND FechaCompra = 'MAR-2022'
                AND IdClaveInt NOT IN (SELECT IdClaveInt FROM Tb_RFID_DetInv)
                AND IdClaveInt NOT IN (SELECT IdClaveInt FROM Tb_RFID_Det)
                AND FechaUltimoMovimiento IS NULL
                ORDER BY FechaUltimoMovimiento ASC";

                using SqlCommand cmd = new SqlCommand(query, connection);
                using SqlDataReader reader = await cmd.ExecuteReaderAsync();

                var listaTemp = new ObservableCollection<string>();
                while (await reader.ReadAsync())
                {
                    listaTemp.Add(reader["IdClaveInt"].ToString());
                }

                EtiquetasSugeridas = listaTemp;

                // REQUERIMIENTO: Actualizar el número total visual
                TotalPendientes = listaTemp.Count;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error: {ex.Message}");
        }
    }
    #endregion


    private int _totalPendientes;
    public int TotalPendientes
    {
        get => _totalPendientes;
        set { _totalPendientes = value; OnPropertyChanged(); }
    }

    private int _totalReimpresosTanda;
    public int TotalReimpresosTanda
    {
        get => _totalReimpresosTanda;
        set { _totalReimpresosTanda = value; OnPropertyChanged(); }
    }

    private Empresa _empresaSeleccionada = Empresa.GAB;
    public Empresa EmpresaSeleccionada
    {
        get => _empresaSeleccionada;
        set
        {
            if (_empresaSeleccionada != value)
            {
                _empresaSeleccionada = value;
                LabelHelper.EmpresaActual = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGAB));
                OnPropertyChanged(nameof(IsAguilares));
                if (value == Empresa.AGUILARES && LabelPrint < 100000)
                    LabelPrint = 100000;
            }
        }
    }

    public bool IsGAB => EmpresaSeleccionada == Empresa.GAB;
    public bool IsAguilares => EmpresaSeleccionada == Empresa.AGUILARES;

    private bool _isGAB = true;
    private bool _isAguilares = false;

    public bool IsGABChecked
    {
        get => _isGAB;
        set
        {
            if (value)
            {
                _isGAB = true;
                _isAguilares = false;
                EmpresaSeleccionada = Empresa.GAB;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAguilaresChecked));
            }
        }
    }

    public bool IsAguilaresChecked
    {
        get => _isAguilares;
        set
        {
            if (value)
            {
                _isAguilares = true;
                _isGAB = false;
                EmpresaSeleccionada = Empresa.AGUILARES;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsGABChecked));
            }
        }
    }
}
