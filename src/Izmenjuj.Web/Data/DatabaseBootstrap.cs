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
                return;
            }

            command.CommandText = """
                IF OBJECT_ID(N'[dbo].[TradeMessages]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[TradeMessages] (
                        [Id] uniqueidentifier NOT NULL,
                        [TradeId] uniqueidentifier NOT NULL,
                        [SenderUserId] nvarchar(450) NOT NULL,
                        [Body] nvarchar(2000) NOT NULL,
                        [SentUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_TradeMessages] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_TradeMessages_Trades_TradeId] FOREIGN KEY ([TradeId]) REFERENCES [dbo].[Trades] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_TradeMessages_TradeId_SentUtc] ON [dbo].[TradeMessages] ([TradeId], [SentUtc]);
                END;

                IF OBJECT_ID(N'[dbo].[TradeEvents]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[TradeEvents] (
                        [Id] uniqueidentifier NOT NULL,
                        [TradeId] uniqueidentifier NOT NULL,
                        [ActorUserId] nvarchar(450) NOT NULL,
                        [Type] int NOT NULL,
                        [Note] nvarchar(500) NULL,
                        [CreatedUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_TradeEvents] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_TradeEvents_Trades_TradeId] FOREIGN KEY ([TradeId]) REFERENCES [dbo].[Trades] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_TradeEvents_TradeId_CreatedUtc] ON [dbo].[TradeEvents] ([TradeId], [CreatedUtc]);
                END;

                IF OBJECT_ID(N'[dbo].[TradeRatings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[TradeRatings] (
                        [Id] uniqueidentifier NOT NULL,
                        [TradeId] uniqueidentifier NOT NULL,
                        [FromUserId] nvarchar(450) NOT NULL,
                        [ToUserId] nvarchar(450) NOT NULL,
                        [Stars] int NOT NULL,
                        [Comment] nvarchar(1000) NULL,
                        [CreatedUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_TradeRatings] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_TradeRatings_Trades_TradeId] FOREIGN KEY ([TradeId]) REFERENCES [dbo].[Trades] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [CK_TradeRatings_Stars] CHECK ([Stars] >= 1 AND [Stars] <= 5)
                    );
                    CREATE UNIQUE INDEX [IX_TradeRatings_TradeId_FromUserId] ON [dbo].[TradeRatings] ([TradeId], [FromUserId]);
                END;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trades_ProposerUserId_Status_CreatedUtc' AND object_id = OBJECT_ID(N'[dbo].[Trades]'))
                    CREATE INDEX [IX_Trades_ProposerUserId_Status_CreatedUtc] ON [dbo].[Trades] ([ProposerUserId], [Status], [CreatedUtc]);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Trades_RecipientUserId_Status_CreatedUtc' AND object_id = OBJECT_ID(N'[dbo].[Trades]'))
                    CREATE INDEX [IX_Trades_RecipientUserId_Status_CreatedUtc] ON [dbo].[Trades] ([RecipientUserId], [Status], [CreatedUtc]);
                """;

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }
}
