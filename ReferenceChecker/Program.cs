using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Options;
using ReferenceChecker.Gac;


namespace ReferenceChecker
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var directory = string.Empty;
            var verbose = false;
            var help = false;
            var output = string.Empty;

            string exceptions = null;
            var options = new OptionSet()
                {
                    {"d|directory=","The directory to check runtime dependencies for.", v => directory = v},
                    {"v|verbose", "Verbose logging",v => verbose = v != null},
                    {"h|?|help", "Show help.", v => help = v != null},
                    {"o|output=","File to output DGML graph of references to.", v => output = v},
                    {"e|exceptions=", "A semi-colon delimited list of exclusiosn (accepts wildcards)",v => exceptions = v}
                };

            var extra = options.Parse(args);

            if (extra.Any() || help)
            {
                OutputHelpAndExit(options);
            }

            if (!Directory.Exists(directory))
            {
                Console.WriteLine("Directory does not exist.");
                OutputHelpAndExit(options);
            }
            if (string.IsNullOrEmpty(directory))
            {
                Console.WriteLine("Please provide a directory.");
                OutputHelpAndExit(options);
            }

            var exclusions = GetExcludedWildcards(exceptions);
            var files = new ConcurrentBag<string>(Directory.GetFiles(directory, "*.dll").Concat(Directory.GetFiles(directory, "*.exe")));

            var grapher = new AssemblyReferenceGrapher(new FileSystem(), new GacResolver());
            var graph = grapher.GenerateAssemblyReferenceGraph(exclusions, files, verbose);

            var roots = graph.Vertices.Where(v => graph.InDegree(v) == 0).ToList();
            var missingButExcluded = graph.Vertices.Where(m => m.Excluded && !m.Exists);
            var failures = graph.Vertices.Where(m => !m.Exists && !m.Excluded);


            OutputAssemblyVertexList(roots, "Roots...");
            OutputAssemblyVertexList(missingButExcluded, "Missing but excluded...");
            var exitCode = OutputAssemblyVertexList(failures, "Missing...");

            if (!string.IsNullOrEmpty(output))
            {
                AssemblyReferenceGrapher.OutputToDgml(output, graph);
            }

            Environment.Exit(exitCode);
        }

        private static int OutputAssemblyVertexList(IEnumerable<AssemblyVertex> list, string heading)
        {
            var returnCode = 0;
            var assemblyVertices = list as AssemblyVertex[] ?? list.ToArray();
            if (assemblyVertices.Any())
            {
                Console.WriteLine();
                Console.WriteLine(heading);
                assemblyVertices.ToList().ForEach(m => Console.WriteLine("\t" + m.AssemblyName));
                returnCode = assemblyVertices.Count();
            }
            return returnCode;
        }

        private static IEnumerable<Regex> GetExcludedWildcards(string exceptions)
        {
            if (string.IsNullOrEmpty(exceptions))
                return new List<Regex>();
            var wildcards = exceptions.Split(';').Select(s => s.ToLowerInvariant());
            return new List<Regex>(wildcards.Select(w => new Wildcard(w)));
        }

        private static void OutputHelpAndExit(OptionSet options)
        {
            options.WriteOptionDescriptions(Console.Out);
            Environment.Exit(1);
        }
    }
}