using System;

namespace KitSymes.GTRP
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SyncVarAttribute : Attribute
    {
        public string OnChangedFunction { get; set; }
        public SyncVarAttribute(string onChangedFunction = "")
        {
            OnChangedFunction = onChangedFunction;
        }
    }
}
