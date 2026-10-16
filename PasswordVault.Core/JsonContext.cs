using System.Text.Json.Serialization;
using PasswordVault.Core.Models;

namespace PasswordVault.Core;

[JsonSerializable(typeof(VaultMetadata))]
[JsonSerializable(typeof(List<PasswordEntry>))]
[JsonSerializable(typeof(AppSettings))]
public partial class VaultJsonContext : JsonSerializerContext
{
}