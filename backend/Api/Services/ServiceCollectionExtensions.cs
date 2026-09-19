namespace Api.Services;

/// <summary>
/// Registers the business-logic layer. Controllers never touch a repository or
/// the DbContext directly — always through a service
/// (LoanTracker_Stack_Rules.md [STACK_RULES]).
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddLoanTrackerServices(this IServiceCollection services)
    {
        services.AddScoped<IItemService, ItemService>();
        services.AddScoped<IBorrowerService, BorrowerService>();
        services.AddScoped<ILoanService, LoanService>();
        services.AddScoped<IItemCategoryService, ItemCategoryService>();
        services.AddScoped<ILoanStatusService, LoanStatusService>();

        return services;
    }
}
