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
                IF COL_LENGTH(N'dbo.Collections', N'ShowItemImages') IS NULL
                BEGIN
                    ALTER TABLE [dbo].[Collections]
                    ADD [ShowItemImages] bit NOT NULL CONSTRAINT [DF_Collections_ShowItemImages] DEFAULT (0);
                END;

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

                IF OBJECT_ID(N'[dbo].[UserBlocks]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[UserBlocks] (
                        [Id] uniqueidentifier NOT NULL,
                        [BlockerUserId] nvarchar(450) NOT NULL,
                        [BlockedUserId] nvarchar(450) NOT NULL,
                        [CreatedUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_UserBlocks] PRIMARY KEY ([Id])
                    );
                    CREATE UNIQUE INDEX [IX_UserBlocks_BlockerUserId_BlockedUserId] ON [dbo].[UserBlocks] ([BlockerUserId], [BlockedUserId]);
                    CREATE INDEX [IX_UserBlocks_BlockedUserId] ON [dbo].[UserBlocks] ([BlockedUserId]);
                END;

                IF OBJECT_ID(N'[dbo].[UserReports]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[UserReports] (
                        [Id] uniqueidentifier NOT NULL,
                        [ReporterUserId] nvarchar(450) NOT NULL,
                        [TargetUserId] nvarchar(450) NOT NULL,
                        [TradeId] uniqueidentifier NULL,
                        [Reason] nvarchar(120) NOT NULL,
                        [Details] nvarchar(2000) NULL,
                        [Status] int NOT NULL,
                        [CreatedUtc] datetime2 NOT NULL,
                        [ReviewedByUserId] nvarchar(450) NULL,
                        [ReviewedUtc] datetime2 NULL,
                        [ResolutionNote] nvarchar(1000) NULL,
                        CONSTRAINT [PK_UserReports] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_UserReports_Status_CreatedUtc] ON [dbo].[UserReports] ([Status], [CreatedUtc]);
                    CREATE INDEX [IX_UserReports_TargetUserId] ON [dbo].[UserReports] ([TargetUserId]);
                END;

                IF OBJECT_ID(N'[dbo].[UserModerations]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[UserModerations] (
                        [UserId] nvarchar(450) NOT NULL,
                        [IsSuspended] bit NOT NULL,
                        [Note] nvarchar(1000) NULL,
                        [UpdatedUtc] datetime2 NOT NULL,
                        [UpdatedByUserId] nvarchar(450) NULL,
                        CONSTRAINT [PK_UserModerations] PRIMARY KEY ([UserId])
                    );
                    CREATE INDEX [IX_UserModerations_IsSuspended] ON [dbo].[UserModerations] ([IsSuspended]);
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
