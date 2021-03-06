﻿using System;

namespace Ruminoid.Toolbox.Composition.Services
{
    [Serializable]
    public class PluginCompositionException : Exception
    {
        public PluginCompositionException()
        {
        }

        public PluginCompositionException(string message) : base(message)
        {
        }

        public PluginCompositionException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}
