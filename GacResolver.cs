using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace ReferenceChecker
{
    /// <summary>
    /// http://trikks.wordpress.com/2011/07/13/programmatically-check-if-an-assembly-is-loaded-in-gac-with-c/
    /// </summary>
    public class GacResolver
    {
        private static readonly IAssemblyCache AssemblyCache;
        static GacResolver()
        {

            // Get IAssemblyCache pointer
            var hr = GacApi.CreateAssemblyCache(out AssemblyCache, 0);
            if (hr != IntPtr.Zero)
            {
                Marshal.ThrowExceptionForHR(hr.ToInt32());
            }
        }

        public static bool AssemblyExists(string assemblyname, out string response)
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

        private static String QueryAssemblyInfo(string assemblyName)
        {
            var assemblyNames = GetAllAssemblyNames(assemblyName);
            var assemblyPath = string.Empty;
            foreach (var assembly in assemblyNames)
            {
                assemblyPath = QueryAssemblyInfoInternal(assembly);
                if (!String.IsNullOrEmpty(assemblyPath))
                    return assemblyPath;
            }

            return assemblyPath;
        }

        // If assemblyName is not fully qualified, a random matching may be 
        private static String QueryAssemblyInfoInternal(string assemblyName)
        {
            var assembyInfo = new AssemblyInfo { cchBuf = 512 };
            assembyInfo.currentAssemblyPath = new String('\0', assembyInfo.cchBuf);

       
            // Get IAssemblyCache pointer
                try
                {
                    var hr = AssemblyCache.QueryAssemblyInfo(0, assemblyName, ref assembyInfo);
                    if (hr != IntPtr.Zero)
                    {
                        Marshal.ThrowExceptionForHR(hr.ToInt32());
                    }
                }
                catch (FileLoadException e)
                {
                    return string.Empty;
                }
            
            return assembyInfo.currentAssemblyPath;
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

    internal class GacApi
    {
        [DllImport("fusion.dll")]
        internal static extern IntPtr CreateAssemblyCache(
            out IAssemblyCache ppAsmCache, int reserved);
    }

    // GAC Interfaces - IAssemblyCache. As a sample, non used vtable entries 
    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("e707dcde-d1cd-11d2-bab9-00c04f8eceae")]
    internal interface IAssemblyCache
    {
        int Dummy1();
        [PreserveSig]
        IntPtr QueryAssemblyInfo(
            int flags,
            [MarshalAs(UnmanagedType.LPWStr)]
                String assemblyName,
            ref AssemblyInfo assemblyInfo);

        int Dummy2();
        int Dummy3();
        int Dummy4();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct AssemblyInfo
    {
        public int cbAssemblyInfo;
        public int assemblyFlags;
        public long assemblySizeInKB;

        [MarshalAs(UnmanagedType.LPWStr)]
        public String currentAssemblyPath;

        public int cchBuf;
    }
}