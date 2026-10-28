using SearchCar.Core.Models;

namespace SearchCar.Core.Geo;

public sealed record PolishCity(string Name, string Region, GeoPoint Point);

/// <summary>Larger Polish towns with their voivodeship, used as a fallback search location when a site does not know the exact place.</summary>
public static class PolishCities
{
    public static IReadOnlyList<PolishCity> All { get; } =
    [
        new("Warszawa", "mazowieckie", new(52.2297, 21.0122)), new("Kraków", "małopolskie", new(50.0647, 19.9450)),
        new("Łódź", "łódzkie", new(51.7592, 19.4560)), new("Wrocław", "dolnośląskie", new(51.1079, 17.0385)),
        new("Poznań", "wielkopolskie", new(52.4064, 16.9252)), new("Gdańsk", "pomorskie", new(54.3520, 18.6466)),
        new("Szczecin", "zachodniopomorskie", new(53.4285, 14.5528)), new("Bydgoszcz", "kujawsko-pomorskie", new(53.1235, 18.0084)),
        new("Lublin", "lubelskie", new(51.2465, 22.5684)), new("Białystok", "podlaskie", new(53.1325, 23.1688)),
        new("Katowice", "śląskie", new(50.2649, 19.0238)), new("Gdynia", "pomorskie", new(54.5189, 18.5305)),
        new("Częstochowa", "śląskie", new(50.8118, 19.1203)), new("Radom", "mazowieckie", new(51.4027, 21.1471)),
        new("Toruń", "kujawsko-pomorskie", new(53.0138, 18.5984)), new("Rzeszów", "podkarpackie", new(50.0412, 21.9991)),
        new("Sosnowiec", "śląskie", new(50.2863, 19.1041)), new("Kielce", "świętokrzyskie", new(50.8661, 20.6286)),
        new("Gliwice", "śląskie", new(50.2945, 18.6714)), new("Olsztyn", "warmińsko-mazurskie", new(53.7784, 20.4801)),
        new("Zabrze", "śląskie", new(50.3249, 18.7857)), new("Bielsko-Biała", "śląskie", new(49.8224, 19.0584)),
        new("Bytom", "śląskie", new(50.3484, 18.9157)), new("Zielona Góra", "lubuskie", new(51.9356, 15.5062)),
        new("Rybnik", "śląskie", new(50.0971, 18.5463)), new("Ruda Śląska", "śląskie", new(50.2584, 18.8563)),
        new("Opole", "opolskie", new(50.6751, 17.9213)), new("Tychy", "śląskie", new(50.1218, 18.9876)),
        new("Gorzów Wielkopolski", "lubuskie", new(52.7368, 15.2288)), new("Elbląg", "warmińsko-mazurskie", new(54.1561, 19.4045)),
        new("Płock", "mazowieckie", new(52.5463, 19.7065)), new("Wałbrzych", "dolnośląskie", new(50.7714, 16.2843)),
        new("Włocławek", "kujawsko-pomorskie", new(52.6483, 19.0677)), new("Tarnów", "małopolskie", new(50.0121, 20.9858)),
        new("Chorzów", "śląskie", new(50.2975, 18.9545)), new("Koszalin", "zachodniopomorskie", new(54.1943, 16.1715)),
        new("Kalisz", "wielkopolskie", new(51.7611, 18.0910)), new("Legnica", "dolnośląskie", new(51.2070, 16.1553)),
        new("Grudziądz", "kujawsko-pomorskie", new(53.4837, 18.7536)), new("Słupsk", "pomorskie", new(54.4641, 17.0287)),
        new("Jaworzno", "śląskie", new(50.2050, 19.2739)), new("Jastrzębie-Zdrój", "śląskie", new(49.9554, 18.5917)),
        new("Nowy Sącz", "małopolskie", new(49.6218, 20.6970)), new("Jelenia Góra", "dolnośląskie", new(50.9044, 15.7194)),
        new("Siedlce", "mazowieckie", new(52.1676, 22.2902)), new("Konin", "wielkopolskie", new(52.2230, 18.2511)),
        new("Piła", "wielkopolskie", new(53.1514, 16.7378)), new("Piotrków Trybunalski", "łódzkie", new(51.4053, 19.7030)),
        new("Suwałki", "podlaskie", new(54.1118, 22.9309)), new("Ostrołęka", "mazowieckie", new(53.0862, 21.5752)),
        new("Łomża", "podlaskie", new(53.1781, 22.0590)), new("Zamość", "lubelskie", new(50.7231, 23.2520)),
        new("Przemyśl", "podkarpackie", new(49.7838, 22.7678)), new("Krosno", "podkarpackie", new(49.6888, 21.7706)),
        new("Chełm", "lubelskie", new(51.1431, 23.4716)), new("Biała Podlaska", "lubelskie", new(52.0325, 23.1149)),
        new("Leszno", "wielkopolskie", new(51.8403, 16.5749)), new("Ciechanów", "mazowieckie", new(52.8814, 20.6197)),
        new("Skierniewice", "łódzkie", new(51.9547, 20.1584)), new("Sieradz", "łódzkie", new(51.5955, 18.7302)),
    ];

    public static IEnumerable<(PolishCity City, double DistanceKm)> NearestTo(GeoPoint point) =>
        All.Select(c => (c, GeoMath.DistanceKm(point, c.Point))).OrderBy(c => c.Item2);
}
