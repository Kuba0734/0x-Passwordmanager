using PasswordVault.Core.Models;

namespace PasswordVault.Core.Services;

public enum VaultChangeSource { App, Browser }

public sealed class VaultChangedEventArgs : EventArgs
{
    public VaultChangedEventArgs(VaultChangeSource source, string? entryId = null)
    {
        Source = source;
        EntryId = entryId;
    }

    public VaultChangeSource Source { get; }
    public string? EntryId { get; }
}

/// <summary>
/// Einzige Quelle der Wahrheit für den entsperrten Tresor.
/// Thread-sicher, weil App-UI und Browser-Bridge gleichzeitig darauf zugreifen.
/// Jede Änderung wird sofort verschlüsselt gespeichert.
/// </summary>
public sealed class VaultStore
{
    private readonly object _sync = new();
    private readonly VaultService _vault = new();
    private readonly VaultFileService _file = new();
    private List<PasswordEntry> _entries = new();
    private VaultMetadata? _metadata;

    public VaultStore(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    public event EventHandler<VaultChangedEventArgs>? Changed;
    public event EventHandler<VaultChangeSource>? Locked;
    public event EventHandler<VaultChangeSource>? Unlocked;

    public bool Exists => _file.Exists(FilePath);

    public bool IsUnlocked
    {
        get { lock (_sync) return _vault.IsUnlocked; }
    }

    public int Count
    {
        get { lock (_sync) return _entries.Count; }
    }

    /// <summary>
    /// Legt einen neuen, leeren Tresor an und entsperrt ihn.
    /// </summary>
    public void Create(string masterPassword, VaultChangeSource source = VaultChangeSource.App)
    {
        lock (_sync)
        {
            if (Exists)
                throw new InvalidOperationException("Es existiert bereits ein Tresor.");
            _metadata = _vault.CreateVault(masterPassword);
            _entries = new List<PasswordEntry>();
            SaveLocked();
        }
        Unlocked?.Invoke(this, source);
    }

    /// <summary>
    /// Entsperrt den Tresor. Wirft eine CryptographicException bei falschem Passwort.
    /// </summary>
    public void Unlock(string masterPassword, VaultChangeSource source = VaultChangeSource.App)
    {
        var (metadata, encrypted) = _file.Load(FilePath);
        lock (_sync)
        {
            try
            {
                _vault.Unlock(masterPassword, metadata);
                _entries = _vault.DecryptEntries(encrypted);
                _metadata = metadata;
            }
            catch
            {
                _vault.Lock();
                throw;
            }
        }
        Unlocked?.Invoke(this, source);
    }

    /// <summary>
    /// Prüft ein Passwort gegen die Datei, ohne den Zustand zu verändern.
    /// </summary>
    public bool VerifyPassword(string masterPassword)
    {
        try
        {
            var (metadata, encrypted) = _file.Load(FilePath);
            var probe = new VaultService();
            probe.Unlock(masterPassword, metadata);
            probe.DecryptEntries(encrypted);
            probe.Lock();
            return true;
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }
    }

    public void Lock(VaultChangeSource source = VaultChangeSource.App)
    {
        bool wasUnlocked;
        lock (_sync)
        {
            wasUnlocked = _vault.IsUnlocked;
            _vault.Lock();
            _entries = new List<PasswordEntry>();
            _metadata = null;
        }
        if (wasUnlocked)
            Locked?.Invoke(this, source);
    }

    /// <summary>
    /// Liefert Kopien aller Einträge (Änderungen daran wirken sich nicht auf den Tresor aus).
    /// </summary>
    public List<PasswordEntry> GetEntries()
    {
        lock (_sync)
        {
            EnsureUnlocked();
            return _entries.Select(e => e.Clone()).ToList();
        }
    }

    public PasswordEntry? Get(string id)
    {
        lock (_sync)
        {
            EnsureUnlocked();
            return _entries.FirstOrDefault(e => e.Id == id)?.Clone();
        }
    }

    public void Add(PasswordEntry entry, VaultChangeSource source = VaultChangeSource.App)
        => AddRange(new[] { entry }, source);

    public void AddRange(IEnumerable<PasswordEntry> entries, VaultChangeSource source = VaultChangeSource.App)
    {
        string? lastId = null;
        lock (_sync)
        {
            EnsureUnlocked();
            foreach (var entry in entries)
            {
                var copy = entry.Clone();
                if (string.IsNullOrEmpty(copy.Id) || _entries.Any(e => e.Id == copy.Id))
                    copy.Id = Guid.NewGuid().ToString();
                copy.CreatedAt = copy.ModifiedAt = DateTime.UtcNow;
                _entries.Add(copy);
                lastId = copy.Id;
                entry.Id = copy.Id;
            }
            SaveLocked();
        }
        Changed?.Invoke(this, new VaultChangedEventArgs(source, lastId));
    }

    public void Update(PasswordEntry entry, VaultChangeSource source = VaultChangeSource.App)
    {
        lock (_sync)
        {
            EnsureUnlocked();
            var idx = _entries.FindIndex(e => e.Id == entry.Id);
            if (idx < 0)
                throw new KeyNotFoundException("Eintrag existiert nicht mehr.");
            var copy = entry.Clone();
            copy.CreatedAt = _entries[idx].CreatedAt;
            copy.ModifiedAt = DateTime.UtcNow;
            _entries[idx] = copy;
            SaveLocked();
        }
        Changed?.Invoke(this, new VaultChangedEventArgs(source, entry.Id));
    }

    public bool Delete(string id, VaultChangeSource source = VaultChangeSource.App)
    {
        lock (_sync)
        {
            EnsureUnlocked();
            if (_entries.RemoveAll(e => e.Id == id) == 0) return false;
            SaveLocked();
        }
        Changed?.Invoke(this, new VaultChangedEventArgs(source, id));
        return true;
    }

    /// <summary>
    /// Einträge, deren URL zur angegebenen Seite passt – beste Treffer zuerst.
    /// </summary>
    public List<PasswordEntry> FindForUrl(string pageUrl)
    {
        lock (_sync)
        {
            EnsureUnlocked();
            return _entries
                .Select(e => (Entry: e, Score: UrlMatcher.MatchScore(e.Url, pageUrl)))
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenByDescending(x => x.Entry.IsFavorite)
                .ThenBy(x => x.Entry.Title, StringComparer.CurrentCultureIgnoreCase)
                .Select(x => x.Entry.Clone())
                .ToList();
        }
    }

    public void ChangeMasterPassword(string newPassword)
    {
        lock (_sync)
        {
            EnsureUnlocked();
            _metadata = _vault.Rekey(newPassword);
            SaveLocked();
        }
    }

    /// <summary>
    /// Kopiert die verschlüsselte Tresor-Datei (z.B. als Backup).
    /// </summary>
    public void Backup(string targetPath)
    {
        lock (_sync)
        {
            File.Copy(FilePath, targetPath, overwrite: true);
        }
    }

    private void SaveLocked()
    {
        var encrypted = _vault.EncryptEntries(_entries);
        _metadata!.LastOpenedAt = DateTime.UtcNow;
        _file.Save(FilePath, _metadata, encrypted);
    }

    private void EnsureUnlocked()
    {
        if (!_vault.IsUnlocked)
            throw new InvalidOperationException("Vault ist gesperrt.");
    }
}
