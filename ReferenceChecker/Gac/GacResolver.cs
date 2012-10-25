using System;
using System.Collections.Generic;
using System.GACManagedAccess;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ReferenceChecker.Gac
{
    public interface IGacResolver
    {
        bool AssemblyExists(string assemblyname, out string response);
        bool AssemblyExists(string assemblyname);
    }

    public class GacResolver : IGacResolver
    {
        public bool AssemblyExists(string assemblyname, out string response)
        {
            try
            {
                response = QueryAssemblyInfo(assemblyname);
                return !string.IsNullOrEmpty(response);
            }
            catch (FileNotFoundException e)
            {
                response = e.Message;
                return false;
            }
        }

        public bool AssemblyExists(string assemblyname)
        {
            string output;
            return AssemblyExists(assemblyname, out output);
        }

        private static String QueryAssemblyInfo(string assemblyName)
        {
            var assemblyNames = GetAllAssemblyNames(assemblyName);
            var assemblyPath = string.Empty;
            foreach (var assembly in assemblyNames)
            {
                try
                {
                    assemblyPath = AssemblyCache.QueryAssemblyInfo(assembly);
                }
                catch (Exception) { }
                //assemblyPath = QueryAssemblyInfoInternal(assembly);
                if (!String.IsNullOrEmpty(assemblyPath))
                    return assemblyPath;
            }

            return assemblyPath;
        }

        private static IEnumerable<string> GetAllAssemblyNames(string assemblyName)
        {
            var assemblyNameObject = new AssemblyName(assemblyName);
            var full = assemblyNameObject.FullName;
            assemblyNameObject.ProcessorArchitecture = ProcessorArchitecture.None;
            var noProc = assemblyNameObject.FullName;
            assemblyNameObject.SetPublicKeyToken(null);
            var noPub = assemblyNameObject.FullName;
            var justVersion = string.Format("{0}, Version={1}", assemblyNameObject.Name, assemblyNameObject.Version);
            var list = new List<String> { assemblyName, full, noProc, noPub, justVersion, assemblyNameObject.Name };
            return list.Distinct().ToList();

        }
    }

}