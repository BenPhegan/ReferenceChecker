using System;
using System.Reflection;

namespace ReferenceChecker
{
    public class AssemblyVertex 
    {
        protected bool Equals(AssemblyVertex other)
        {
            return Equals(AssemblyName.FullName, other.AssemblyName.FullName) && Exists.Equals(other.Exists) && Excluded.Equals(other.Excluded) && Required32Bit.Equals(other.Required32Bit);
        }

        public override int GetHashCode()
        {
            var hashCode = 0;
            if (AssemblyName != null)
            {
                hashCode = hashCode ^ AssemblyName.FullName.GetHashCode();
            }

            return hashCode ^ Exists.GetHashCode() ^ Excluded.GetHashCode() ^ Required32Bit.GetHashCode();
        }

        public AssemblyName AssemblyName;
        public Boolean Exists;
        public Boolean Excluded;
        public Boolean Required32Bit; 

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((AssemblyVertex) obj);
        }
    }
}