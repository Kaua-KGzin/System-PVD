using Pdv.Backend.Services;

namespace Pdv.Backend.Common;

public static class EndpointResultExtensions
{
    public static IResult ToHttpResult<T>(this ServiceResult<T> result)
    {
        if (result.Succeeded)
        {
            return Results.Ok(result.Value);
        }

        return Results.Problem(
            title: result.Error!.Message,
            statusCode: result.Error.StatusCode);
    }

    public static IResult ToCreatedResult<T>(this ServiceResult<T> result, Func<T, string> locationFactory)
        where T : notnull
    {
        if (result.Succeeded)
        {
            return Results.Created(locationFactory(result.Value!), result.Value);
        }

        return Results.Problem(
            title: result.Error!.Message,
            statusCode: result.Error.StatusCode);
    }
}
