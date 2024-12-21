using Octockup.Server.Providers.Storage;

namespace Octockup.Server.Extensions
{
    public static class StorageProviderExtensions
    {
        public static IEnumerable<string> GetParametersKeys(this IStorageProvider storageProvider)
        {
            // storageProvider is IStorageProvider but actually is IStorageProvider<>, get generic type
            var type = storageProvider
                .GetType()
                .GetInterfaces()
                .First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IStorageProvider<>))
                .GetGenericArguments()
                .First();
            return type.GetProperties().Select(p => p.Name);
        }
    }
}
