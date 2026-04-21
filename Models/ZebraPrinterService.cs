using Microsoft.Data.SqlClient;
using System.Data.SqlClient;
using System.Drawing;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using Zebra.Sdk.Comm;
using Zebra.Sdk.Printer;

namespace GABRFIDLabeler.Models;

public class ZebraPrinterService
{
    private ZebraPrinter _printer;
    private Connection _connection;
    private string _connectionString;
    private TcpClient _tcpClient;
    private NetworkStream _stream;

    public ZebraPrinterService(string dbConnectionString)
    {
        _connectionString = dbConnectionString;
    }

    public async Task<bool> ConnectAsync(string printerIp)
    {
        _connection = new TcpConnection(printerIp, TcpConnection.DEFAULT_ZPL_TCP_PORT);
        try
        {
            _connection.Open();
            _printer = ZebraPrinterFactory.GetInstance(_connection);
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al conectar: {ex.Message}");
            return false;
        }
    }

    public async Task PrintMultipleLabelsAsync(string zplAll)
    {
        if (_printer == null) return;
        try
        {
            _printer.SendCommand(zplAll); // envía todo el lote de etiquetas
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al imprimir: {ex.Message}");
        }
    }


    #region LAST VERSION
    public async Task PrintAndWriteRfidBatchAsync(string fechaCompra, int inicialLabel, int totalLabels)
    {
        //if (_printer != null || totalLabels <= 0) return;

        try
        {
            // Generamos todo el ZPL en un solo comando
            string zplBatchCommand = GenerateBatchZPL(fechaCompra, inicialLabel, totalLabels);

            // Enviamos todo el lote a la impresora de una vez
            _printer.SendCommand(zplBatchCommand);

            // Realizamos todas las inserciones en un solo lote
            await BulkInsertLabels(fechaCompra, inicialLabel, totalLabels);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al imprimir lote: {ex.Message}");
            throw;
        }
    }

    private string GenerateBatchZPL(string fechaCompra, int inicialLabel, int totalLabels)
    {
        var zplBuilder = new StringBuilder();
        int finalLabel = inicialLabel + totalLabels - 1;

        for (int i = inicialLabel + 1; i <= finalLabel + 1; i++)
        {
            string increment = i.ToString("D6");
            string text = $"080-M7623-{increment}";
            string epc = $"7623{increment}";
            //string date = "MAR-2025";
            string date = fechaCompra;
            //string name = $"Cajon {i}";
            string name = $"{i}";

            //zplBuilder.AppendLine($@"^XA
            //                    ~SD25
            //                    ^PQ2
            //                    ^FO30,50^BQN,2,7^FDLA,{text}^FS
            //                    ^FO20,250^A0N,45,45,^FD{date}^FS
            //                    ^RFW,H^FD{epc}^FS
            //                    ^FO270,150^A0N,50,50,^FD{name}^FS
            //                    ^FO445,20^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
            //                    ^XZ");

            //zplBuilder.AppendLine($@"^XA
            //                    ^PW735
            //                    ^LL192
            //                    ~SD30
            //                    ^PON
            //                    ^LH0,0
            //                    ^FO20,12^BQN,2,7^FDLA,{text}^FS
            //                    ^FO250,125^A0N,50,50^FD{date}^FS
            //                    ^RFW,H^FD{epc}^FS
            //                    ^FO300,35^A0N,90,90^FD{name}^FS
            //                    ^FO665,10^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
            //                    ^FO640,80^GFA,850,850,10,1FE3CN01JF8,:1FE1CN01JF8,1FE3CN01JF8,:0FC3CN01JF8,I03CN01JF8,8007CN01JF8,C00FCN01JF8,E03FCN01JF8,JFCN01JF8,E01FCN01JF8,8007CN01JF8,:I03CN01JF8,0FC3CN01JF8,1FE3CN01JF8,:::8FC7CN01JF8,S01JF8,:::JFCN01JF8,I03CN01JF8,:I038N01JF8,:FFC78N01JF8,FFE38N01JF8,::FFE18N01JF8,FF838N01JF8,I038N01JF8,I078N01JF8,I0F8N01JF8,JF8N01JF8,:1IF8N01JF8,:0IF8N01JF8,8IF8N01JF8,I038N01JF8,:::JF8N01JF8,J08N01JF8,S01JF8,::8FC7O01JF8,1FE38N01JF8,1FE3CN01JF8,:1FE3CN01JF,1FC38N01JF,0303O03JF,I06O03JF,:001E2N07JF,3IFEN07IFE,1JFN07IFE,1JFN0JFE,0JFM01JFC,0JF8L01JFC,07IF8L03JFC,03IFCL07JF8,01IFE3J01KF8,01IFE1EI03KF,00JF0FE01LF,007IF87NFE,003IFC3NFC,I0IFE1NFC,I07IF0NF8,I03IF83MF,J0IFC1LFE,J03FFE07KF8,K0IF81JFE,K03FFE07IF8,L07FFC07FC,M03FF8,^FS
            //                    ^XZ");

            zplBuilder.AppendLine($@"^XA
                                ~SD25
                                ^PQ2
                                ^FO20,12^BQN,2,7^FDLA,{text}^FS
                                ^FO250,125^A0N,50,50^FD{date}^FS
                                ^FO300,35^A0N,90,90^FD{name}^FS
                                ^FO665,10^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
                                ^FO640,80^GFA,850,850,10,1FE3CN01JF8,:1FE1CN01JF8,1FE3CN01JF8,:0FC3CN01JF8,I03CN01JF8,8007CN01JF8,C00FCN01JF8,E03FCN01JF8,JFCN01JF8,E01FCN01JF8,8007CN01JF8,:I03CN01JF8,0FC3CN01JF8,1FE3CN01JF8,:::8FC7CN01JF8,S01JF8,:::JFCN01JF8,I03CN01JF8,:I038N01JF8,:FFC78N01JF8,FFE38N01JF8,::FFE18N01JF8,FF838N01JF8,I038N01JF8,I078N01JF8,I0F8N01JF8,JF8N01JF8,:1IF8N01JF8,:0IF8N01JF8,8IF8N01JF8,I038N01JF8,:::JF8N01JF8,J08N01JF8,S01JF8,::8FC7O01JF8,1FE38N01JF8,1FE3CN01JF8,:1FE3CN01JF,1FC38N01JF,0303O03JF,I06O03JF,:001E2N07JF,3IFEN07IFE,1JFN07IFE,1JFN0JFE,0JFM01JFC,0JF8L01JFC,07IF8L03JFC,03IFCL07JF8,01IFE3J01KF8,01IFE1EI03KF,00JF0FE01LF,007IF87NFE,003IFC3NFC,I0IFE1NFC,I07IF0NF8,I03IF83MF,J0IFC1LFE,J03FFE07KF8,K0IF81JFE,K03FFE07IF8,L07FFC07FC,M03FF8,^FS
                                ^XZ");
        }

        return zplBuilder.ToString();
    }

    private async Task BulkInsertLabels(string fechaCompra, int inicialLabel, int totalLabels)
    {
        if (string.IsNullOrEmpty(_connectionString)) return;

        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            await connection.OpenAsync();

            // Iniciamos una transacción para mejor performance
            using (SqlTransaction transaction = connection.BeginTransaction())
            {
                try
                {
                    //string query = @"INSERT INTO [dbo].[Tb_RFID_Catalogo] 
                    //            ([IdClaveTag], [IdClaveInt], [IdStatus], [FechaCompra]) 
                    //            VALUES (@IdClaveTag, @IdClaveInt, @IdStatus, @FechaCompra)";
                    string query = @"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[Tb_RFID_Catalogo] WHERE [IdClaveInt] = @IdClaveInt)
                BEGIN
                    INSERT INTO [dbo].[Tb_RFID_Catalogo] 
                    ([IdClaveTag], [IdClaveInt], [IdStatus], [FechaCompra]) 
                    VALUES (@IdClaveTag, @IdClaveInt, @IdStatus, @FechaCompra)
                END";

                    int finalLabel = inicialLabel + totalLabels - 1;

                    for (int i = inicialLabel + 1; i <= finalLabel + 1; i++)
                    {
                        string increment = i.ToString("D6");
                        string text = $"080-M7623-{increment}";
                        string epc = $"7623{increment}00000000000000";
                        //string date = "MAR-2025";
                        string date = fechaCompra;

                        using (SqlCommand command = new SqlCommand(query, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@IdClaveTag", epc);
                            command.Parameters.AddWithValue("@IdClaveInt", text);
                            command.Parameters.AddWithValue("@IdStatus", "1");
                            command.Parameters.AddWithValue("@FechaCompra", date);

                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    // Confirmamos la transacción si todo fue bien
                    await transaction.CommitAsync();
                }
                catch
                {
                    // Si hay error, hacemos rollback
                    await transaction.RollbackAsync();
                    throw;
                }
            }
        }
    }
    #endregion

    private async Task PrintSingleLabel(string text, string epc, string date, string name)
    {
        try
        {
            string zplCommand = $@"^XA
                                  ^FO50,50^A0N,30,30^FD{text}^FS
                                  ^RFW,H^FD{epc}^FS
                                  ^BY2^BCN,100,Y,N^FD{text}^FS
                                  ^XZ";
            string zplCommand2 = $@"^XA
                                    ~SD25
                                    ^FO30,50^BQN,2,7^FDLA,{text}^FS
                                    ^FO20,250^A0N,45,45,^FD{date}^FS
                                    ^RFW,H^FD{epc}^FS
                                    ^FO270,150^A0N,50,50,^FD{name}^FS
                                    ^FO445,20^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
                                    ^XZ";

            _printer.SendCommand(zplCommand2);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al imprimir etiqueta: {ex.Message}");
            throw;
        }
    }

    private async Task InsertIntoDatabase(string text, string epc, string date)
    {
        using (SqlConnection connection = new SqlConnection(_connectionString))
        {
            try
            {
                await connection.OpenAsync();
                string query = @"INSERT INTO [dbo].[Tb_RFID_Catalogo] 
                                ([IdClaveTag], [IdClaveInt], [IdStatus], [FechaCompra]) 
                                VALUES (@IdClaveTag, @IdClaveInt, @IdStatus, @FechaCompra)";

                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@IdClaveTag", epc);
                    command.Parameters.AddWithValue("@IdClaveInt", text);
                    command.Parameters.AddWithValue("@IdStatus", "1");
                    command.Parameters.AddWithValue("@FechaCompra", date);

                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al insertar en BD: {ex.Message}");
                throw;
            }
        }
    }

    public async Task PrintAndWriteRfidAsync(string text, string epc, string date, string name)
    {
        if (_printer == null) return;
        try
        {
            string zplCommand = $@"^XA
                                  ^FO50,50^A0N,30,30^FD{text}^FS
                                  ^RFW,H^FD{epc}^FS
                                  ^BY2^BCN,100,Y,N^FD{text}^FS
                                  ^XZ";
            string zplCommand2 = $@"^XA
                                    ~SD28
                                    ^PR2
                                    ^FO20,30^BQN,2,7^FDLA,{text}^FS
                                    ^FO20,240^A0N,50,50,^FD{date}^FS
                                    ^RFW,H^FD{epc}^FS
                                    ^FO270,130^A0N,90,90,^FD{name}^FS
                                    ^FO445,20^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
                                    ^XZ";

            string password = "C3494D32";
            string defaultPassword = "00000000";
            //^RFW,H,1,12,1 ^ FD{ epc}^FS

            string zplCommand4 = $@"^XA
                                ~SD25
                                ^RS,10,500,1,E^FS
                                ^RZ{defaultPassword},P^FS
                                ^RFW,H,1,4,3^FD{password}^FS
                                ^RFW,H^FD{epc}^FS
                                ^RLE,P^FS
                                ^FO20,12^BQN,2,7^FDLA,{text}^FS
                                ^FO250,125^A0N,50,50^FD{date}^FS
                                ^FO300,35^A0N,90,90^FD{name}^FS
                                ^FO665,10^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
                                ^FO640,80^GFA,850,850,10,1FE3CN01JF8,:1FE1CN01JF8,1FE3CN01JF8,:0FC3CN01JF8,I03CN01JF8,8007CN01JF8,C00FCN01JF8,E03FCN01JF8,JFCN01JF8,E01FCN01JF8,8007CN01JF8,:I03CN01JF8,0FC3CN01JF8,1FE3CN01JF8,:::8FC7CN01JF8,S01JF8,:::JFCN01JF8,I03CN01JF8,:I038N01JF8,:FFC78N01JF8,FFE38N01JF8,::FFE18N01JF8,FF838N01JF8,I038N01JF8,I078N01JF8,I0F8N01JF8,JF8N01JF8,:1IF8N01JF8,:0IF8N01JF8,8IF8N01JF8,I038N01JF8,:::JF8N01JF8,J08N01JF8,S01JF8,::8FC7O01JF8,1FE38N01JF8,1FE3CN01JF8,:1FE3CN01JF,1FC38N01JF,0303O03JF,I06O03JF,:001E2N07JF,3IFEN07IFE,1JFN07IFE,1JFN0JFE,0JFM01JFC,0JF8L01JFC,07IF8L03JFC,03IFCL07JF8,01IFE3J01KF8,01IFE1EI03KF,00JF0FE01LF,007IF87NFE,003IFC3NFC,I0IFE1NFC,I07IF0NF8,I03IF83MF,J0IFC1LFE,J03FFE07KF8,K0IF81JFE,K03FFE07IF8,L07FFC07FC,M03FF8,^FS
                                ^XZ";

            string zplRFID = $@"^XA
^RS,10,,1,E^FS
^RFW,H,1,12,1^FD1234567890ABCDE123456789^FS
^FO50,50^A0N,50,50^FDSIN VOID^FS
^XZ";

            _printer.SendCommand(zplCommand2);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al imprimir: {ex.Message}");
        }
    }

    public async Task PrintAndWritesRfidAsync(string text, string epc, string date, string name)
    {
        if (_printer == null) return;

        try
        {
            // LAYOUT OPTIMIZADO 65x40mm - Zonas definidas:
            // Zona A (Y=20-120): Sin inlay - Impresión nítida
            // Zona B (Y=130-220): INLAY RFID - Evitar elementos críticos
            // Zona C (Y=230-310): Sin inlay - Impresión nítida

            string zplCommand = $@"
^XA
^MMT                    // Modo continuo
^PW520                  // 65mm = ~520 puntos (203dpi)
^LL320                  // 40mm = ~320 puntos

// === CONFIGURACIÓN POR ZONAS ===

// ZONA A (Y=20-120) - Alta densidad, sin inlay
~SD22                   // Densidad alta para calidad máxima
^FO20,20^BQN,2,6^FDLA,{text}^FS      // QR Code arriba (Y=20)
^FO20,140^A0N,35,35^FD{name}^FS      // Nombre ajustado a Y=140 (justo antes del inlay)

// ZONA B (Y=130-220) - Solo RFID, NO imprimir aquí
~SD12                   // Bajar densidad (si hay algo que imprimir)
^RFW,H^FD{epc}^FS        // Escritura RFID (no visible, no afecta calidad)

// ZONA C (Y=230-310) - Alta densidad, sin inlay  
~SD22                   // Restaurar densidad alta
^FO20,250^A0N,30,30^FD{date}^FS      // Fecha abajo (Y=250)

// Logo GFA - Mantener en esquina superior derecha (Y=20, fuera de inlay)
^FO400,15^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS

^XZ";

            // Comando de seguridad RFID (tu lógica actual)
            string zplSecurity = $@"
^XA
^RS,10,500,1,E^FS
^RZ00000000,P^FS
^RFW,H,1,4,3^FDC3494D32^FS
^RLE,P^FS
^XZ";

            _printer.SendCommand(zplCommand);
            // Opcional: _printer.SendCommand(zplSecurity);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al imprimir: {ex.Message}");
            // TODO: Loggear en tu sistema de auditoría GAB RFID
        }
    }

    bool EsEpcValido(string epc)
    {
        return Regex.IsMatch(epc, @"^[0-9A-F]{24}$");
    }


    public async Task PrintAndWriteNERfidAsync(string text, string epc, string date, string name)
    {
        if (_printer == null) return;
        try
        {
            EsEpcValido(epc);
            string zplCommand = $@"^XA
                                  ^FO50,50^A0N,30,30^FD{text}^FS
                                  ^RFW,H^FD{epc}^FS
                                  ^BY2^BCN,100,Y,N^FD{text}^FS
                                  ^XZ";
            string zplCommand2 = $@"^XA
                                    ~SD25
                                    ^FO30,50^BQN,2,7^FDLA,{text}^FS
                                    ^FO20,250^A0N,45,45,^FD{date}^FS
                                    ^RFW,H^FD{epc}^FS
                                    ^FO270,150^A0N,50,50,^FD{name}^FS
                                    ^FO445,20^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
                                    ^XZ";

            string password = "C3494D32";
            string defaultPassword = "00000000";
            //^RFW,H,1,12,1 ^ FD{ epc}^FS

            string zplCommand4 = $@"^XA
                                ^PW736
                                ^LL228
                                ^LH0,20
                                ^PON
                                ^MNY
                                ~SD30
                                ^RS,10,500,1,E^FS
                                ^RZ{defaultPassword},P^FS
                                ^RFW,H,1,4,3^FD{password}^FS
                                ^RFW,H^FD{epc}^FS
                                ^RLE,P^FS
                                ^FO20,13^BQN,2,7^FDLA,{text}^FS
                                ^FO250,30^A0N,90,90^FD{name}^FS
                                ^FO250,130^A0N,50,50^FD{date}^FS
                                ^FO660,13^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
                                ^FO640,90^GFA,850,850,10,1FE3CN01JF8,:1FE1CN01JF8,1FE3CN01JF8,:0FC3CN01JF8,I03CN01JF8,8007CN01JF8,C00FCN01JF8,E03FCN01JF8,JFCN01JF8,E01FCN01JF8,8007CN01JF8,:I03CN01JF8,0FC3CN01JF8,1FE3CN01JF8,:::8FC7CN01JF8,S01JF8,:::JFCN01JF8,I03CN01JF8,:I038N01JF8,:FFC78N01JF8,FFE38N01JF8,::FFE18N01JF8,FF838N01JF8,I038N01JF8,I078N01JF8,I0F8N01JF8,JF8N01JF8,:1IF8N01JF8,:0IF8N01JF8,8IF8N01JF8,I038N01JF8,:::JF8N01JF8,J08N01JF8,S01JF8,::8FC7O01JF8,1FE38N01JF8,1FE3CN01JF8,:1FE3CN01JF,1FC38N01JF,0303O03JF,I06O03JF,:001E2N07JF,3IFEN07IFE,1JFN07IFE,1JFN0JFE,0JFM01JFC,0JF8L01JFC,07IF8L03JFC,03IFCL07JF8,01IFE3J01KF8,01IFE1EI03KF,00JF0FE01LF,007IF87NFE,003IFC3NFC,I0IFE1NFC,I07IF0NF8,I03IF83MF,J0IFC1LFE,J03FFE07KF8,K0IF81JFE,K03FFE07IF8,L07FFC07FC,M03FF8,^FS
                                ^XZ";

            string zplRFID = $@"^XA
^RS,10,,1,E^FS
^RFW,H,1,12,1^FD1234567890ABCDE123456789^FS
^FO50,50^A0N,50,50^FDSIN VOID^FS
^XZ";

            _printer.SendCommand(zplCommand4);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al imprimir: {ex.Message}");
        }
    }

    public async Task PrintAndWriteNEWRfidAsync(string text, string epc, string date, string name)
    {
        if (_printer == null) return;

        try
        {
            EsEpcValido(epc);

            var nameBox = new Rectangle(280, 30, 300, 90);
            nameBox = RfidSafeZone.AjustarSiInvadeIC(nameBox);

            string zplCommand = $@"^XA
^PW736
^LL228
^LH0,20
^PON
^MNY
~SD30
^RS,10,500,1,E^FS
^RZ00000000,P^FS
^RFW,H,1,4,3^FDC3494D32^FS
^RFW,H^FD{epc}^FS
^RLE,P^FS

^FO20,13^BQN,2,7^FDLA,{text}^FS
^FO{nameBox.X},{nameBox.Y}^A0N,90,90^FD{name}^FS
^FO580,13^A0R,35,35^FD{date}^FS

^FO660,13^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
                                ^FO640,90^GFA,850,850,10,1FE3CN01JF8,:1FE1CN01JF8,1FE3CN01JF8,:0FC3CN01JF8,I03CN01JF8,8007CN01JF8,C00FCN01JF8,E03FCN01JF8,JFCN01JF8,E01FCN01JF8,8007CN01JF8,:I03CN01JF8,0FC3CN01JF8,1FE3CN01JF8,:::8FC7CN01JF8,S01JF8,:::JFCN01JF8,I03CN01JF8,:I038N01JF8,:FFC78N01JF8,FFE38N01JF8,::FFE18N01JF8,FF838N01JF8,I038N01JF8,I078N01JF8,I0F8N01JF8,JF8N01JF8,:1IF8N01JF8,:0IF8N01JF8,8IF8N01JF8,I038N01JF8,:::JF8N01JF8,J08N01JF8,S01JF8,::8FC7O01JF8,1FE38N01JF8,1FE3CN01JF8,:1FE3CN01JF,1FC38N01JF,0303O03JF,I06O03JF,:001E2N07JF,3IFEN07IFE,1JFN07IFE,1JFN0JFE,0JFM01JFC,0JF8L01JFC,07IF8L03JFC,03IFCL07JF8,01IFE3J01KF8,01IFE1EI03KF,00JF0FE01LF,007IF87NFE,003IFC3NFC,I0IFE1NFC,I07IF0NF8,I03IF83MF,J0IFC1LFE,J03FFE07KF8,K0IF81JFE,K03FFE07IF8,L07FFC07FC,M03FF8,^FS
                                ^XZ";

            _printer.SendCommand(zplCommand);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al imprimir: {ex.Message}");
        }
    }

    private (string mes, string anio) ExtraerMesAnio(string date)
    {
        DateTime dt;

        if (!DateTime.TryParse(date, out dt))
            dt = DateTime.Now;

        string mes = dt.ToString("MMM", new CultureInfo("es-MX")).ToUpper();
        string anio = dt.Year.ToString();

        return (mes.Replace(".", ""), anio);
    }
    private (string big1, string big2) ExtraerBigDesdeNumeroDividido(string input)
    {
        if (!int.TryParse(input, out int valor))
            throw new Exception("El valor BIG no es un número válido");

        if (valor < 1 || valor > 10000)
            throw new Exception("El valor BIG debe estar entre 1 y 10000");

        string s = valor.ToString();

        if (s.Length == 1)
            return (s, s);           // 7 → 7 | 7

        if (s.Length == 2)
            return (s.Substring(0, 1), s.Substring(1, 1));   // 77 → 7 | 7

        if (s.Length == 3)
            return (s.Substring(0, 2), s.Substring(2, 1));   // 105 → 10 | 5

        // 4 dígitos (9084 → 90 | 84)
        return (s.Substring(0, 2), s.Substring(2, 2));
    }

    public async Task PrintRfidAutoAsync(string text, string epc, string date, string name)
    {
        if (_printer == null) return;

        try
        {
            EsEpcValido(epc);

            var (big1, big2) = ExtraerBigDesdeNumeroDividido(name);
            var (mes, anio) = ExtraerMesAnio(date);

            string zpl = $@"^XA
^PW736
^LL228
^LH0,0
^PON
^MNY
~SD30

^RS,10,500,1,E^FS
^RZ00000000,P^FS
^RFW,H,1,4,3^FDC3494D32^FS
^RFW,H^FD{epc}^FS
^RLE,P^FS

^FO20,13^BQN,2,7^FDLA,{text}^FS
^FO200,50^A0N,90,85^FD{big1}^FS
^FO300,50^A0N,90,85^FD{big2}^FS
^FO500,50^A0N,50,50^FD{mes}^FS
^FO500,100^A0N,50,50^FD{anio}^FS

^FO660,13^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
^FO640,90^GFA,850,850,10,1FE3CN01JF8,:1FE1CN01JF8,1FE3CN01JF8,:0FC3CN01JF8,I03CN01JF8,8007CN01JF8,C00FCN01JF8,E03FCN01JF8,JFCN01JF8,E01FCN01JF8,8007CN01JF8,:I03CN01JF8,0FC3CN01JF8,1FE3CN01JF8,:::8FC7CN01JF8,S01JF8,:::JFCN01JF8,I03CN01JF8,:I038N01JF8,:FFC78N01JF8,FFE38N01JF8,::FFE18N01JF8,FF838N01JF8,I038N01JF8,I078N01JF8,I0F8N01JF8,JF8N01JF8,:1IF8N01JF8,:0IF8N01JF8,8IF8N01JF8,I038N01JF8,:::JF8N01JF8,J08N01JF8,S01JF8,::8FC7O01JF8,1FE38N01JF8,1FE3CN01JF8,:1FE3CN01JF,1FC38N01JF,0303O03JF,I06O03JF,:001E2N07JF,3IFEN07IFE,1JFN07IFE,1JFN0JFE,0JFM01JFC,0JF8L01JFC,07IF8L03JFC,03IFCL07JF8,01IFE3J01KF8,01IFE1EI03KF,00JF0FE01LF,007IF87NFE,003IFC3NFC,I0IFE1NFC,I07IF0NF8,I03IF83MF,J0IFC1LFE,J03FFE07KF8,K0IF81JFE,K03FFE07IF8,L07FFC07FC,M03FF8,^FS
^XZ";

            string zpl2 = $@"^XA
^PW736
^LL228
^LH0,20
^PON
^MNY
~SD30

^RS,10,500,1,E^FS
^RZ00000000,P^FS
^RFW,H,1,2,1^FDC3494D32^FS
^RFW,H,1,3,6^FD{epc}^FS
^RLE,2,2^FS

^FO20,13^BQN,2,7^FDLA,{text}^FS
^FO220,110^A0N,90,90^FD{name}^FS
^FO485,45^A0N,60,60^FD{mes}^FS
^FO485,100^A0N,60,60^FD{anio}^FS

^FO660,13^GFA,357,357,7,N03FC,M01IF8,M0JFE,L01KF8,L03F801FC,L03EI07E,3JFC18I03F,7JF8007FC0F8KF001IF078FL03IF87CFL03E1FC3CFL03803E1EFO01F1EFN0F0F0EFM01FCF8!FM03FC78!FM07FE78!:FM07FE38!FM07FE78!FM03FC78!:FN0F0F0EFP0F1EFO01E1EFO03E3CFO07C3CFO03878FQ0F8FP01F,FP03E,FP03C,FP038,F700FE33F8,F7F0FF73FE,F7F8FF73FF,F738C07387,F618C073838C,F738FC73839C,F7F8FC7383BC,F7F0FC7383BC,F770C073873C,F678C073FF3C,F638C073FC3C,F618C031E03C,FO03C,:QF8,:7PF,3OFE,^FS
^FO640,90^GFA,850,850,10,1FE3CN01JF8,:1FE1CN01JF8,1FE3CN01JF8,:0FC3CN01JF8,I03CN01JF8,8007CN01JF8,C00FCN01JF8,E03FCN01JF8,JFCN01JF8,E01FCN01JF8,8007CN01JF8,:I03CN01JF8,0FC3CN01JF8,1FE3CN01JF8,:::8FC7CN01JF8,S01JF8,:::JFCN01JF8,I03CN01JF8,:I038N01JF8,:FFC78N01JF8,FFE38N01JF8,::FFE18N01JF8,FF838N01JF8,I038N01JF8,I078N01JF8,I0F8N01JF8,JF8N01JF8,:1IF8N01JF8,:0IF8N01JF8,8IF8N01JF8,I038N01JF8,:::JF8N01JF8,J08N01JF8,S01JF8,::8FC7O01JF8,1FE38N01JF8,1FE3CN01JF8,:1FE3CN01JF,1FC38N01JF,0303O03JF,I06O03JF,:001E2N07JF,3IFEN07IFE,1JFN07IFE,1JFN0JFE,0JFM01JFC,0JF8L01JFC,07IF8L03JFC,03IFCL07JF8,01IFE3J01KF8,01IFE1EI03KF,00JF0FE01LF,007IF87NFE,003IFC3NFC,I0IFE1NFC,I07IF0NF8,I03IF83MF,J0IFC1LFE,J03FFE07KF8,K0IF81JFE,K03FFE07IF8,L07FFC07FC,M03FF8,^FS
^XZ";

            _printer.SendCommand(zpl2);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }



    public void Disconnect()
    {
        _connection?.Close();
    }
}
