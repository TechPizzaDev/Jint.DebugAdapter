using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Jither.SourceGen.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.MSBuild;

namespace Jither.SourceGen;

internal class GeneratorRunner
{
    public async Task RunAsync(string outputPath, string projectUrl, string? solutionUrl)
    {
        Console.WriteLine("Creating MSBuildWorkspace...");
        MSBuildWorkspace workspace = MSBuildWorkspace.Create();

        Console.WriteLine($"Opening project: \"{projectUrl}\"");
        Project project = await workspace.OpenProjectAsync(projectUrl, new Progress<ProjectLoadProgress>(p =>
        {
            Console.WriteLine($"  {p.Operation} for \"{p.FilePath}\" took {ToReadable(p.ElapsedTime)}");
        }));

        Console.WriteLine($"Getting compilation for {project.Name}...");
        Compilation? projectCompilation = await project.GetCompilationAsync();
        if (null == projectCompilation)
        {
            throw new Exception("Failed to get compilation.");
        }

        List<IIncrementalGenerator> generators = [new VisitInheritorsSourceGen()];

        List<ISourceGenerator> wrappedGenerators = generators.Select(GeneratorExtensions.AsSourceGenerator).ToList();

        CSharpParseOptions? parseOptions = project.ParseOptions as CSharpParseOptions;

        GeneratorDriverOptions driverOptions = new(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);

        Console.WriteLine("Creating GeneratorDriver...");
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            wrappedGenerators,
            parseOptions: parseOptions,
            driverOptions: driverOptions);

        Console.WriteLine("Running generators...");
        driver = driver.RunGenerators(projectCompilation);

        GeneratorDriverRunResult driverResult = driver.GetRunResult();
        GeneratorDriverTimingInfo timingInfo = driver.GetTimingInfo();

        foreach (GeneratorRunResult genResult in driverResult.Results)
        {
            if (genResult.Exception != null)
            {
                Console.WriteLine($"Exception in {genResult.Generator.GetType().Name}:");
                Console.WriteLine(genResult.Exception);
                continue;
            }

            using IndentedTextWriter stepLog = new();
            stepLog.WriteLine($"Steps of {genResult.Generator.GetType().Name}:");
            using (stepLog.IncreaseIndent())
            {
                foreach ((string name, ImmutableArray<IncrementalGeneratorRunStep> steps) in genResult.TrackedSteps)
                {
                    stepLog.WriteLine($"{name}:");
                    using (stepLog.IncreaseIndent())
                    {
                        foreach (IncrementalGeneratorRunStep step in steps)
                        {
                            PrintStep(name != step.Name, step, stepLog);
                        }
                    }
                }
            }
            Console.WriteLine(stepLog.ToString());

            if (wrappedGenerators.Contains(genResult.Generator))
            {
                using IndentedTextWriter sourcesLog = new();
                sourcesLog.WriteLine($"Emitting {genResult.GeneratedSources.Length} generated sources");
                using (sourcesLog.IncreaseIndent())
                {
                    foreach (GeneratedSourceResult source in genResult.GeneratedSources)
                    {
                        string docPath = Path.Combine(outputPath, source.HintName);
                        string? docDir = Path.GetDirectoryName(docPath);

                        sourcesLog.WriteLine(Path.GetFullPath(docPath));

#pragma warning disable RS1035 // Do not use APIs banned for analyzers
                        Directory.CreateDirectory(docDir!);
                        File.WriteAllText(docPath, source.SourceText.ToString());
#pragma warning restore RS1035 // Do not use APIs banned for analyzers
                    }
                }
                Console.WriteLine(sourcesLog.ToString());
            }
        }
    }

    private static void PrintStep(bool header, IncrementalGeneratorRunStep step, IndentedTextWriter writer)
    {
        if (header && step.Name != null)
        {
            writer.Write($"{step.Name} ");
        }
        writer.WriteLine($" {ToReadable(step.ElapsedTime)}: {step.Inputs.Length} in -> {step.Outputs.Length} out");
    }

    private static string ToReadable(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMilliseconds < 10)
        {
            return $"{timeSpan.TotalMilliseconds:0.0}ms";
        }
        return $"{timeSpan.TotalMilliseconds:0}ms";
    }
}
