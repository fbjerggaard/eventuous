// Copyright (C) Ubiquitous AS. All rights reserved
// Licensed under the Apache License, Version 2.0.

using System.Data;
using Eventuous.Subscriptions;
using Eventuous.Subscriptions.Checkpoints;
using Eventuous.Subscriptions.Context;
using Eventuous.Subscriptions.Filters;
using Eventuous.Tools;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace Eventuous.Postgresql.Subscriptions;

public class PostgresStreamSubscription : PostgresSubscriptionBase<PostgresStreamSubscriptionOptions> {
    public PostgresStreamSubscription(
        NpgsqlDataSource                  dataSource,
        PostgresStreamSubscriptionOptions options,
        ICheckpointStore                  checkpointStore,
        ConsumePipe                       consumePipe,
        ILoggerFactory?                   loggerFactory = null
    ) : base(dataSource, options, checkpointStore, consumePipe, loggerFactory)
        => _streamName = options.Stream.ToString();

    protected override NpgsqlCommand PrepareCommand(NpgsqlConnection connection, long start) {
        var cmd = new NpgsqlCommand(Schema.ReadStreamSub, connection);

        cmd.CommandType = CommandType.Text;
        cmd.Parameters.AddWithValue("_stream_id", NpgsqlDbType.Integer, _streamId);
        cmd.Parameters.AddWithValue("_from_position", NpgsqlDbType.Integer, (int) start + 1);
        cmd.Parameters.AddWithValue("_count", NpgsqlDbType.Integer, Options.MaxPageSize);
        return cmd;
    }

    protected override async Task BeforeSubscribe(CancellationToken cancellationToken) {
        await using var connection = await DataSource.OpenConnectionAsync(cancellationToken).NoContext();
        await using var cmd = connection.CreateCommand();
        cmd.CommandType = CommandType.Text;
        cmd.CommandText = Schema.CheckStream;
        cmd.Parameters.AddWithValue("_stream_name", NpgsqlDbType.Varchar, Options.Stream.ToString());
        cmd.Parameters.AddWithValue("_expected_version", NpgsqlDbType.Integer, -2);
        await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).NoContext();
        await reader.ReadAsync(cancellationToken).NoContext();
        _streamId = reader.GetInt32(0);
    }

    protected override long MoveStart(PersistedEvent evt) => evt.StreamPosition;

    ulong           _sequence;
    int             _streamId;
    readonly string _streamName;

    protected override IMessageConsumeContext AsContext(
        PersistedEvent    evt,
        object?           e,
        Metadata?         meta,
        CancellationToken cancellationToken
    )
        => new MessageConsumeContext(
            evt.MessageId.ToString(),
            evt.MessageType,
            ContentType,
            _streamName,
            (ulong) evt.StreamPosition,
            (ulong) evt.GlobalPosition,
            _sequence++,
            evt.Created,
            e,
            meta,
            Options.SubscriptionId,
            cancellationToken
        );

    protected override EventPosition GetPositionFromContext(IMessageConsumeContext context)
        => EventPosition.FromContext(context);
}

public record PostgresStreamSubscriptionOptions(StreamName Stream) : PostgresSubscriptionBaseOptions;