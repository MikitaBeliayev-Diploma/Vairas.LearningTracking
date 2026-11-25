using Npgsql;
using Vairas.LearningTracking.Business;
using Vairas.LearningTracking.Domain;
namespace Vairas.LearningTracking.Persistance;

public sealed class OutboxStore(string connection) : IOutboxStore
{
    public async Task<OutboxMessage?> ClaimAsync(CancellationToken ct)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = """
            WITH candidate AS (SELECT id FROM outbox_message WHERE sent_at IS NULL AND next_attempt_at<=clock_timestamp()
              AND (lease_until IS NULL OR lease_until<clock_timestamp()) ORDER BY occurred_at,id FOR UPDATE SKIP LOCKED LIMIT 1)
            UPDATE outbox_message o SET lease_id=@lease,lease_until=clock_timestamp()+interval '30 seconds',attempts=attempts+1
            FROM candidate c WHERE o.id=c.id
            RETURNING o.id,o.school_id,o.aggregate_id,o.aggregate_version,o.event_type,o.payload,o.correlation_id,o.occurred_at,o.attempts,o.lease_id
            """;
        command.Parameters.AddWithValue("lease", Guid.NewGuid());
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) return null;
        return new OutboxMessage { Id = reader.GetGuid(0), SchoolId = reader.GetGuid(1), AggregateId = reader.GetGuid(2), AggregateVersion = reader.GetInt64(3),
            EventType = reader.GetString(4), Payload = reader.GetString(5), CorrelationId = reader.GetString(6), OccurredAt = reader.GetFieldValue<DateTimeOffset>(7), Attempts = reader.GetInt32(8), LeaseId = reader.GetGuid(9) };
    }
    public async Task FinishAsync(Guid id, Guid lease, bool sent, CancellationToken ct)
    {
        await using var db = new NpgsqlConnection(connection); await db.OpenAsync(ct);
        await using var command = db.CreateCommand();
        command.CommandText = """
            UPDATE outbox_message SET sent_at=CASE WHEN @sent THEN clock_timestamp() ELSE sent_at END,
            next_attempt_at=clock_timestamp()+make_interval(secs=>least(300,power(2,least(attempts,8)))::integer),
            lease_id=NULL,lease_until=NULL WHERE id=@id AND lease_id=@lease
            """;
        command.Parameters.AddWithValue("sent", sent); command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("lease", lease);
        await command.ExecuteNonQueryAsync(ct);
    }
}
