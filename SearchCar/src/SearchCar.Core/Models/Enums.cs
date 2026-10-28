namespace SearchCar.Core.Models;

/// <summary>Fuel types as offered by otomoto.pl.</summary>
public enum FuelType { Any, Petrol, PetrolLpg, PetrolCng, Diesel, Hybrid, PluginHybrid, Electric, Ethanol, Hydrogen }

public enum Gearbox { Any, Manual, Automatic }

public enum VinSource
{
    None,
    /// <summary>Published by the site as plain data.</summary>
    Listing,
    /// <summary>Found in the seller's description text.</summary>
    Description,
    /// <summary>Typed in by the user (e.g. after revealing it on the site).</summary>
    Manual,
}

public static class FuelTypeNames
{
    /// <summary>Polish names, as used on otomoto.pl.</summary>
    public static string Display(FuelType fuel) => fuel switch
    {
        FuelType.Any => "Dowolne",
        FuelType.Petrol => "Benzyna",
        FuelType.PetrolLpg => "Benzyna + LPG",
        FuelType.PetrolCng => "Benzyna + CNG",
        FuelType.Diesel => "Diesel",
        FuelType.Hybrid => "Hybryda",
        FuelType.PluginHybrid => "Hybryda plug-in",
        FuelType.Electric => "Elektryczny",
        FuelType.Ethanol => "Etanol",
        FuelType.Hydrogen => "Wodór",
        _ => fuel.ToString(),
    };

    public static string Display(Gearbox gearbox) => gearbox switch
    {
        Gearbox.Manual => "Manualna",
        Gearbox.Automatic => "Automatyczna",
        _ => "Dowolna",
    };
}
