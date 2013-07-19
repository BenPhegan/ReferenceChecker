using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;
using NSubstitute;
using NUnit.Framework;
using ReferenceChecker.Gac;

namespace ReferenceChecker.Tests
{
    [TestFixture]
    public class AssemblyReferenceGrapherTests
    {
        [Test]
        public void NodesCreatedForAssemblyFilesAndManifestDependencies()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string>{{"Blah","1.0"},{"System","4.0.0.0"}}))}
                });

            var gacResolver = Substitute.For<IGacResolver>();
            string output;
            gacResolver.AssemblyExists(Arg.Any<String>(),out output).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);

            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(3,graph.Vertices.Count());
        }

        [Test]
        public void IgnoreFileNameCaseWhenMatchingManifestDependencies()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"Blah", "1.0"}, {"System", "4.0.0.0"}}))},
                    {@"c:\blah.dll", new MockFileData(CreateAssembly("Blah",dependencies: new Dictionary<string, string> {{"System", "4.0.0.0"}}))}
                });

            var gacResolver = Substitute.For<IGacResolver>();
            string output;
            gacResolver.AssemblyExists(Arg.Any<String>(), out output).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);

            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(3, graph.Vertices.Count());
        }

        [Test]
        public void DifferentManifestVersionsAreDifferentNodes()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"Blah", "1.0"}, {"System", "4.0.0.0"}}))},
                    {@"c:\blah.dll", new MockFileData(CreateAssembly("Blah",dependencies: new Dictionary<string, string> {{"System", "2.0.0.0"}}))}
                });

            var gacResolver = Substitute.For<IGacResolver>();
            string output;
            gacResolver.AssemblyExists(Arg.Any<String>(), out output).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);

            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(4, graph.Vertices.Count());
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void CanDetectIncorrectVersion(bool checkAssemblyVersion, bool assemblyExists)
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly("Test", dependencies: new Dictionary<string, string> {{"Blah", "1.0.0.0"}, {"System", "4.0.0.0"}}))},
                    {@"c:\blah.dll", new MockFileData(CreateAssembly("Blah", version: "1.1.0.0", dependencies: new Dictionary<string, string> {{"System", "2.0.0.0"}}))}
                });

            var gacResolver = Substitute.For<IGacResolver>();
            string output;
            gacResolver.AssemblyExists(Arg.Any<String>(), out output).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);

            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false, checkAssemblyVersion);
            Assert.AreEqual(5, graph.Vertices.Count());
            var assembly = graph.Vertices.First(v => v.AssemblyName.Name.Equals("Blah") && v.AssemblyName.Version.ToString().Equals("1.0.0.0"));
            Assert.AreEqual(assemblyExists, assembly.Exists);
        }
        
        [Test]
        public void MissingFileResultsInNodeWithExistEqualsFalse()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"Blah", "1.0"}, {"System", "4.0.0.0"}}))},
                });

            var gacResolver = Substitute.For<IGacResolver>();
            gacResolver.AssemblyExists("System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null").Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);

            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(3, graph.Vertices.Count());
            Assert.AreEqual(false, graph.Vertices.FirstOrDefault(v => v.AssemblyName.Name.Equals("Blah", StringComparison.OrdinalIgnoreCase)).Exists);
            Assert.AreEqual(1, graph.Vertices.Count(v => v.Exists == false));
        }

        [Test]
        public void CanExcludeAssemblyReferenceFromGraph()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"Blah", "1.0"}, {"System", "4.0.0.0"}}))},
                });

            var gacResolver = Substitute.For<IGacResolver>();
            gacResolver.AssemblyExists(Arg.Any<String>()).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);
            var ignore = new List<Regex>{new Wildcard("system")};
            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(),ignore, new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(2, graph.Vertices.Count());
        }

        [Test]
        public void CanExcludeFileAndAllReferencesFromGraph()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"Blah", "1.0"}, {"System", "4.0.0.0"}}))},
                    {@"c:\test2.dll", new MockFileData(CreateAssembly("Test2",dependencies: new Dictionary<string, string> {{"Blah2", "1.0"}, {"Other", "4.0.0.0"}}))},
                });

            var gacResolver = Substitute.For<IGacResolver>();
            gacResolver.AssemblyExists(Arg.Any<String>()).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);
            var ignore = new List<Regex> { new Wildcard("test2") };
            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), ignore, new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(3, graph.Vertices.Count());
        }

        [TestCase(false, false, 2, 0)]
        [TestCase(false, true, 3, 1)]
        [TestCase(true, false, 3, 1)]
        [TestCase(true, true, 2, 0)]
        public void CanDetectIncorrectCorFlags(bool sourceIs64Bit, bool dependencyIs64Bit, int vertexCount, int missingCount)
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"Blah", "1.0"}}, sixtyfourbit: sourceIs64Bit))},
                    {@"c:\blah.dll", new MockFileData(CreateAssembly("Blah",sixtyfourbit: dependencyIs64Bit))},
                });

            var gacResolver = Substitute.For<IGacResolver>();
            gacResolver.AssemblyExists(Arg.Any<String>()).Returns(false);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);
            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(vertexCount, graph.Vertices.Count());
            var missing = graph.Vertices.Where(v => !v.Exists);
            Assert.AreEqual(missingCount, missing.Count());
        }
        
        private static byte[] CreateAssembly(string name = "Test", string moduleName = "Test", string version = "1.0", Dictionary<string, string> dependencies = null, bool sixtyfourbit = false)
        {
            var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name, new Version(version)), moduleName, ModuleKind.Dll);
            assembly.MainModule.Attributes = sixtyfourbit ? ModuleAttributes.ILOnly : ModuleAttributes.ILOnly | ModuleAttributes.Required32Bit;
            if (dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    assembly.MainModule.AssemblyReferences.Add(new AssemblyNameReference(dependency.Key, new Version(dependency.Value)));
                }
            }
            byte[] assemblyByteArray;
            using (var stream = new MemoryStream())
            {
                assembly.Write(stream);
                assemblyByteArray = stream.ToArray();
            }
            return assemblyByteArray;
        }
    }
}
