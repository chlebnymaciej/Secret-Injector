namespace VaultInjector.Core.Models;

/// <summary>How the value pasted for a menu entry should be derived from the Vault secret.</summary>
public enum SecretFetchMode
{
    SingleField,
    WholeSecretAsJson
}
