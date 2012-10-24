using NUnit.Framework;
using ReferenceChecker.Gac;

namespace ReferenceChecker.Tests
{
    [TestFixture]
    public class GacResolverTests
    {
        [TestCase("System", Result = true, Description = "Can resolve System")]
        [TestCase("Giberishshsidfasdfasdfasdf.asdfasdfas.dasdfasdf", Result = false, Description = "Cant resolve gibberish")]
        [TestCase("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL", Result = true, Description = "Using full name")]
        [TestCase("System.Xml, Version=4.0.0.0, Culture=neutral, PublicKeyToken=null, ProcessorArchitecture=MSIL", Result = true, Description = "With a null PublicKeyToken, we return false rather than throw exception")]
        public bool CanResolveSystem(string assemblyName)
        {
            string test;
            var resolver = new GacResolver();
            return resolver.AssemblyExists(assemblyName, out test);
        }
    }
}