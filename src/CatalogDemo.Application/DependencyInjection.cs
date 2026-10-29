using Microsoft.Extensions.DependencyInjection;

namespace CatalogDemo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<Services.ICategoryService, Services.CategoryService>();
        services.AddScoped<Services.IProductService, Services.ProductService>();
        return services;
    }
}