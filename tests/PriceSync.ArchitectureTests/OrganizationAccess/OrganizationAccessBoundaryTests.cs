using FluentAssertions;
using Xunit;
using static PriceSync.ArchitectureTests.Support.RepositoryProjects;
using PriceSync.Modules.OrganizationAccess;
using PriceSync.Modules.OrganizationAccess.Contracts;

namespace PriceSync.ArchitectureTests.OrganizationAccess;

public sealed class OrganizationAccessBoundaryTests
{
    private readonly IReadOnlyDictionary<string, Project> _projects = Project
        .LoadAll(FindRepositoryRoot(AppContext.BaseDirectory))
        .ToDictionary(project => project.Name);

    [Fact]
    public void Organization_access_contracts_exports_only_the_approved_boundary_types()
    {
        typeof(OrganizationId).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .BeEquivalentTo(
                [
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationId",
                    "PriceSync.Modules.OrganizationAccess.Contracts.PersonId",
                    "PriceSync.Modules.OrganizationAccess.Contracts.ICurrentPerson",
                    "PriceSync.Modules.OrganizationAccess.Contracts.ICurrentOrganization",
                    "PriceSync.Modules.OrganizationAccess.Contracts.IOrganizationAuthorizer",
                    "PriceSync.Modules.OrganizationAccess.Contracts.IOrganizationStatusReader",
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationCapability",
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationStatusResult",
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationContextErrors",
                ]);
    }

    [Fact]
    public void Organization_access_implementation_exports_only_composition_surfaces()
    {
        typeof(OrganizationAccessEndpointRouteBuilderExtensions).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .BeEquivalentTo(
                [
                    "PriceSync.Modules.OrganizationAccess.OrganizationAccessEndpointRouteBuilderExtensions",
                    "PriceSync.Modules.OrganizationAccess.OrganizationAccessModuleServiceCollectionExtensions",
                ]);
    }

    [Fact]
    public void Organization_membership_identity_remains_internal_to_organization_access()
    {
        typeof(OrganizationId).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .NotContain("PriceSync.Modules.OrganizationAccess.Contracts.OrganizationMembershipId");
    }

    [Fact]
    public void Organization_access_contracts_do_not_export_domain_creation_types()
    {
        typeof(OrganizationId).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .NotContain(
                [
                    "PriceSync.Modules.OrganizationAccess.Contracts.Organization",
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationMembership",
                    "PriceSync.Modules.OrganizationAccess.Contracts.InitialOrganizationState",
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationRole",
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationStatus",
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationMembershipStatus",
                    "PriceSync.Modules.OrganizationAccess.Contracts.LegalOrganizationIdentity",
                ]);
    }

    [Fact]
    public void Organization_access_contracts_has_no_project_references()
    {
        _projects["PriceSync.Modules.OrganizationAccess.Contracts"].ProjectReferences
            .Should()
            .BeEmpty("the initial Organization & Access contracts must not depend on shared kernel, module implementations, or helper abstractions");
    }

    [Fact]
    public void Only_organization_access_references_its_persistence_schema_and_db_context()
    {
        var repositoryRoot = FindRepositoryRoot(AppContext.BaseDirectory);
        var sourceRoot = Path.Combine(repositoryRoot, "src");
        var organizationAccessRoot = Path.Combine(sourceRoot, "PriceSync.Modules.OrganizationAccess");
        var forbiddenReferences = new[]
        {
            "organization_access",
            "OrganizationAccessDbContext",
            "PriceSync.Modules.OrganizationAccess.Persistence",
        };

        var ownedReferences = Directory
            .EnumerateFiles(organizationAccessRoot, "*.cs", SearchOption.AllDirectories)
            .Select(File.ReadAllText)
            .ToArray();

        ownedReferences.Should().Contain(
            contents => forbiddenReferences.Any(contents.Contains),
            "the Organization & Access module should own the persistence implementation being protected");

        var crossModuleViolations = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => Path.GetRelativePath(organizationAccessRoot, path).StartsWith("..", StringComparison.Ordinal))
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
            .BeEmpty("other modules and hosts must not query the organization_access schema or couple to Organization & Access persistence internals");
    }
}
