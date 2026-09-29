using System.Data;
using GeopoliticalMonitor.Application.Alerts;
using GeopoliticalMonitor.Domain.Alerts;
using GeopoliticalMonitor.Domain.Watchlists;
using GeopoliticalMonitor.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeopoliticalMonitor.Infrastructure.Persistence;

public sealed class SqliteAlertStore(Func<GeopoliticalMonitorDbContext> createDbContext) : IAlertStore
{
    public async Task<IReadOnlyList<AlertRuleDto>> GetRulesAsync(
        int maximumRules,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        return await dbContext.AlertRules
            .AsNoTracking()
            .OrderBy(item => item.WatchlistId)
            .ThenBy(item => item.TriggerType)
            .Take(maximumRules)
            .Select(item => new AlertRuleDto(
                item.Id,
                item.WatchlistId,
                item.TriggerType,
                item.IsEnabled,
                item.CreatedAtUtc,
                item.LastEnabledAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<SetAlertRuleResult> SetRuleEnabledAsync(
        WatchlistId watchlistId,
        AlertTriggerType triggerType,
        bool enabled,
        AlertRuleId newRuleId,
        DateTimeOffset transitionAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database
            .BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
            .ConfigureAwait(false);
        if (!await dbContext.Watchlists.AnyAsync(item => item.Id == watchlistId.Value, cancellationToken).ConfigureAwait(false))
        {
            return new SetAlertRuleResult(SetAlertRuleDisposition.WatchlistNotFound);
        }

        var triggerName = triggerType.ToString();
        var entity = await dbContext.AlertRules.SingleOrDefaultAsync(
            item => item.WatchlistId == watchlistId.Value && item.TriggerType == triggerName,
            cancellationToken).ConfigureAwait(false);

        if (entity is null)
        {
            if (!enabled)
            {
                return new SetAlertRuleResult(SetAlertRuleDisposition.RuleNotFound);
            }

            var rule = AlertRule.CreateEnabled(newRuleId, watchlistId, triggerType, transitionAtUtc);
            entity = new AlertRuleEntity
            {
                Id = rule.Id.Value,
                WatchlistId = rule.WatchlistId.Value,
                TriggerType = rule.TriggerType.ToString(),
                IsEnabled = rule.IsEnabled,
                CreatedAtUtc = rule.CreatedAtUtc,
                LastEnabledAtUtc = rule.LastEnabledAtUtc,
            };
            dbContext.AlertRules.Add(entity);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new SetAlertRuleResult(SetAlertRuleDisposition.CreatedEnabled, ToDto(entity));
        }

        if (entity.IsEnabled == enabled)
        {
            return new SetAlertRuleResult(SetAlertRuleDisposition.Unchanged, ToDto(entity));
        }

        entity.IsEnabled = enabled;
        if (enabled)
        {
            entity.LastEnabledAtUtc = transitionAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return new SetAlertRuleResult(
            enabled ? SetAlertRuleDisposition.Enabled : SetAlertRuleDisposition.Disabled,
            ToDto(entity));
    }

    public async Task<long> GetUnreadCountAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        return await dbContext.AlertOccurrences
            .LongCountAsync(item => item.ReadAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<AlertListResult> GetAlertsAsync(
        int maximumAlerts,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var unreadCount = await dbContext.AlertOccurrences
            .LongCountAsync(item => item.ReadAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
        var items = await dbContext.AlertOccurrences
            .FromSqlInterpolated($"SELECT * FROM AlertOccurrences ORDER BY TriggeredAtUtc DESC, Id DESC LIMIT {maximumAlerts + 1}")
            .AsNoTracking()
            .Select(item => new AlertOccurrenceDto(
                item.Id,
                item.AlertRuleId,
                item.EventId,
                item.AlertRule!.WatchlistId,
                item.Event!.Title,
                item.AlertRule.Watchlist!.Name,
                item.AlertRule.TriggerType,
                item.ProviderRevisionAtUtc,
                item.TriggeredAtUtc,
                item.ReadAtUtc))
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);
        return new AlertListResult(items.Take(maximumAlerts).ToArray(), unreadCount, items.Length > maximumAlerts);
    }

    public async Task<MarkAlertReadResult> MarkReadAsync(
        AlertId alertId,
        DateTimeOffset readAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var entity = await dbContext.AlertOccurrences
            .SingleOrDefaultAsync(item => item.Id == alertId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (entity is null)
        {
            return new MarkAlertReadResult(MarkAlertReadDisposition.NotFound, alertId.Value);
        }

        if (entity.ReadAtUtc is not null)
        {
            return new MarkAlertReadResult(MarkAlertReadDisposition.AlreadyRead, alertId.Value, entity.ReadAtUtc);
        }

        entity.ReadAtUtc = readAtUtc;
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new MarkAlertReadResult(MarkAlertReadDisposition.Updated, alertId.Value, entity.ReadAtUtc);
    }

    private static AlertRuleDto ToDto(AlertRuleEntity entity)
        => new(
            entity.Id,
            entity.WatchlistId,
            entity.TriggerType,
            entity.IsEnabled,
            entity.CreatedAtUtc,
            entity.LastEnabledAtUtc);
}
