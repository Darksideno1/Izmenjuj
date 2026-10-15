using Izmenjuj.Web.Data;
using Izmenjuj.Web.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Net;

namespace Izmenjuj.Web.Services;

public sealed class TradeNotificationInterceptor(
    IServiceScopeFactory scopeFactory,
    IAppEmailSender emailSender,
    IConfiguration configuration,
    ILogger<TradeNotificationInterceptor> logger) : SaveChangesInterceptor
{
    private readonly List<PendingTradeNotice> _pending = [];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Capture(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capture(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        await FlushAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        _pending.Clear();
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        _pending.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capture(DbContext? context)
    {
        if (context is null) return;

        foreach (var entry in context.ChangeTracker.Entries<TradeEvent>()
                     .Where(x => x.State == EntityState.Added))
        {
            var entity = entry.Entity;
            _pending.Add(new PendingTradeNotice(entity.TradeId, entity.ActorUserId, entity.Type, false));
        }

        foreach (var entry in context.ChangeTracker.Entries<TradeMessage>()
                     .Where(x => x.State == EntityState.Added))
        {
            var entity = entry.Entity;
            _pending.Add(new PendingTradeNotice(entity.TradeId, entity.SenderUserId, null, true));
        }
    }

    private async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (_pending.Count == 0 || !emailSender.IsConfigured)
        {
            _pending.Clear();
            return;
        }

        var pending = _pending.Distinct().ToList();
        _pending.Clear();

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var tradeIds = pending.Select(x => x.TradeId).Distinct().ToList();
            var trades = await db.Trades.AsNoTracking()
                .Where(x => tradeIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            var userIds = trades.Values
                .SelectMany(x => new[] { x.ProposerUserId, x.RecipientUserId })
                .Distinct()
                .ToList();
            var users = await db.Users.AsNoTracking()
                .Where(x => userIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, cancellationToken);

            foreach (var notice in pending)
            {
                if (!trades.TryGetValue(notice.TradeId, out var trade)) continue;
                var recipientId = trade.ProposerUserId == notice.ActorUserId
                    ? trade.RecipientUserId
                    : trade.ProposerUserId;
                if (!users.TryGetValue(recipientId, out var recipient) || string.IsNullOrWhiteSpace(recipient.Email)) continue;

                var actorName = users.TryGetValue(notice.ActorUserId, out var actor) && !string.IsNullOrWhiteSpace(actor.DisplayName)
                    ? actor.DisplayName
                    : "Drug zbiratelj";
                var subject = notice.IsMessage
                    ? "Novo sporočilo pri menjavi – Izmenjuj.si"
                    : SubjectFor(notice.EventType);
                var action = notice.IsMessage ? "poslal novo zasebno sporočilo" : TextFor(notice.EventType);
                var publicBaseUrl = configuration["App:PublicBaseUrl"]?.TrimEnd('/');
                var link = string.IsNullOrWhiteSpace(publicBaseUrl)
                    ? string.Empty
                    : $"<p><a href=\"{WebUtility.HtmlEncode($"{publicBaseUrl}/Trades/Details/{trade.Id}")}\">Odpri menjavo</a></p>";

                await emailSender.SendAsync(
                    recipient.Email,
                    subject,
                    $"<p><strong>{WebUtility.HtmlEncode(actorName)}</strong> je {WebUtility.HtmlEncode(action)}.</p>{link}<p>To je samodejno obvestilo Izmenjuj.si.</p>",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pošiljanje obvestil o menjavah ni uspelo.");
        }
    }

    private static string SubjectFor(TradeEventType? type) => type switch
    {
        TradeEventType.Proposed => "Nov predlog menjave – Izmenjuj.si",
        TradeEventType.Countered => "Nov protipredlog – Izmenjuj.si",
        TradeEventType.Accepted => "Menjava je sprejeta – Izmenjuj.si",
        TradeEventType.Rejected => "Predlog menjave je zavrnjen – Izmenjuj.si",
        TradeEventType.Cancelled => "Predlog menjave je preklican – Izmenjuj.si",
        TradeEventType.CompletionConfirmed => "Potrditev zaključka menjave – Izmenjuj.si",
        TradeEventType.Completed => "Menjava je zaključena – Izmenjuj.si",
        _ => "Sprememba menjave – Izmenjuj.si"
    };

    private static string TextFor(TradeEventType? type) => type switch
    {
        TradeEventType.Proposed => "poslal nov predlog menjave",
        TradeEventType.Countered => "poslal protipredlog",
        TradeEventType.Accepted => "sprejel predlog menjave",
        TradeEventType.Rejected => "zavrnil predlog menjave",
        TradeEventType.Cancelled => "preklical predlog menjave",
        TradeEventType.CompletionConfirmed => "potrdil zaključek menjave",
        TradeEventType.Completed => "zaključil menjavo",
        _ => "spremenil menjavo"
    };

    private sealed record PendingTradeNotice(Guid TradeId, string ActorUserId, TradeEventType? EventType, bool IsMessage);
}
