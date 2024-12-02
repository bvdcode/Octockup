namespace Octockup.Server.Resources
{
    public static class AppResources
    {
        public static byte[] Favicon => favicon ??= File.ReadAllBytes("Resources/favicon.ico");
        private static byte[]? favicon;
    }
}
