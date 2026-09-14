namespace VaultInjector.Core.Services;

public sealed class VaultServiceException : Exception
{
    public int? StatusCode { get; }

    public VaultServiceException(string message, int? statusCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
