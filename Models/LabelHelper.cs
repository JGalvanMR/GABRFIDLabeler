using System;

namespace GABRFIDLabeler.Models
{
    public enum Empresa
    {
        GAB,
        AGUILARES
    }

    public static class LabelHelper
    {
        public static Empresa EmpresaActual { get; set; } = Empresa.GAB;

        public const int AGUILARES_MIN_NUMBER = 100000; // 100,000

        public static string GetIdClaveInt(int number)
        {
            switch (EmpresaActual)
            {
                case Empresa.GAB:
                    return $"080-M7623-{number:D6}";
                case Empresa.AGUILARES:
                    if (number < AGUILARES_MIN_NUMBER)
                        throw new ArgumentException($"Para AGUILARES el número debe ser >= {AGUILARES_MIN_NUMBER}");
                    return $"02-M7275-{number}";
                default:
                    return $"080-M7623-{number:D6}";
            }
        }

        public static string GetIdClaveTag(int number)
        {
            string baseTag;
            switch (EmpresaActual)
            {
                case Empresa.GAB:
                    baseTag = $"7623{number:D6}";
                    break;
                case Empresa.AGUILARES:
                    if (number < AGUILARES_MIN_NUMBER)
                        throw new ArgumentException($"Para AGUILARES el número debe ser >= {AGUILARES_MIN_NUMBER}");
                    baseTag = $"7275{number}";
                    break;
                default:
                    baseTag = $"7623{number:D6}";
                    break;
            }
            // Rellenar con ceros a la derecha hasta 24 caracteres
            return baseTag.PadRight(24, '0');
        }

        public static bool TryParseClaveInt(string claveInt, out int number, out Empresa empresa)
        {
            number = 0;
            empresa = Empresa.GAB;

            if (claveInt.StartsWith("080-M7623-"))
            {
                empresa = Empresa.GAB;
                string numStr = claveInt.Substring("080-M7623-".Length);
                return int.TryParse(numStr, out number);
            }
            if (claveInt.StartsWith("02-M7275-"))
            {
                empresa = Empresa.AGUILARES;
                string numStr = claveInt.Substring("02-M7275-".Length);
                return int.TryParse(numStr, out number);
            }
            return false;
        }
    }
}