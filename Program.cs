using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.Cecil;
using Mono.Options;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Serialization;


namespace ReferenceChecker
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var directory = string.Empty;
            bool verbose;
            bool help;
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

            if (extra.Any())
            {
                OutputHelpAndExit(options);
            }

            if (string.IsNullOrEmpty(directory))
            {
                OutputHelpAndExit(options);
            }

            var exclusions = GetExcludedWildcards(exceptions);
            var files = new ConcurrentBag<string>(Directory.GetFiles(directory, "*.dll").Concat(Directory.GetFiles(directory, "*.exe")));

            var edges = new ConcurrentBag<EquatableEdge<AssemblyVertex>>();
            Parallel.ForEach(files, file =>
                {
                    var assembly = AssemblyDefinition.ReadAssembly(file);
                    foreach (var reference in assembly.MainModule.AssemblyReferences)
                    {
                        var exists = files.Any(f =>
                            {
                                var fileInfo = new FileInfo(f);
                                return reference.Name.Equals(fileInfo.Name.Replace(fileInfo.Extension, ""));
                            });
                        if (!exists)
                        {
                            string assemblyPath;
                            exists = GacResolver.AssemblyExists(reference.FullName,out assemblyPath);
                        }
                        edges.Add(new EquatableEdge<AssemblyVertex>(
                                      new AssemblyVertex
                                          {
                                              AssemblyName = new AssemblyName(assembly.FullName),
                                              Exists = true
                                          }, new AssemblyVertex
                                              {
                                                  AssemblyName = new AssemblyName(reference.FullName),
                                                  Exists = exists
                                              }));
                    }
                });

            var graph = new AdjacencyGraph<AssemblyVertex, EquatableEdge<AssemblyVertex>>();
            var allVertices = edges.Select(e => e.Source).Concat(edges.Select(e => e.Target));
            var distinctVertices = allVertices.DistinctBy(v => v.AssemblyName.FullName);
            graph.AddVertexRange(distinctVertices);
            graph.AddEdgeRange(edges);
            var sources = graph.Edges.Select(e => e.Source).Distinct();
            var targets = graph.Edges.Select(e => e.Target).Distinct();
            var roots = sources.Where(s => !targets.Contains(s));
            var missing = graph.Vertices.Where(v => !v.Exists);
            var matchedExluded = missing.Where(m => exclusions.Any(e => e.IsMatch(m.AssemblyName.Name.ToLowerInvariant())));
            var failures = missing.Where(m => !matchedExluded.Any(e => e.Equals(m)));

            if (!string.IsNullOrEmpty(output))
            {
                graph.ToDirectedGraphML(graph.GetVertexIdentity(),graph.GetEdgeIdentity(),(n,d) =>
                    {
                        d.Label = n.AssemblyName.Name + " " + n.AssemblyName.Version;
                        if (!n.Exists)
                            d.Background = "Red";
                    },(e,l) => l.Label = "").WriteXml(output);
            }
            var exitCode = 0;
            if (roots.Any())
            {
                Console.WriteLine("Roots....");
                roots.ToList().ForEach(r => Console.WriteLine("\t"+r.AssemblyName));
            }
            if (matchedExluded.Any())
            {
                Console.WriteLine("Missing but excluded...");
                matchedExluded.ToList().ForEach(m => Console.WriteLine("\t" + m.AssemblyName));
            }
            if (failures.Any())
            {
                Console.WriteLine("Missing...");
                failures.ToList().ForEach(m => Console.WriteLine("\t" + m.AssemblyName));
                exitCode = failures.Count();
            }
            Environment.Exit(exitCode);
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

    internal class AssemblyVertex 
    {
        protected bool Equals(AssemblyVertex other)
        {
            return Equals(AssemblyName.FullName, other.AssemblyName.FullName) && Exists.Equals(other.Exists);
        }

        public override int GetHashCode()
        {
            var hashCode = 0;
            if (AssemblyName != null)
            {
                hashCode = hashCode ^ AssemblyName.FullName.GetHashCode();
            }

            return hashCode ^ Exists.GetHashCode();
        }

        public AssemblyName AssemblyName;
        public Boolean Exists;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AssemblyVertex) obj);
        }
    }

    public static class Extensions
    {
        public static IEnumerable<TSource> DistinctBy<TSource, TKey>(this IEnumerable<TSource> source, Func<TSource, TKey> keySelector)
        {
            var knownKeys = new HashSet<TKey>();
            return source.Where(element => knownKeys.Add(keySelector(element)));
        }
    }
}