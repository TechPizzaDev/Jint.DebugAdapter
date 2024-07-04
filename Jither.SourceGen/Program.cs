using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Locator;

namespace Jither.SourceGen;

internal class Program
{
    public static async Task Main(string[] args)
    {
        string? outputPath = args.ElementAtOrDefault(0);
        string? projectUrl = args.ElementAtOrDefault(1);
        string? solutionUrl = args.ElementAtOrDefault(2);

        Console.WriteLine("Looking for MSBuild...");
        VisualStudioInstance[] instances = MSBuildLocator.QueryVisualStudioInstances().ToArray();
        foreach (VisualStudioInstance instance in instances)
        {
            Console.WriteLine($"Found \"{instance.Name}\" at \"{instance.MSBuildPath}\"");
        }

        Console.WriteLine("Registering MSBuild...");
        MSBuildLocator.RegisterDefaults();

        GeneratorRunner runner = new();
        await runner.RunAsync(outputPath, projectUrl, solutionUrl);
    }
}
