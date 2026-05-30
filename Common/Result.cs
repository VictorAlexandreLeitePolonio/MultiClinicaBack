namespace ProjetoLP.API.Common;

public class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool isSuccess, T? value, string? errorCode, string? errorMessage)
    {
        IsSuccess    = isSuccess;
        Value        = value;
        ErrorCode    = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Ok(T value)
        => new(true, value, null, null);

    public static Result<T> Fail(string errorCode, string errorMessage)
        => new(false, default, errorCode, errorMessage);
}
