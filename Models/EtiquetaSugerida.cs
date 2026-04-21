using CommunityToolkit.Mvvm.ComponentModel;

namespace ZebraRFIDApp.Models
{
    // El modificador 'partial' es vital para que CommunityToolkit genere el código de soporte
    public partial class EtiquetaSugerida : ObservableObject
    {
        [ObservableProperty]
        private string epc;

        [ObservableProperty]
        private bool isReimpreso;

        public EtiquetaSugerida(string epc)
        {
            Epc = epc;
            IsReimpreso = false;
        }
    }
}