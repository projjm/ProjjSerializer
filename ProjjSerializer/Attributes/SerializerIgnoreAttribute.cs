using System;
using System.Collections.Generic;
using System.Text;

namespace ProjjSerializer.Attributes
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field | AttributeTargets.Interface, Inherited = true, AllowMultiple = false)]
    public class SerializerIgnoreAttribute : Attribute
    {
        // Implementation not needed, notifies type cacher only
    }
}
