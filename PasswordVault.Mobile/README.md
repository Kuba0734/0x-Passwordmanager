# Password Vault für iPhone

Gleicher Tresor wie die Windows-App: `PasswordVault.Core` wird unverändert genutzt,
die Datei `vault.dat` ist zwischen PC und iPhone austauschbar.

Das Projekt ist absichtlich **nicht** in `PasswordVault.slnx`, damit Visual Studio ohne
iOS-Werkzeuge die Windows-App weiter normal öffnet und baut.

## Bauen (ohne Mac) – GitHub Actions

1. Privates Repository auf github.com anlegen und den Ordner hochladen (`git push`).
2. Jeder Push, der `PasswordVault.Mobile` oder `PasswordVault.Core` ändert, startet
   **Actions → iPhone-App bauen** (oder dort manuell „Run workflow“).
3. Nach ~10–15 Minuten unter dem Lauf → *Artifacts* → `PasswordVault-iPhone` herunterladen
   und entpacken → `PasswordVault.ipa`.

## Installieren – kostenlose Apple-ID

1. iTunes **von apple.com** installieren (nicht Microsoft Store) und [Sideloadly](https://sideloadly.io) installieren.
2. iPhone per Kabel anschließen, in Sideloadly `PasswordVault.ipa` wählen, Apple-ID eingeben, *Start*.
3. Auf dem iPhone: Einstellungen → Datenschutz & Sicherheit → **Entwicklermodus** an, neu starten.
4. Einstellungen → Allgemein → VPN & Geräteverwaltung → deiner Apple-ID vertrauen.

Die Signatur gilt **7 Tage**. Danach Schritt 2 wiederholen – die Daten in der App bleiben erhalten.

## Dauerhaft kostenlos nutzen

Apple begrenzt Apps, die mit einer kostenlosen Apple-ID signiert sind, auf 7 Tage. Das lässt sich nicht abschalten
(nur das Entwicklerprogramm für 99 €/Jahr signiert für ein Jahr), aber das Erneuern lässt sich automatisieren:

| Weg | Wie | PC nötig? |
|---|---|---|
| **SideStore** (empfohlen) | Einmal mit dem PC einrichten (Anleitung auf sidestore.io), danach `PasswordVault.ipa` in SideStore öffnen. SideStore erneuert die Signatur auf dem iPhone selbst – alle paar Tage „Refresh All“ tippen oder per Kurzbefehle-Automation täglich. | nur einmal |
| **AltStore + AltServer** | AltServer läuft auf dem PC, iPhone im selben WLAN: AltStore erneuert im Hintergrund automatisch. | ja, muss laufen |
| **Sideloadly** | Beim Installieren „Automatic refresh“ aktivieren und in iTunes „Mit diesem iPhone über WLAN synchronisieren“ einschalten. Sideloadly erneuert, solange es im Hintergrund läuft. | ja, muss laufen |

Wichtig:

- Immer **dieselbe Apple-ID** verwenden, sonst gilt die App als neu und Tresor sowie Face ID sind auf dem iPhone weg.
- Ist die Signatur doch einmal abgelaufen, startet die App nur nicht – einfach neu installieren, die Daten bleiben.
- Mit kostenloser Apple-ID sind höchstens 3 selbst installierte Apps gleichzeitig möglich.
- Regelmäßig „Tresor sichern“ nutzen, dann ist auch ein verlorenes iPhone kein Problem.

## Tresor abgleichen

- PC → iPhone: am PC Einstellungen → beim Speicherort „Ordner öffnen“, `vault.dat` nach iCloud Drive/OneDrive
  kopieren, in der iPhone-App „Tresor vom PC übernehmen“.
- iPhone → PC: in der App Einstellungen → „Tresor sichern“ → „In Dateien sichern“.
  Am PC App sperren, `vault.dat` im Datenordner ersetzen, entsperren.
- Die Datei liegt außerdem in der Dateien-App unter *Auf meinem iPhone → Password Vault*.

Immer nur auf einem Gerät ändern, dann abgleichen – beim Ersetzen gewinnt die neuere Kopie nicht automatisch.
