namespace TransactionApi.Application.Queries.GetAccountInterest;

public class InvalidInterestParametersException : Exception
{
    public InvalidInterestParametersException(string message)
        : base(message)
    {
    }
}
