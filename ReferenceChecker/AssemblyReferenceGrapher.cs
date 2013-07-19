using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Mono.Cecil;
using QuickGraph;
using QuickGraph.Algorithms;
using QuickGraph.Serialization;
using ReferenceChecker.Gac;

namespace ReferenceChecker
{
    public class AssemblyReferenceGrapher
    {
        private readonly IFileSystem _fileSystem;
        private readonly IGacResolver _gacResolver;

        public AssemblyReferenceGrapher(IFileSystem fileSystem, IGacResolver gacResolver)
        {
            _fileSystem = fileSystem;
            _gacResolver = gacResolver;
        }
        public BidirectionalGraph<AssemblyVertex, EquatableEdge<AssemblyVertex>> GenerateAssemblyReferenceGraph(IEnumerable<Regex> exclusions, IEnumerable<Regex> ignoring, ConcurrentBag<string> files, bool verbose, bool checkAssemblyVersionMatch = true)
        {
            if (verbose) Console.WriteLine("Processing {0} files.", files.Count);
            var edges = new ConcurrentBag<EquatableEdge<AssemblyVertex>>();
            var vertices = new List<AssemblyVertex>();
            var ignoreList = ignoring.ToList();
            var excludeList = exclusions.ToList();

            var current = 0;
            var total = files.Count;
            Parallel.ForEach(files, file =>
                {
                    if (verbose) Console.Write("\rProcessing file: {0} of {1}", ++current, total);
                    AssemblyDefinition assembly;
                    try
                    {
                        assembly = AssemblyDefinition.ReadAssembly(new MemoryStream(_fileSystem.File.ReadAllBytes(file)));
                    }       
                    catch (Exception)
                    {
                        if (verbose) Console.WriteLine("Skipping file as it does not appear to be a .Net assembly: {0}", file);
                        return;
                    }
                    //We need to load the assembly to ensure that the assembly name is the same as the file name (to be exact)
                    if (ignoreList.Any(i => i.IsMatch(assembly.Name.Name.ToLowerInvariant())))
                    {
                        if (verbose) Console.WriteLine("Ignoring file: {0}", file);
                        return;
                    }

                    //Check for 32bitness on our source assembly
                    var required32Bit = Required32BitSet(assembly);
                    var assemblyName = new AssemblyName(assembly.FullName);
 
                    //If we dont have references (I know, unlikely right?)
                    if (!assembly.MainModule.AssemblyReferences.Any())
                    {
                        vertices.Add(CreateAssemblyVertex(assemblyName.FullName, assemblyName.Name, true, Required32BitSet(assembly), excludeList));
                        return;
                    }

                    //Otherwise create edges for them...
                    foreach (var reference in assembly.MainModule.AssemblyReferences)
                    {
                        var foundFileMatch = files.Any(f => CheckDependentFileMatchesManifest(f, reference, checkAssemblyVersionMatch, required32Bit));

                        if (!foundFileMatch)
                        {
                            foundFileMatch = _gacResolver.AssemblyExists(reference.FullName);
                        }
                        if (!ignoreList.Any(i => i.IsMatch(reference.Name.ToLowerInvariant())))
                            edges.Add(CreateNewEdge(reference, foundFileMatch, required32Bit, assemblyName, excludeList));
                        else
                            if (verbose) Console.WriteLine("Ignoring: {0}",assemblyName.Name);
                    }
                });

            if (verbose) Console.WriteLine();
            if (verbose) Console.WriteLine("Creating Graph...");
            var graph = new BidirectionalGraph<AssemblyVertex, EquatableEdge<AssemblyVertex>>();
            var allVertices = edges.Select(e => e.Source).Concat(edges.Select(e => e.Target)).Concat(vertices);
            var distinctVertices = allVertices.Distinct();
            graph.AddVertexRange(distinctVertices);
            graph.AddEdgeRange(edges);
            return graph;
        }

        private static bool Required32BitSet(AssemblyDefinition assembly)
        {
            return assembly.Modules.Any(m => m.Attributes.HasFlag(ModuleAttributes.Required32Bit));
        }

        private bool CheckDependentFileMatchesManifest(string filename, AssemblyNameReference reference, bool checkAssemblyVersionMatch, bool sourceRequires32Bit)
        {
            var fileInfo = new FileInfo(filename);
            var nameMatch = reference.Name.Equals(fileInfo.Name.Replace(fileInfo.Extension, ""), StringComparison.OrdinalIgnoreCase);
            var tempAssembly = AssemblyDefinition.ReadAssembly(new MemoryStream(_fileSystem.File.ReadAllBytes(fileInfo.FullName)));
            var dependencyIs32Bit = Required32BitSet(tempAssembly);
            var bitnessMatches = (sourceRequires32Bit && dependencyIs32Bit) || (!sourceRequires32Bit && !dependencyIs32Bit);
            if (checkAssemblyVersionMatch)
            {
                var versionMatch = tempAssembly.Name.Version == reference.Version;
                return nameMatch && versionMatch && bitnessMatches;
            }
            return nameMatch && bitnessMatches;
        }

        private static EquatableEdge<AssemblyVertex> CreateNewEdge(AssemblyNameReference reference, bool exists, bool required32Bit, AssemblyName assemblyName, List<Regex> exclusions)
        {
            return new EquatableEdge<AssemblyVertex>(
                CreateAssemblyVertex(assemblyName.FullName, assemblyName.Name, true, required32Bit, exclusions),
                CreateAssemblyVertex(reference.FullName, reference.Name, exists, required32Bit, exclusions));
        }

        private static AssemblyVertex CreateAssemblyVertex(string assemblyFullName, string assemblyName, bool exists, bool required32Bit, IEnumerable<Regex> exclusions)
        {
            return new AssemblyVertex
                {
                    AssemblyName = new AssemblyName(assemblyFullName),
                    Exists = exists,
                    Excluded = exclusions.Any(e => e.IsMatch(assemblyName.ToLowerInvariant())),
                    Required32Bit = required32Bit
                };
        }

        public static void OutputToDgml(string output, IVertexAndEdgeListGraph<AssemblyVertex, EquatableEdge<AssemblyVertex>> graph)
        {
            graph.ToDirectedGraphML(graph.GetVertexIdentity(), graph.GetEdgeIdentity(), (n, d) =>
                {
                    d.Label = n.AssemblyName.Name + " " + n.AssemblyName.Version;
                    if (!n.Exists)
                        d.Background = "Red";
                    if (!n.Exists && n.Excluded)
                        d.Background = "Yellow";
                }, (e, l) => l.Label = "").WriteXml(output);
        }
    }
}