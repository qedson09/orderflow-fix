using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderFlow.Application.Orders;
using OrderFlow.Infrastructure.Persistence;
using OrderFlow.Infrastructure.Persistence.Auditing;

namespace OrderFlow.Infrastructure.Messaging;

public sealed class AuditInbox(GeneratorDb db, ILogger<AuditInbox> logger)
{
    public const string ConsumerName = "order-generator-audit-v1";

    public async Task AcceptAsync(string payload, CancellationToken ct)
    {
        OrderDecisionEvent message;

        try
        {
            message = JsonSerializer.Deserialize<OrderDecisionEvent>(payload, OrderDecisionEvent.Json) ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new PoisonMessageException("JSON inválido");
        }

        if (message.SchemaVersion != 1 || message.EventType != "OrderDecisionRecorded" || message.EventId == Guid.Empty ||
            !Guid.TryParseExact(message.ClOrdId, "D", out _) || string.IsNullOrWhiteSpace(message.Symbol) ||
            message.Decision is not ("Accepted" or "Rejected") || message.OccurredAtUtc.Kind != DateTimeKind.Utc ||
            message.OrderId == Guid.Empty || string.IsNullOrWhiteSpace(message.ExecId))
            throw new PoisonMessageException("Schema ou campos de evento inválidos");

        // Preserve v1 hashes for legacy events produced before accountId was added.
        var node = JsonSerializer.SerializeToNode(message, OrderDecisionEvent.Json)!.AsObject();

        using var original = JsonDocument.Parse(payload);

        if (!original.RootElement.TryGetProperty("accountId", out _)) node.Remove("accountId");

        var canonical = node.ToJsonString(OrderDecisionEvent.Json);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var key = ConsumerName + "|" + message.EventId;

        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtextextended({key}, 0))", ct);

        var previous = await db.Inbox.FindAsync([ConsumerName, message.EventId], ct);

        if (previous is not null)
        {
            if (previous.PayloadHash != hash) throw new PoisonMessageException("EventId reutilizado com payload diferente");
            await tx.CommitAsync(ct); return;
        }

        db.Inbox.Add(new InboxMessage { Consumer = ConsumerName, EventId = message.EventId, PayloadHash = hash });

        db.Events.Add(new OrderEventProjection
        {
            EventId = message.EventId,
            ClOrdId = message.ClOrdId,
            Symbol = message.Symbol,
            SymbolSequence = message.SymbolSequence,
            OccurredAt = message.OccurredAtUtc,
            Type = message.EventType,
            Description = message.Decision == "Accepted" ? "Aceite registrado na auditoria." : "Rejeição registrada na auditoria: " + message.Reason
        });

        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);

        logger.LogInformation("Auditoria persistida {EventId} {ClOrdId}", message.EventId, message.ClOrdId);
    }
    public async Task QuarantineAsync(string topic, int partition, long offset, string payload, string error, CancellationToken ct)
    {
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO generator."QuarantinedMessages" ("Topic", "Partition", "Offset", "Payload", "Error", "CreatedAt")
            VALUES ({topic}, {partition}, {offset}, {payload}, {error}, {DateTime.UtcNow})
            ON CONFLICT ("Topic", "Partition", "Offset") DO NOTHING
            """, ct);

        logger.LogError("Mensagem em quarentena {Topic}/{Partition}/{Offset}: {Error}", topic, partition, offset, error);
    }
}
