using System.Xml.Linq;

namespace PriceSync.ArchitectureTests.Support;

internal static class RepositoryProjects
{
    internal static readonly string[] ModuleNames =
    [
        "OrganizationAccess",
        "TradingRelationships",
        "Agreements",
        "PricePublication",
        "CustomerPricing",
        "RetrievalConsumption",
        "IntegrationConfiguration",
    ];

    internal static string FindRepositoryRoot(string startDirectory)
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

    internal sealed record Project(string Name, IReadOnlyCollection<string> ProjectReferences)
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
