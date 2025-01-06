using Octockup.Server.Providers.Storage;

namespace Octockup.Server.Extensions
{
    public static class StorageProviderExtensions
    {
        public static string GetClassName(this IStorageProvider storageProvider)
        {
            return storageProvider.GetType().Name;
        }

        public static IEnumerable<string> GetParametersKeys(this IStorageProvider storageProvider)
        {
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
