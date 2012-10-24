using System;
using System.Reflection;

namespace ReferenceChecker
{
    public class AssemblyVertex 
    {
        protected bool Equals(AssemblyVertex other)
        {
            return Equals(AssemblyName.FullName, other.AssemblyName.FullName) && Exists.Equals(other.Exists) && Excluded.Equals(other.Excluded);
        }

        public override int GetHashCode()
        {
            var hashCode = 0;
            if (AssemblyName != null)
            {
                hashCode = hashCode ^ AssemblyName.FullName.GetHashCode();
            }

            return hashCode ^ Exists.GetHashCode() ^ Excluded.GetHashCode();
        }

        public AssemblyName AssemblyName;
        public Boolean Exists;
        public Boolean Excluded;

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AssemblyVertex) obj);
        }
    }
}