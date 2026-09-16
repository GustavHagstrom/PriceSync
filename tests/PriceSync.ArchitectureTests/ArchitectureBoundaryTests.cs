using System.Xml.Linq;
using FluentAssertions;
using PriceSync.Modules.OrganizationAccess;
using PriceSync.Modules.OrganizationAccess.Contracts;
using Xunit;

namespace PriceSync.ArchitectureTests;

public sealed class ArchitectureBoundaryTests
{
    private static readonly string[] ModuleNames =
    [
        "OrganizationAccess",
        "TradingRelationships",
        "Agreements",
        "PricePublication",
        "CustomerPricing",
        "RetrievalConsumption",
        "IntegrationConfiguration",
    ];

    private readonly IReadOnlyDictionary<string, Project> _projects = Project
        .LoadAll(FindRepositoryRoot(AppContext.BaseDirectory))
        .ToDictionary(project => project.Name);

    [Fact]
    public void Solution_contains_the_established_modular_monolith_projects()
    {
        var expectedProjects = new List<string>
        {
            "PriceSync.Api",
            "PriceSync.Application",
            "PriceSync.SharedKernel",
            "PriceSync.ArchitectureTests",
        };

        expectedProjects.AddRange(ModuleNames.Select(moduleName => $"PriceSync.Modules.{moduleName}"));
        expectedProjects.AddRange(ModuleNames.Select(moduleName => $"PriceSync.Modules.{moduleName}.Contracts"));

        _projects.Keys.Should().Contain(expectedProjects);
    }

    [Fact]
    public void Module_implementation_projects_depend_only_on_allowed_projects()
    {
        var allowedReferences = ModuleNames
            .Select(moduleName => $"PriceSync.Modules.{moduleName}.Contracts")
            .Append("PriceSync.SharedKernel")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var moduleName in ModuleNames)
        {
            var projectName = $"PriceSync.Modules.{moduleName}";

            _projects[projectName].ProjectReferences
                .Should()
                .OnlyContain(
                    reference => allowedReferences.Contains(reference),
                    $"{projectName} may reference shared kernel and module contracts, but not module implementations");
        }
    }

    [Fact]
    public void Module_contract_projects_do_not_depend_on_module_implementations()
    {
        var moduleImplementations = ModuleNames
            .Select(moduleName => $"PriceSync.Modules.{moduleName}")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var moduleName in ModuleNames)
        {
            var projectName = $"PriceSync.Modules.{moduleName}.Contracts";

            _projects[projectName].ProjectReferences
                .Should()
                .NotContain(
                    reference => moduleImplementations.Contains(reference),
                    $"{projectName} must stay independent from module implementation projects");
        }
    }

    [Fact]
    public void Organization_access_contracts_exports_only_the_initial_boundary_types()
    {
        typeof(OrganizationId).Assembly.ExportedTypes
            .Select(type => type.FullName)
            .Should()
            .BeEquivalentTo(
                [
                    "PriceSync.Modules.OrganizationAccess.Contracts.OrganizationId",
                    "PriceSync.Modules.OrganizationAccess.Contracts.PersonId",
                    "PriceSync.Modules.OrganizationAccess.Contracts.ICurrentPerson",
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
    public void Organization_access_contract_identifiers_reject_empty_guids()
    {
        var createOrganizationId = () => new OrganizationId(Guid.Empty);
        var createPersonId = () => new PersonId(Guid.Empty);

        createOrganizationId.Should().Throw<ArgumentException>();
        createPersonId.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Shared_kernel_does_not_depend_on_modules()
    {
        _projects["PriceSync.SharedKernel"].ProjectReferences
            .Should()
            .NotContain(
                reference => reference.StartsWith("PriceSync.Modules.", StringComparison.Ordinal),
                "shared kernel must stay technical and independent from PriceSync module boundaries");
    }

    [Fact]
    public void Application_depends_only_on_shared_kernel_and_module_contracts()
    {
        _projects["PriceSync.Application"].ProjectReferences
            .Should()
            .OnlyContain(
                reference => reference == "PriceSync.SharedKernel"
                    || reference.StartsWith("PriceSync.Modules.", StringComparison.Ordinal)
                    && reference.EndsWith(".Contracts", StringComparison.Ordinal),
                "application orchestration must use module contracts rather than module implementations");
    }

    [Fact]
    public void Api_references_application_and_module_implementations_for_composition()
    {
        var expectedReferences = ModuleNames
            .Select(moduleName => $"PriceSync.Modules.{moduleName}")
            .Append("PriceSync.Application");

        _projects["PriceSync.Api"].ProjectReferences
            .Should()
            .Contain(expectedReferences);
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

        var violations = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(organizationAccessRoot, path).StartsWith("..", StringComparison.Ordinal))
            .ToArray();

        violations.Should().NotBeEmpty("the Organization & Access module should own the persistence implementation being protected");

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

    private static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "PriceSync.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate PriceSync.slnx.");
    }

    private sealed record Project(string Name, IReadOnlyCollection<string> ProjectReferences)
    {
        public static IEnumerable<Project> LoadAll(string repositoryRoot)
        {
            foreach (var projectFile in Directory.EnumerateFiles(repositoryRoot, "*.csproj", SearchOption.AllDirectories))
            {
                var document = XDocument.Load(projectFile);
                var projectDirectory = Path.GetDirectoryName(projectFile) ?? repositoryRoot;
                var references = document
                    .Descendants("ProjectReference")
                    .Select(reference => reference.Attribute("Include")?.Value)
                    .Where(include => !string.IsNullOrWhiteSpace(include))
                    .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
                    .Select(path => Path.GetFileNameWithoutExtension(path) ?? throw new InvalidOperationException($"Project reference path '{path}' has no file name."))
                    .Order(StringComparer.Ordinal)
                    .ToArray();

                yield return new Project(Path.GetFileNameWithoutExtension(projectFile), references);
            }
        }
    }
}
