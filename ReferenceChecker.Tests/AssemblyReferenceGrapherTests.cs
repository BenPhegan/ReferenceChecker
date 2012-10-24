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
            var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Test", new Version("1.0")), "Test", ModuleKind.Dll);
            assembly.MainModule.AssemblyReferences.Add(new AssemblyNameReference("Blah", new Version("1.0")));
            var stream = new MemoryStream();
            assembly.Write(stream);
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
                {
                    {@"c:\test.dll", new MockFileData(stream.ToArray())}
                });

            var gacResolver = Substitute.For<IGacResolver>();
            string output;
            gacResolver.AssemblyExists(Arg.Any<String>(),out output).Returns(true);
            var grapher = new AssemblyReferenceGrapher(fileSystem, gacResolver);
            var graph = grapher.GenerateAssemblyReferenceGraph(new List<Regex>(), new ConcurrentBag<string> {@"c:\test.dll"}, false);
            Assert.AreEqual(2,graph.Vertices.Count());
        }
    }
}
