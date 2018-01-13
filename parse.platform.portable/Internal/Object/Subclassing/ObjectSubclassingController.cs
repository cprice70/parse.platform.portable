using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using Parse.Internal.Utilities;
using Parse.Public;

namespace Parse.Internal.Object.Subclassing
{
    internal class ObjectSubclassingController : IObjectSubclassingController
    {
        // Class names starting with _ are documented to be reserved. Use this one
        // here to allow us to 'inherit' certain properties.
        private const string ParseObjectClassName = "_ParseObject";

        private readonly ReaderWriterLockSlim _mutex;
        private readonly IDictionary<string, ObjectSubclassInfo> _registeredSubclasses;
        private readonly Dictionary<string, Action> _registerActions;

        public ObjectSubclassingController()
        {
            _mutex = new ReaderWriterLockSlim();
            _registeredSubclasses = new Dictionary<string, ObjectSubclassInfo>();
            _registerActions = new Dictionary<string, Action>();

            // Register the ParseObject subclass, so we get access to the ACL,
            // objectId, and other ParseFieldName properties.
            RegisterSubclass(typeof(ParseObject));
        }

        public string GetClassName(Type type)
        {
            return type == typeof(ParseObject)
                ? ParseObjectClassName
                : ObjectSubclassInfo.GetClassName(type.GetTypeInfo());
        }

        public Type GetType(string className)
        {
            _mutex.EnterReadLock();
            _registeredSubclasses.TryGetValue(className, out var info);
            _mutex.ExitReadLock();

            return info?.TypeInfo.AsType();
        }

        public bool IsTypeValid(string className, Type type)
        {
            _mutex.EnterReadLock();
            _registeredSubclasses.TryGetValue(className, out var subclassInfo);
            _mutex.ExitReadLock();

            return subclassInfo == null
                ? type == typeof(ParseObject)
                : subclassInfo.TypeInfo == type.GetTypeInfo();
        }

        public void RegisterSubclass(Type type)
        {
            var typeInfo = type.GetTypeInfo();
            if (!typeof(ParseObject).GetTypeInfo().IsAssignableFrom(typeInfo))
            {
                throw new ArgumentException("Cannot register a type that is not a subclass of ParseObject");
            }

            var className = GetClassName(type);

            try
            {
                // Perform this as a single independent transaction, so we can never get into an
                // intermediate state where we *theoretically* register the wrong class due to a
                // TOCTTOU bug.
                _mutex.EnterWriteLock();

                if (_registeredSubclasses.TryGetValue(className, out var previousInfo))
                {
                    if (typeInfo.IsAssignableFrom(previousInfo.TypeInfo))
                    {
                        // Previous subclass is more specific or equal to the current type, do nothing.
                        return;
                    }
                    else if (previousInfo.TypeInfo.IsAssignableFrom(typeInfo))
                    {
                        // Previous subclass is parent of new child, fallthrough and actually register
                        // this class.
                        /* Do nothing */
                    }
                    else
                    {
                        throw new ArgumentException(
                            "Tried to register both " + previousInfo.TypeInfo.FullName + " and " + typeInfo.FullName +
                            " as the ParseObject subclass of " + className + ". Cannot determine the right class " +
                            "to use because neither inherits from the other."
                        );
                    }
                }

                ConstructorInfo constructor = type.FindConstructor();
                if (constructor == null)
                {
                    throw new ArgumentException(
                        "Cannot register a type that does not implement the default constructor!");
                }

                _registeredSubclasses[className] = new ObjectSubclassInfo(type, constructor);
            }
            finally
            {
                _mutex.ExitWriteLock();
            }

            _mutex.EnterReadLock();
            _registerActions.TryGetValue(className, out var toPerform);
            _mutex.ExitReadLock();

            toPerform?.Invoke();
        }

        public void UnregisterSubclass(Type type)
        {
            _mutex.EnterWriteLock();
            _registeredSubclasses.Remove(GetClassName(type));
            _mutex.ExitWriteLock();
        }

        public void AddRegisterHook(Type t, Action action)
        {
            _mutex.EnterWriteLock();
            _registerActions.Add(GetClassName(t), action);
            _mutex.ExitWriteLock();
        }

        public ParseObject Instantiate(string className)
        {
            _mutex.EnterReadLock();
            _registeredSubclasses.TryGetValue(className, out var info);
            _mutex.ExitReadLock();

            return info != null
                ? info.Instantiate()
                : new ParseObject(className);
        }

        public IDictionary<string, string> GetPropertyMappings(string className)
        {
            _mutex.EnterReadLock();
            _registeredSubclasses.TryGetValue(className, out var info);
            if (info == null)
            {
                _registeredSubclasses.TryGetValue(ParseObjectClassName, out info);
            }

            _mutex.ExitReadLock();

            return info.PropertyMappings;
        }
    }
}