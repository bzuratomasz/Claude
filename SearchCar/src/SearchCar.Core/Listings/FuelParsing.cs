using SearchCar.Core.Models;

namespace SearchCar.Core.Listings;

/// <summary>Recognises fuel and gearbox from the free text sites use ("Benzyna+LPG", "PETROL_LPG", "Hybryda (benzyna/elektryczny)"...).</summary>
public static class FuelParsing
{
    public static FuelType FromText(string? text)
    {
        var t = VehicleCatalog.Key(text ?? "");
        if (t.Length == 0) return FuelType.Any;
        if (t.Contains("lpg")) return FuelType.PetrolLpg;
        if (t.Contains("cng") || t.Contains("gazziemny")) return FuelType.PetrolCng;
        if (t.Contains("plugin") || t.Contains("phev")) return FuelType.PluginHybrid;
        if (t.Contains("hybr")) return FuelType.Hybrid;
        if (t.Contains("wodor") || t.Contains("hydrogen") || t.Contains("wasserstoff")) return FuelType.Hydrogen;
        if (t.Contains("etanol") || t.Contains("ethanol")) return FuelType.Ethanol;
        if (t.Contains("elektr") || t.Contains("electric")) return FuelType.Electric;
        if (t.Contains("diesel")) return FuelType.Diesel;
        if (t.Contains("benzyn") || t.Contains("petrol") || t.Contains("gasoline") || t.Contains("benzin")) return FuelType.Petrol;
        return FuelType.Any;
    }

    public static Gearbox GearboxFromText(string? text)
    {
        var t = VehicleCatalog.Key(text ?? "");
        if (t.Contains("manual")) return Gearbox.Manual;
        if (t.Contains("autom") || t.Contains("polautom") || t.Contains("dsg") || t.Contains("cvt")) return Gearbox.Automatic;
        return Gearbox.Any;
    }
}
