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

## Tresor abgleichen

- PC → iPhone: am PC Einstellungen → beim Speicherort „Ordner öffnen“, `vault.dat` nach iCloud Drive/OneDrive
  kopieren, in der iPhone-App „Tresor vom PC übernehmen“.
- iPhone → PC: in der App Einstellungen → „Tresor sichern“ → „In Dateien sichern“.
  Am PC App sperren, `vault.dat` im Datenordner ersetzen, entsperren.
- Die Datei liegt außerdem in der Dateien-App unter *Auf meinem iPhone → Password Vault*.

Immer nur auf einem Gerät ändern, dann abgleichen – beim Ersetzen gewinnt die neuere Kopie nicht automatisch.
