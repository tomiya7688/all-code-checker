namespace AllCodeChecker.Discovery;

public sealed class UnsupportedInputException : Exception
{
    public UnsupportedInputException(string message)
        : base(message)
    {
    }
}
