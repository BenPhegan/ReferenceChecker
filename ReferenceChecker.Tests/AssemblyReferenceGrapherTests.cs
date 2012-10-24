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
        public void FirstTest()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(CreateAssembly(dependencies: new Dictionary<string, string>{{"Blah","1.0"},{"System","4.0.0.0"}}))}
                });

            var gacResolver = Substitute.For<IGacResolver>();
            string output;
            gacResolver.AssemblyExists(Arg.Any<String>(),out output).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);

            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new ConcurrentBag<string>(fileSystem.AllPaths), false);
            Assert.AreEqual(3,graph.Vertices.Count());
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
