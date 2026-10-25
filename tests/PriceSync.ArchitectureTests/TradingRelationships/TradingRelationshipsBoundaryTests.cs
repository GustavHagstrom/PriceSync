using FluentAssertions;
using Xunit;
using static PriceSync.ArchitectureTests.Support.RepositoryProjects;

namespace PriceSync.ArchitectureTests.TradingRelationships;

public sealed class TradingRelationshipsBoundaryTests
{
    private readonly IReadOnlyDictionary<string, Project> _projects = Project
        .LoadAll(FindRepositoryRoot(AppContext.BaseDirectory))
        .ToDictionary(project => project.Name);

    [Fact]
    public void Other_feature_modules_do_not_reference_trading_relationships_implementation_or_persistence()
    {
        const string implementationProject = "PriceSync.Modules.TradingRelationships";
        var otherModuleProjects = ModuleNames
            .Where(moduleName => moduleName != "TradingRelationships")
            .SelectMany(moduleName => new[]
            {
                _projects[$"PriceSync.Modules.{moduleName}"],
                _projects[$"PriceSync.Modules.{moduleName}.Contracts"],
            });

        foreach (var project in otherModuleProjects)
        {
            project.ProjectReferences.Should().NotContain(
                implementationProject,
                $"{project.Name} must not reference TradingRelationships implementation, which also contains its persistence");
        }
    }

    [Fact]
    public void Api_host_references_trading_relationships_implementation_for_composition()
    {
        _projects["PriceSync.Api"].ProjectReferences
            .Should()
            .Contain(
                "PriceSync.Modules.TradingRelationships",
                "the API host is the composition root and must be allowed to register the module and map its endpoints");
    }

    [Fact]
    public void Trading_relationships_implementation_exports_only_composition_surfaces()
    {
        typeof(PriceSync.Modules.TradingRelationships.DependencyInjection).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .BeEquivalentTo(
                [
                    "PriceSync.Modules.TradingRelationships.DependencyInjection",
                    "PriceSync.Modules.TradingRelationships.Endpoints",
                ]);
    }

    [Fact]
    public void Trading_relationships_root_namespace_contains_only_composition_surfaces()
    {
        typeof(PriceSync.Modules.TradingRelationships.DependencyInjection).Assembly.GetTypes()
            .Where(type => !type.IsNested && type.Namespace == "PriceSync.Modules.TradingRelationships")
            .Select(type => type.Name)
            .Should().BeEquivalentTo(["DependencyInjection", "Endpoints"]);
    }

    [Fact]
    public void Trading_relationships_contracts_exports_only_identifier_and_agreement_scope_facts()
    {
        typeof(PriceSync.Modules.TradingRelationships.Contracts.TradingRelationshipId).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .BeEquivalentTo(
                [
                    "PriceSync.Modules.TradingRelationships.Contracts.TradingRelationshipId",
                    "PriceSync.Modules.TradingRelationships.Contracts.IAgreementScopeReader",
                    "PriceSync.Modules.TradingRelationships.Contracts.AgreementScope",
                    "PriceSync.Modules.TradingRelationships.Contracts.AgreementScopeStatus",
                ]);
    }

    [Fact]
    public void Trading_relationships_contracts_references_only_organization_identity_contracts()
    {
        _projects["PriceSync.Modules.TradingRelationships.Contracts"].ProjectReferences
            .Should()
            .BeEquivalentTo(["PriceSync.Modules.OrganizationAccess.Contracts"],
                "agreement scope exchanges the exact Organization-owned identifiers");
    }

    [Fact]
    public void Only_trading_relationships_references_its_persistence_schema_and_db_context()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var tradingRelationshipsRoot = Path.Combine(sourceRoot, "PriceSync.Modules.TradingRelationships");
        var forbiddenReferences = new[]
        {
            "trading_relationships",
            "TradingRelationshipsDbContext",
            "PriceSync.Modules.TradingRelationships.Persistence",
        };

        var ownedReferences = Directory
            .EnumerateFiles(tradingRelationshipsRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        ownedReferences.Should().Contain(
            contents => forbiddenReferences.Any(contents.Contains),
            "the TradingRelationships module should own the persistence implementation being protected");

        var crossModuleViolations = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(tradingRelationshipsRoot, path).StartsWith("..", StringComparison.Ordinal))
            .SelectMany(path =>
            {
                var contents = File.ReadAllText(path);
                return forbiddenReferences
                    .Where(contents.Contains)
                    .Select(forbiddenReference => $"{Path.GetRelativePath(repositoryRoot, path)} contains {forbiddenReference}");
            })
            .ToArray();

        crossModuleViolations
            .Should()
            .BeEmpty("other modules and hosts must not query the trading_relationships schema or couple to TradingRelationships persistence internals");
    }
}
