namespace SearchCar.Core.Vin;

/// <summary>World Manufacturer Identifier (VIN positions 1-3) lookup for common European-market brands.</summary>
public static class WmiDirectory
{
    private static readonly Dictionary<string, string> Manufacturers = new(StringComparer.OrdinalIgnoreCase)
    {
        ["WVW"] = "Volkswagen", ["WV1"] = "Volkswagen", ["WV2"] = "Volkswagen", ["WVG"] = "Volkswagen",
        ["WAU"] = "Audi", ["WUA"] = "Audi", ["TRU"] = "Audi",
        ["WBA"] = "BMW", ["WBS"] = "BMW", ["WBY"] = "BMW",
        ["WDD"] = "Mercedes-Benz", ["WDB"] = "Mercedes-Benz", ["W1K"] = "Mercedes-Benz", ["W1N"] = "Mercedes-Benz",
        ["WP0"] = "Porsche", ["WP1"] = "Porsche",
        ["W0L"] = "Opel", ["W0V"] = "Opel",
        ["WF0"] = "Ford", ["1FA"] = "Ford", ["1FM"] = "Ford",
        ["TMB"] = "Skoda", ["VSS"] = "Seat",
        ["VF1"] = "Renault", ["UU1"] = "Dacia",
        ["VF3"] = "Peugeot", ["VR3"] = "Peugeot", ["VF7"] = "Citroen", ["VR7"] = "Citroen",
        ["ZFA"] = "Fiat", ["SUF"] = "Fiat", ["ZAR"] = "Alfa Romeo",
        ["YV1"] = "Volvo", ["LVY"] = "Volvo",
        ["JTD"] = "Toyota", ["JTN"] = "Toyota", ["JTM"] = "Toyota", ["SB1"] = "Toyota", ["NMT"] = "Toyota", ["VNK"] = "Toyota",
        ["JHM"] = "Honda", ["SHH"] = "Honda",
        ["JMZ"] = "Mazda",
        ["JN1"] = "Nissan", ["SJN"] = "Nissan",
        ["KMH"] = "Hyundai", ["TMA"] = "Hyundai",
        ["KNA"] = "Kia", ["KNE"] = "Kia", ["U5Y"] = "Kia",
        ["5YJ"] = "Tesla", ["7SA"] = "Tesla", ["LRW"] = "Tesla", ["XP7"] = "Tesla",
        ["SAL"] = "Land Rover", ["SAJ"] = "Jaguar",
        ["TSM"] = "Suzuki", ["JSA"] = "Suzuki",
        ["JMB"] = "Mitsubishi",
    };

    public static string? Manufacturer(string vin) =>
        vin.Length >= 3 && Manufacturers.TryGetValue(vin[..3], out var name) ? name : null;

    /// <summary>Approximate region of manufacture from the first VIN character.</summary>
    public static string Region(string vin)
    {
        if (vin.Length == 0)
            return "Nieznany";

        return vin[0] switch
        {
            '1' or '4' or '5' => "Ameryka Północna (USA)",
            '2' => "Ameryka Północna (Kanada)",
            '3' => "Ameryka Północna (Meksyk)",
            '6' or '7' => "Oceania",
            '8' or '9' => "Ameryka Południowa",
            'J' => "Azja (Japonia)",
            'K' => "Azja (Korea Południowa)",
            'L' => "Azja (Chiny)",
            >= 'M' and <= 'R' => "Azja",
            >= 'A' and <= 'H' => "Afryka",
            'S' => "Europa (Wielka Brytania / Polska)",
            'T' => "Europa (Czechy / Węgry / Szwajcaria / Portugalia)",
            'U' => "Europa (Rumunia / Słowacja / Dania)",
            'V' => "Europa (Francja / Hiszpania / Austria)",
            'W' => "Europa (Niemcy)",
            'X' => "Europa (Holandia / Rosja / Grecja)",
            'Y' => "Europa (Szwecja / Finlandia / Belgia)",
            'Z' => "Europa (Włochy)",
            _ => "Nieznany",
        };
    }
}
