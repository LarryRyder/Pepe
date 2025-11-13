# ScreenIconWatcher

Ein schlankes .NET-6-Windows-Forms-Tool, das den Desktop von Windows 10/11 periodisch auf ein bestimmtes Icon prüft. Sobald das Icon erkannt wird, kann automatisch ein frei definierbarer Befehl (z. B. das Starten einer EXE) ausgeführt werden.

## Voraussetzungen

- Windows 10 oder 11
- .NET 6 SDK (für Anpassungen oder eigene Builds)
- [OpenCvSharp](https://github.com/shimat/opencvsharp) wird automatisch als NuGet-Paket eingebunden
- Ein Referenzbild (PNG/JPG/BMP) des Icons, das erkannt werden soll

## Bedienung

1. Projekt einmalig wiederherstellen und bauen:
   ```powershell
   cd ScreenIconWatcher
   dotnet restore
   dotnet build --configuration Release
   ```
2. Anwendung starten (Debug-Build):
   ```powershell
   dotnet run --configuration Debug
   ```
3. Im Fenster das Referenzbild auswählen, optional Schwellwert/Intervall anpassen und bei Bedarf einen Befehl hinterlegen, der bei einem Treffer ausgeführt werden soll. Der Start/Stopp-Button aktiviert bzw. deaktiviert die Überwachung.

### Veröffentlichung als EXE

Für eine portable EXE (x64, selbstenthaltend) kann per Publish gebaut werden:

```powershell
cd ScreenIconWatcher
dotnet publish -c Release -r win10-x64 --self-contained true /p:PublishSingleFile=true
```

Die ausführbare Datei befindet sich anschließend unter `bin/Release/net6.0-windows/win10-x64/publish/ScreenIconWatcher.exe` und kann auf Windows 10/11 gestartet werden.

## Hinweise

- Der Standard-Schwellwert von 0,90 eignet sich für klare Icons. Bei falschen Treffern den Wert erhöhen.
- Das Aktionsfeld akzeptiert Befehl plus Argumente (z. B. `"C:\\Tools\\MyApp.exe" --flag`). Beim Treffer wird der Befehl mit einem Cooldown von 5 Sekunden erneut ausgeführt.
- Log-Meldungen werden im unteren Bereich des Fensters angezeigt.
