namespace Link2Stream.Server.Resources
{
    public static class AppResources
    {
        public static byte[] Favicon => favicon ??= File.ReadAllBytes("Resources/favicon.ico");
        private static byte[]? favicon;
    }
}
