namespace IPFamilySwitcher.Services;

public sealed class FirewallOperationException : Exception
{
    public FirewallOperationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
