using Octockup.Server.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Octockup.Server.Database
{
    public class SqliteDbContext(DbContextOptions options) : AppDbContext(options)
    {

    }
}