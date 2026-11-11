using System;

namespace HuntingGame.Tools
{
    public enum MethodButtonMode { Both, EditMode, PlayMode }

    /// <summary>Expose a parameterless instance void method in the default Inspector.</summary>
    [AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class MethodButtonAttribute : Attribute
    {
        public string Label { get; }
        public MethodButtonMode Mode { get; }
        public MethodButtonAttribute(string label = null, MethodButtonMode mode = MethodButtonMode.Both)
        { Label = label; Mode = mode; }
    }
}