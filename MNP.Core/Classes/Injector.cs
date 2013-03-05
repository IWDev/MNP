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
        private readonly ILogProvider LogProvider;
        private readonly Dictionary<Type, Func<object>> dependancies = new Dictionary<Type, Func<object>>();

        public Injector(ILogProvider logProvider)
        {
            this.LogProvider = logProvider;
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

    }
}