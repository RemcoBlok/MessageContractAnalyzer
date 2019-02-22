using System;
using System.Collections.Generic;
using System.Text;

namespace Messages
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class ActivatorAttribute : Attribute
    {
    }
}
