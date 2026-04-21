using GABRFIDLabeler.ViewModels;

namespace GABRFIDLabeler.Views;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        string _connectionString = "Server=tcp:189.206.160.206,2352;Database=GAB_Irapuato;User Id=sa;Password=Gabira1;Encrypt=True;TrustServerCertificate=True;";
        var viewModel = new PrinterViewModel(_connectionString);
        BindingContext = viewModel;

        // Suscribirse al evento para seleccionar el Entry
        viewModel.ReprintFinished += () =>
        {
            EpcEntry.Focus();
            EpcEntry.CursorPosition = 0;
            EpcEntry.SelectionLength = EpcEntry.Text?.Length ?? 0;
        };

        // Suscribirse al evento para seleccionar el Entry
        viewModel.ReprintNFinished += () =>
        {
            EpcEntryNEW.Focus();
            EpcEntryNEW.CursorPosition = 0;
            EpcEntryNEW.SelectionLength = EpcEntryNEW.Text?.Length ?? 0;
        };


    }

}
