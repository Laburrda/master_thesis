namespace Shop.Api.Contracts.Errors
{
    public record ErrorResponse(ErrorDetails Error);

    public record ErrorDetails(string Code, string Message);

}
