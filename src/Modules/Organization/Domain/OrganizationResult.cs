namespace IUMP.Modules.Organization.Domain;

public sealed record Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public string Code { get; }
    public string? Error { get; }

    private Result(bool isSuccess, string code, string? error)
    {
        IsSuccess = isSuccess;
        Code = code;
        Error = error;
    }

    public static Result Success() => new(true, string.Empty, null);
    public static Result Failure(string code, string? error) => new(false, code, error);
}
