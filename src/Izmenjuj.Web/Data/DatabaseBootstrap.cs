using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using System.Data;

namespace Izmenjuj.Web.Data;

public static class DatabaseBootstrap
{
    public static async Task InitializeAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var command = db.Database.GetDbConnection().CreateCommand();
            command.CommandText = """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'AspNetUsers'
                ) THEN 1 ELSE 0 END;
                """;

            if (command.Connection?.State != ConnectionState.Open)
            {
                await command.Connection!.OpenAsync(cancellationToken);
            }

            var exists = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken) ?? 0) == 1;
            if (!exists)
            {
                var creator = db.GetService<IRelationalDatabaseCreator>();
                await creator.CreateTablesAsync(cancellationToken);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
