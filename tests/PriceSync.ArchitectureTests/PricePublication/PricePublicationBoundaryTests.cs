using FluentAssertions;
using Xunit;
using static PriceSync.ArchitectureTests.Support.RepositoryProjects;

namespace PriceSync.ArchitectureTests.PricePublication;

public sealed class PricePublicationBoundaryTests
{
    private readonly IReadOnlyDictionary<string, Project> _projects = Project
        .LoadAll(FindRepositoryRoot(AppContext.BaseDirectory))
        .ToDictionary(project => project.Name);

    [Fact]
    public void Other_feature_modules_do_not_reference_price_publication_implementation_or_persistence()
    {
        const string implementationProject = "PriceSync.Modules.PricePublication";
        var otherModuleProjects = ModuleNames
            .Where(moduleName => moduleName != "PricePublication")
            .SelectMany(moduleName => new[]
            {
                _projects[$"PriceSync.Modules.{moduleName}"],
                _projects[$"PriceSync.Modules.{moduleName}.Contracts"],
            });

        foreach (var project in otherModuleProjects)
        {
            project.ProjectReferences.Should().NotContain(
                implementationProject,
                $"{project.Name} must not reference PricePublication implementation, which also contains its persistence");
        }
    }

    [Fact]
    public void Api_host_references_price_publication_implementation_for_composition()
    {
        _projects["PriceSync.Api"].ProjectReferences
            .Should()
            .Contain(
                "PriceSync.Modules.PricePublication",
                "the API host is the composition root and must be allowed to register the module and map its endpoints");
    }

    [Fact]
    public void Price_publication_implementation_exports_only_composition_surfaces()
    {
        typeof(PriceSync.Modules.PricePublication.DependencyInjection).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .BeEquivalentTo(
                [
                    "PriceSync.Modules.PricePublication.DependencyInjection",
                    "PriceSync.Modules.PricePublication.Endpoints",
                ]);
    }

    [Fact]
    public void Price_publication_root_namespace_contains_only_composition_surfaces()
    {
        typeof(PriceSync.Modules.PricePublication.DependencyInjection).Assembly.GetTypes()
            .Where(type => !type.IsNested && type.Namespace == "PriceSync.Modules.PricePublication")
            .Select(type => type.Name)
            .Should().BeEquivalentTo(["DependencyInjection", "Endpoints"]);
    }

    [Fact]
    public void Price_publication_contracts_exports_only_scope_verification_and_identifiers()
    {
        typeof(PriceSync.Modules.PricePublication.Contracts.PriceListId).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .BeEquivalentTo(
                ["PriceSync.Modules.PricePublication.Contracts.PriceListId",
                    "PriceSync.Modules.PricePublication.Contracts.IPriceListScopeVerifier",
                    "PriceSync.Modules.PricePublication.Contracts.PriceListType",
                    "PriceSync.Modules.PricePublication.Contracts.PriceListReference",
                    "PriceSync.Modules.PricePublication.Contracts.VerifiedPriceListScope",
                    "PriceSync.Modules.PricePublication.Contracts.InvalidPriceListReferenceReason",
                    "PriceSync.Modules.PricePublication.Contracts.InvalidPriceListReference",
                    "PriceSync.Modules.PricePublication.Contracts.PriceListVerificationResult",
                    "PriceSync.Modules.PricePublication.Contracts.PriceListVerificationResult+Verified",
                    "PriceSync.Modules.PricePublication.Contracts.PriceListVerificationResult+Invalid",
                    "PriceSync.Modules.PricePublication.Contracts.PriceListVerificationResult+Unavailable",]);
    }

    [Fact]
    public void Price_publication_contracts_depends_only_on_organization_identity_contracts()
    {
        _projects["PriceSync.Modules.PricePublication.Contracts"].ProjectReferences
            .Should()
            .BeEquivalentTo(["PriceSync.Modules.OrganizationAccess.Contracts"], "scope verification exchanges strongly typed Organization IDs");
    }

    [Fact]
    public void Only_price_publication_references_its_persistence_schema_and_db_context()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var publicationRoot = Path.Combine(sourceRoot, "PriceSync.Modules.PricePublication");
        var forbiddenReferences = new[]
        {
            "price_publication",
            "PricePublicationDbContext",
            "PriceSync.Modules.PricePublication.Persistence",
        };

        var ownedReferences = Directory
            .EnumerateFiles(publicationRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        ownedReferences.Should().Contain(
            contents => forbiddenReferences.Any(contents.Contains),
            "the PricePublication module should own the persistence implementation being protected");

        var crossModuleViolations = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(publicationRoot, path).StartsWith("..", StringComparison.Ordinal))
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
            .BeEmpty("other modules and hosts must not query the price_publication schema or couple to PricePublication persistence internals");
    }
}
