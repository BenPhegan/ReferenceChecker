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
                    {@"c:\blah.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"System", "4.0.0.0"}}))}
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
                    {@"c:\blah.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"System", "2.0.0.0"}}))}
                });

            var gacResolver = Substitute.For<IGacResolver>();
            string output;
            gacResolver.AssemblyExists(Arg.Any<String>(), out output).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);

            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(4, graph.Vertices.Count());
        }

        [Test]
        public void CanDetectIncorrectVersion()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string> {{"Blah", "1.0"}, {"System", "4.0.0.0"}}))},
                    {@"c:\blah.dll", new MockFileData(CreateAssembly(version: "1.1", dependencies: new Dictionary<string, string> {{"System", "2.0.0.0"}}))}
                });

            var gacResolver = Substitute.For<IGacResolver>();
            string output;
            gacResolver.AssemblyExists(Arg.Any<String>(), out output).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);

            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(4, graph.Vertices.Count());
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
                    {@"c:\test2.dll", new MockFileData(CreateAssembly(name: "Test2",dependencies: new Dictionary<string, string> {{"Blah2", "1.0"}, {"Other", "4.0.0.0"}}))},
                });

            var gacResolver = Substitute.For<IGacResolver>();
            gacResolver.AssemblyExists(Arg.Any<String>()).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);
            var ignore = new List<Regex> { new Wildcard("test2") };
            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), ignore, new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(3, graph.Vertices.Count());
        }
        
        private static byte[] CreateAssembly(string name = "Test", string moduleName = "Test", string version = "1.0", Dictionary<string, string> dependencies = null)
        {
            var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(name, new Version(version)), moduleName, ModuleKind.Dll);
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
