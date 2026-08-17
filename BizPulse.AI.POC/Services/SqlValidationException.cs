namespace BizPulse.AI.POC.Services;

public class SqlValidationException : Exception
{
    public SqlValidationException(string message)
        : base(message)
    {
    }
}