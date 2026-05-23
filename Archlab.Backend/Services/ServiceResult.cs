namespace Archlab.Backend.Services;

public sealed record ServiceError(string Message, int StatusCode);

public sealed record ServiceResult<T>(T? Value, ServiceError? Error)
{
    public bool Succeeded => Error is null;

    public static ServiceResult<T> Ok(T value) => new(value, null);

    public static ServiceResult<T> Fail(string message, int statusCode = StatusCodes.Status400BadRequest) =>
        new(default, new ServiceError(message, statusCode));
}
