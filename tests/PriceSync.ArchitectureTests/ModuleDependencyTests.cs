using FluentAssertions;
using Xunit;
using static PriceSync.ArchitectureTests.Support.RepositoryProjects;

namespace PriceSync.ArchitectureTests;

public sealed class ModuleDependencyTests
{
    private readonly IReadOnlyDictionary<string, Project> _projects = Project
        .LoadAll(FindRepositoryRoot(AppContext.BaseDirectory))
        .ToDictionary(project => project.Name);

    [Fact]
    public void Module_implementation_projects_depend_only_on_allowed_projects()
    {
        var allowedReferences = ModuleNames
            .Select(moduleName => $"PriceSync.Modules.{moduleName}.Contracts")
            .Append("PriceSync.SharedKernel")
            .Append("PriceSync.BuildingBlocks")
            .ToHashSet(StringComparer.Ordinal);

        foreach (var moduleName in ModuleNames)
        {
            var projectName = $"PriceSync.Modules.{moduleName}";

            _projects[projectName].ProjectReferences
                .Should()
                .OnlyContain(
                    reference => allowedReferences.Contains(reference),
                    $"{projectName} may reference shared kernel, technical building blocks and module contracts, but not module implementations");
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
    public void Technical_building_blocks_do_not_depend_on_modules()
    {
        _projects["PriceSync.BuildingBlocks"].ProjectReferences
            .Should()
            .NotContain(reference => reference.StartsWith("PriceSync.Modules.", StringComparison.Ordinal),
                "technical building blocks must remain independent from module business concepts");
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
}
