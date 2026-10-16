# Password Vault – Browser-Erweiterung

Für Chrome, Edge, Brave, Opera (GX), Vivaldi und andere Chromium-Browser (Manifest V3).

## Installation

1. In der App: **Einstellungen → Browser-Erweiterung → Einrichten**. Die App kopiert die Erweiterung nach
   `Dokumente\PasswordVault Browser-Erweiterung` und öffnet den Ordner.
2. Im Browser `chrome://extensions` (bzw. `edge://extensions`, `opera://extensions`) öffnen.
3. **Entwicklermodus** einschalten → **Entpackte Erweiterung laden** → den Ordner auswählen.
4. Auf das Password-Vault-Symbol klicken → **Jetzt koppeln** → den angezeigten Code in der App mit **Erlauben** bestätigen.

## Funktionen

- **Vorschläge direkt im Login-Feld**: Klick ins Feld zeigt passende Logins für die Seite.
- **Ausfüllen** per Klick, im Popup oder mit `Strg+Umschalt+L`.
- **Speichern & Aktualisieren**: Nach dem Anmelden fragt ein Banner, ob neue oder geänderte Zugangsdaten gespeichert werden sollen.
- **Passwort-Vorschläge** bei Registrierungen und per Rechtsklick → „Sicheres Passwort einfügen“.
- **Popup**: Suche, Details, Kopieren, Generator, Entsperren/Sperren des Tresors.

## Sicherheit

- Die Erweiterung spricht nur mit der App auf `http://localhost:27832`. Es gibt keinen Server und keine Cloud.
- Jede Anfrage braucht ein Kopplungs-Token, das du in der App bestätigt hast. Das Token liegt im
  Erweiterungsspeicher und in der App im Windows-Anmeldeinformationstresor.
- Normale Webseiten können die App nicht ansprechen: Die App lehnt jede `http(s)`-Herkunft ab.
- Passwörter gehen nur an Frames, deren Herkunft zum gespeicherten Eintrag passt. Ein fremdes iframe auf
  einer Seite bekommt also nichts.
- Das Master-Passwort wird nur im Popup eingegeben, nie in einer Webseite.

## Icons neu erzeugen

```powershell
powershell -File tools\make-icons.ps1
```
