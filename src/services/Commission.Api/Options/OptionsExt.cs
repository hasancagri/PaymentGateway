using Microsoft.Extensions.Options;

namespace Commission.Api.Options;

// House-style options bağlama (ECommerce WebApp/Extensions/OptionsExt.cs referans): section adı =
// POCO tip adı; DataAnnotations + ValidateOnStart; tüketici IOptions<T> değil düz POCO enjekte eder.
public static class OptionsExt
{
    public static IServiceCollection AddOptionsExt(this IServiceCollection services)
    {
        return services;
    }
}