using FluentAssertions;
using Xunit;
using static PriceSync.ArchitectureTests.Support.RepositoryProjects;

namespace PriceSync.ArchitectureTests;

public sealed class SolutionStructureTests
{
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
}
