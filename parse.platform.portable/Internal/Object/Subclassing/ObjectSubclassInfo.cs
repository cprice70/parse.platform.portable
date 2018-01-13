using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Parse.Internal.Utilities;
using Parse.Public;

namespace Parse.Internal.Object.Subclassing
{
    internal class ObjectSubclassInfo
    {
        public ObjectSubclassInfo(Type type, ConstructorInfo constructor)
        {
            TypeInfo = type.GetTypeInfo();
            ClassName = GetClassName(TypeInfo);
            Constructor = constructor;
            PropertyMappings = ReflectionHelpers.GetProperties(type)
                .Select(prop => Tuple.Create(prop, prop.GetCustomAttribute<ParseFieldNameAttribute>(true)))
                .Where(t => t.Item2 != null)
                .Select(t => Tuple.Create(t.Item1, t.Item2.FieldName))
                .ToDictionary(t => t.Item1.Name, t => t.Item2);
        }

        public TypeInfo TypeInfo { get; }
        private string ClassName { get; }
        public IDictionary<string, string> PropertyMappings { get; }
        private ConstructorInfo Constructor { get; }

        public ParseObject Instantiate()
        {
            return (ParseObject) Constructor.Invoke(null);
        }

        internal static string GetClassName(TypeInfo type)
        {
            var attribute = type.GetCustomAttribute<ParseClassNameAttribute>();
            return attribute?.ClassName;
        }
    }
}