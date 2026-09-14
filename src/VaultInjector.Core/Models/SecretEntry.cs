namespace VaultInjector.Core.Models;

/// <summary>One user-configured item shown in the paste context menu.</summary>
public sealed class SecretEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Display text (and alias) shown in the context menu.</summary>
    public string Alias { get; set; } = string.Empty;

    /// <summary>Secret path relative to <see cref="VaultConnectionSettings.BasePath"/>.</summary>
    public string RelativePath { get; set; } = string.Empty;

    public SecretFetchMode Mode { get; set; } = SecretFetchMode.SingleField;

    /// <summary>Required when <see cref="Mode"/> is <see cref="SecretFetchMode.SingleField"/>.</summary>
    public string? FieldName { get; set; }

    public SecretEntry Clone() => new()
    {
        Id = Id,
        Alias = Alias,
        RelativePath = RelativePath,
        Mode = Mode,
        FieldName = FieldName
    };

    public override string ToString() => string.IsNullOrWhiteSpace(Alias) ? RelativePath : Alias;
}
