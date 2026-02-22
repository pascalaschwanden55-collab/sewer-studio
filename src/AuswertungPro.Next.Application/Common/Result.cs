namespace AuswertungPro.Next.Application.Common;

public sealed class Result
{
    public bool Ok { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool ok, string? code, string? message)
    {
        Ok = ok;
        ErrorCode = code;
        ErrorMessage = message;
    }

    public static Result Success() => new(true, null, null);
    public static Result Fail(string code, string message) => new(false, code, message);
}

public sealed class Result<T>
{
    public bool Ok { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool ok, T? value, string? code, string? message)
    {
        Ok = ok;
        Value = value;
        ErrorCode = code;
        ErrorMessage = message;
    }

    public static Result<T> Success(T value) => new(true, value, null, null);
    public static Result<T> Fail(string code, string message) => new(false, default, code, message);
}
