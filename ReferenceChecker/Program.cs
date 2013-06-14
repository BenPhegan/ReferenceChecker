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
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var directory = string.Empty;
            var verbose = false;
            var help = false;
            var output = string.Empty;

            var exceptions = string.Empty;
            var assembliesToIgnore = string.Empty;
            var expectedRoots = string.Empty;

            var options = new OptionSet
                {
                    {"d|directory=","The directory to check runtime dependencies for.", v => directory = v},
                    {"v|verbose", "Verbose logging",v => verbose = v != null},
                    {"h|?|help", "Show help.", v => help = v != null},
                    {"o|output=","File to output DGML graph of references to.", v => output = v},
                    {"e|exceptions=", "A semi-colon delimited list of exclusions (accepts wildcards)",v => exceptions = v},
                    {"i|ignore=","List of wildards matching assemblies to ignore when graphing", v => assembliesToIgnore = v},
                    {"r|roots=", "A semi-colon delimited list of expected roots.",v => expectedRoots = v},
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

            var exclusions = WildcardListFromString(exceptions);
            var ignoreWildcards = WildcardListFromString(assembliesToIgnore);
            var rootsList = expectedRoots.Split(';').ToList();

            var files = new ConcurrentBag<string>(Directory.GetFiles(directory, "*.dll").Concat(Directory.GetFiles(directory, "*.exe")));

            var grapher = new AssemblyReferenceGrapher(new FileSystem(), new GacResolver());
            var graph = grapher.GenerateAssemblyReferenceGraph(exclusions, ignoreWildcards, files, verbose);

            var roots = graph.Vertices.Where(v => graph.InDegree(v) == 0).ToList();
            var missingButExcluded = graph.Vertices.Where(m => m.Excluded && !m.Exists);
            var failures = graph.Vertices.Where(m => !m.Exists && !m.Excluded);

            OutputList(roots, "Roots...", a => a.AssemblyName.FullName, a => a.AssemblyName.ToString());
            OutputList(missingButExcluded, "Missing but excluded...", a => a.AssemblyName.FullName, a => a.AssemblyName.ToString());
            var exitCode = OutputList(failures, "Missing...", a => a.AssemblyName.FullName, a => a.AssemblyName.ToString());

            if (rootsList.Any())
            {
                var missingRoots = rootsList.Where(er => !roots.Any(r => r.AssemblyName.Name.Equals(er,StringComparison.OrdinalIgnoreCase))).ToList();
                var additionalRoots = roots.Where(r => !rootsList.Any(er => er.Equals(r.AssemblyName.Name, StringComparison.OrdinalIgnoreCase))).ToList();
                exitCode = exitCode + OutputList(missingRoots, "Missing Roots...", a => a, a => a);
                exitCode = exitCode + OutputList(additionalRoots, "Additonal Roots...", a => a.AssemblyName.FullName, a => a.AssemblyName.ToString());
            }

            if (!string.IsNullOrEmpty(output))
            {
                AssemblyReferenceGrapher.OutputToDgml(output, graph);
            }

            Environment.Exit(exitCode);
        }

        private static int OutputList<T>(IEnumerable<T> list, string heading, Func<T,string> orderBy, Func<T,string> outputString)
        {
            var returnCode = 0;
            var assemblyVertices = list as T[] ?? list.ToArray();
            if (assemblyVertices.Any())
            {
                Console.WriteLine();
                Console.WriteLine(heading);
                assemblyVertices.OrderByDescending(orderBy).ToList().ForEach(m => Console.WriteLine("\t" + outputString(m)));
                returnCode = assemblyVertices.Count();
            }
            return returnCode;
        }

        private static IEnumerable<Regex> WildcardListFromString(string exceptions)
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