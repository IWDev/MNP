using System;
using System.Collections.Generic;
using System.Linq;

namespace MNP.Core
{
    /// <summary>
    /// A very basic IoC container.
    /// </summary>
    public sealed class Injector
    {
        #region "Singleton Pattern"
        private static class SingletonHolder
        {
            internal static readonly Injector instance = new Injector();
            // Empty static constructor - forces laziness!
            static SingletonHolder() { }
        }

        public static Injector Instance { get { return SingletonHolder.instance; } }
        #endregion

        public ILogProvider LogProvider { get; set; }
        private readonly Dictionary<Type, Func<object>> dependancies = new Dictionary<Type, Func<object>>();

        private Injector()
        {
        }

        public void Bind<TBase, TDerived>() where TDerived : TBase
        {
            this.dependancies[typeof(TBase)] = () => ResolveByType(typeof(TDerived));
        }

        public void Bind<T>(T instance)
        {
            this.dependancies[typeof(T)] = () => instance;
        }

        public T Resolve<T>()
        {
            this.LogProvider.Log(String.Format("Resolving {0}...", typeof(T).Name), "Injector.Resolve", LogLevel.Verbose);
            return (T)Resolve(typeof(T));
        }

        public object Resolve(Type type)
        {
            Func<object> provider;
            if (this.dependancies.TryGetValue(type, out provider))
            {
                return provider.Invoke();
            }
            return ResolveByType(type);
        }

        private object ResolveByType(Type type)
        {
            // get the constructor
            var constructor = type.GetConstructors().FirstOrDefault();
            if (constructor != null)
            {
                var args = constructor.GetParameters().Select(p => Resolve(p.ParameterType)).ToArray();
                return constructor.Invoke(args);
            }
            var instanceProperty = type.GetProperty("Instance");
            if (instanceProperty != null)
            {
                return instanceProperty.GetValue(null);
            }
            return null;
        }

        public void Clear()
        {
            dependancies.Clear();
        }
    }
}