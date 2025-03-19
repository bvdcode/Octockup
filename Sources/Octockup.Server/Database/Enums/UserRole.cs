namespace Octockup.Server.Database.Enums
{
    public enum UserRole
    {
        /// <summary>
        /// Blocked user - can't login and use the system.
        /// </summary>
        Blocked = 0,

        /// <summary>
        /// Regular user - can see backups and read logs.
        /// </summary>
        User = 1,

        /// <summary>
        /// Manager user - can see backups, read logs, create and delete backups.
        /// </summary>
        Manager = 2,

        /// <summary>
        /// Admin user - can do everything.
        /// </summary>
        Admin = 3
    }
}