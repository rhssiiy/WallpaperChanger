using System;

namespace Configurator
{
    [Flags]
    public enum Purity
    {
        None = 0,
        Clean = 1,
        Sketchy = 2
    }
}