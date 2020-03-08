using System;
using System.Collections.Generic;
using System.Text;

namespace Messages
{
    [AttributeUsage(AttributeTargets.Property)]
    public sealed class ActivatorInitializedAttribute : Attribute
    {
    }
}
