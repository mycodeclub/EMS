namespace EMS.Services.Common;

/// <summary>Outcome of a write. Errors are user-facing messages (add them to ModelState in controllers).</summary>
public class ServiceResult
{
    public bool Succeeded => Errors.Count == 0;
    public IReadOnlyList<string> Errors { get; }

    protected ServiceResult(IReadOnlyList<string> errors) => Errors = errors;

    public static ServiceResult Success() => new([]);
    public static ServiceResult Failure(params string[] errors) => new(errors);
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Data { get; }

    private ServiceResult(T? data, IReadOnlyList<string> errors) : base(errors) => Data = data;

    public static ServiceResult<T> Success(T data) => new(data, []);
    public static new ServiceResult<T> Failure(params string[] errors) => new(default, errors);
}
