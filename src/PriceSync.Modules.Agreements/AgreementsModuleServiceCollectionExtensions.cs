using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Persistence;

namespace PriceSync.Modules.Agreements;

public static class AgreementsModuleServiceCollectionExtensions
{
    public static IServiceCollection AddAgreementsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Agreements")
            ?? throw new InvalidOperationException("Connection string 'Agreements' is required.");
        services.AddDbContext<AgreementsDbContext>(options => options.UseNpgsql(connectionString,
            postgres => postgres.MigrationsHistoryTable(AgreementsDbContext.MigrationsHistoryTableName,
                AgreementsDbContext.SchemaName)));
        return services;
    }
}
