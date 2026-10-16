# Password Vault

Lokaler Passwort-Manager für Windows, iPhone und Chromium-Browser. Der Tresor liegt verschlüsselt in einer einzigen Datei (`vault.dat`), die zwischen den Geräten austauschbar ist.

## Sicherheit

- Schlüssel aus dem Master-Passwort per **Argon2id** (64 MB, 4 Iterationen)
- Verschlüsselung mit **AES-GCM**
- Sicherheitsanalyse für schwache und doppelte Passwörter, Passwort-Generator

## Projekte

| Projekt | Inhalt |
|---|---|
| `PasswordVault.Core` | Krypto, Tresor, CSV-Import/-Export, Generator (.NET 8) |
| `PasswordVault.App` | Windows-App (WinUI 3) |
| `PasswordVault.Mobile` | iPhone-App (.NET MAUI), siehe [README](PasswordVault.Mobile/README.md) |
| `BrowserExtension` | Erweiterung für Chrome, Edge, Opera & Co., siehe [README](BrowserExtension/README.md) |
| `PasswordVault.Tests` | Tests (xUnit, FluentAssertions) |

## Bauen

`PasswordVault.slnx` in Visual Studio 2022 öffnen und `PasswordVault.App` starten.

Tests:

```bash
dotnet test PasswordVault.Tests
```

Die iPhone-App baut GitHub Actions (`.github/workflows/ios.yml`) bei Änderungen an `PasswordVault.Mobile` oder `PasswordVault.Core`.
