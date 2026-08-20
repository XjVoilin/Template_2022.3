using System;
using System.Collections.Generic;
using July.Logging;

namespace Game.Aot
{
    /// <summary>Startup-only value registry for the seed project's composition root.</summary>
    public static class SeedServices
    {
        private static readonly Dictionary<Type, object> Services = new();

        public static void Register<T>(T instance)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            Services[typeof(T)] = instance;
        }

        public static T Resolve<T>()
        {
            if (Services.TryGetValue(typeof(T), out var instance))
                return (T)instance;

            throw new InvalidOperationException($"Startup service {typeof(T).Name} is not registered.");
        }

        public static bool TryResolve<T>(out T instance)
        {
            if (Services.TryGetValue(typeof(T), out var value) && value is T typed)
            {
                instance = typed;
                return true;
            }

            instance = default;
            return false;
        }

        public static void Clear()
        {
            var values = new List<object>(Services.Values);
            Services.Clear();

            for (var index = values.Count - 1; index >= 0; index--)
            {
                if (values[index] is not IDisposable disposable) continue;
                try { disposable.Dispose(); }
                catch (Exception exception) { JLogger.LogException(exception); }
            }
        }
    }
}
