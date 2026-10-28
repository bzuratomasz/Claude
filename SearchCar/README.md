# SearchCar

WinUI 3 desktop app (Polish UI, dark theme) that searches used-car classifieds on several Polish websites around a
location and verifies each offer's VIN.

## Sources

| Site | How it is searched | VIN |
|---|---|---|
| **otomoto.pl** | town + radius (up to 300 km), 32 offers/page | from description; otherwise "ukryty w serwisie" (hidden on the site, behind reCAPTCHA) |
| **autoplac.pl** | voivodeship/town + radius, 24 offers/page, coordinates in the list | published in clear text on the offer page |
| **sprzedajemy.pl** | town + radius (up to 150 km), 30/page; every offer page is read (slower) | behind "pokaż VIN" (show VIN) → hidden; or from description |
| **autotrader.pl** | voivodeship, 30/page; towns geocoded, distance checked by the app | from description |
| **motogratka.pl** | **first page only** for the town; its robots.txt forbids paging and radius | from description |
| **AutoScout24** (off by default) | coordinates + radius; mostly foreign (DE, AT…) offers, EUR → PLN at the NBP rate | from description |
| **OLX.pl** | blocks automated access (HTTP 403); the app builds the same search as a link to open in a browser | – |

Researched and not added: Gumtree.pl (closed); Allegro Lokalnie, mobile.de, samochody.pl (no usable listing pages);
autocentrum.pl (dealer and subscription offers without year, mileage or location); Carvago, Autohero, AAA Auto (online
dealers with delivery, not local classifieds).

All requests identify the app (`User-Agent: SearchCar/1.0`) and are throttled: at most 2 at a time per site, a pause between
result pages, and 1 request per second to OpenStreetMap Nominatim. Each site's robots.txt is respected. None of these sites has a public
API: the app reads the data embedded in their public pages, so a site redesign can break its provider. The other
sites keep working, and a failing site shows a note.

## Features

- **Filters:** make and model (191 makes and ~2,460 models from otomoto.pl, refreshable), year, mileage, price, fuel, gearbox.
- **Location on a dark OpenStreetMap map:** click the map, drag the pin, or type a place. The radius goes up to 300 km.
- **Results appear site by site** as each one answers. Offer pages are read in the background, and offers that turn out to be
  outside the radius or criteria are dropped.
- **VIN report** for every offer, with the **Otwórz ogłoszenie** (open offer) button and a box to paste a VIN revealed on the site. It checks
  length and characters, check digit, manufacturer, model year, the CEPIK flag, an online NHTSA decode, and whether the same VIN appears in other offers. The same VIN on
  another site with the same make and year is reported as the same car advertised twice, not as fraud.
- **Links** open each site's search, and the matching OLX search, in your browser.

## Build and run

Windows 10 1809+ / 11, .NET 9 SDK, WebView2 runtime, internet access.

```powershell
dotnet build SearchCar.sln
dotnet test tests\SearchCar.Core.Tests
.\src\SearchCar.App\bin\Debug\net9.0-windows10.0.19041.0\win-x64\SearchCar.exe
```

Installation package:

```powershell
.\build\New-InstallPackage.ps1 -Version 1.0.0          # tests + self-contained publish -> artifacts\SearchCar-1.0.0-win-x64.zip
.\build\New-InstallPackage.ps1 -Version 1.0.0 -Inno    # additionally SearchCar-Setup-1.0.0.exe (needs Inno Setup 6)
```

The ZIP contains `Instaluj.cmd`, which installs for the current user (no admin rights) into `%LOCALAPPDATA%\Programs\SearchCar` and creates
Start menu and desktop shortcuts plus an entry in Settings, Apps (uninstall). The target PC needs no .NET or Windows App SDK.

Adding a site: implement `ICarListingProvider` in `src/SearchCar.Core/Listings` and add it to the source list in `MainWindow.xaml.cs`.

## Layout

```
src/SearchCar.Core          models, geo (places, towns, Nominatim), site providers, catalog, VIN rules/decoder/verifier
src/SearchCar.App           WinUI 3 app (MainWindow, view models, Web/map.html)
tests/SearchCar.Core.Tests  xUnit tests (site parsers use small synthetic pages, no network)
```
