using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using PriceSync.Modules.Agreements.Persistence;
using PriceSync.Modules.Agreements.Features.Create;
using PriceSync.Modules.Agreements.Features.List;
using PriceSync.Modules.Agreements.Features.Get;

namespace PriceSync.Modules.Agreements;

public static class DependencyInjection
{
    public static IServiceCollection AddAgreementsModule(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Agreements")
            ?? throw new InvalidOperationException("Connection string 'Agreements' is required.");
        services.AddDbContext<AgreementsDbContext>(options => options.UseNpgsql(connectionString,
            postgres => postgres.MigrationsHistoryTable(AgreementsDbContext.MigrationsHistoryTableName,
                AgreementsDbContext.SchemaName)));
        services.AddSingleton(Domain.AgreementRoundingPolicy.Default);
        services.AddScoped<CreateHandler>();
        services.AddScoped<AgreementAcceptanceStore>();
        services.AddScoped<Acceptance.AcceptanceScopeReader>();
        services.AddScoped<Features.AcceptVersion.AcceptVersionHandler>();
        services.AddScoped<Features.GetVersionAcceptance.GetVersionAcceptanceHandler>();
        services.AddScoped<Features.PublishVersion.PublishVersionHandler>();
        services.AddScoped<Features.WithdrawVersion.WithdrawVersionHandler>();
        services.AddScoped<Features.WithdrawVersion.WithdrawVersionValidator>();
        services.AddScoped<ListHandler>();
        services.AddScoped<Features.Recommendation.RecommendationHandler>();
        services.AddScoped<Features.Recommendation.RecommendationValidator>();
        services.AddScoped<ListValidator>();
        services.AddScoped<GetHandler>();
        services.AddScoped<Features.ListVersions.ListVersionsHandler>();
        services.AddScoped<AgreementVersionReader>();
        services.AddScoped<Contracts.IAgreementVersionReader>(provider => provider.GetRequiredService<AgreementVersionReader>());
        services.AddScoped<Features.GetVersion.GetVersionHandler>();
        return services;
    }
}
