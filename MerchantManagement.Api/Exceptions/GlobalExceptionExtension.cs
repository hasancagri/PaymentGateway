namespace MerchantManagement.Api.Exceptions;

public static class GlobalExceptionExtension
{
    public static void AddGlobalExceptionHandler(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();
    }
}