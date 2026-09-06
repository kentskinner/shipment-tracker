module ShipmentTracker.Projector.DeadLetter

open System
open Confluent.Kafka
open Thoth.Json.Net

let topic = "shipment-events-dlq"

/// A dead-letter entry is its own small contract: enough to diagnose the
/// failure (error, timestamp), locate the original (topic/partition/offset),
/// and replay it (raw value, original key).
let private encode (source: ConsumeResult<string, string>) (error: string) =
    Encode.object [
        "failedAt", Encode.datetimeOffset DateTimeOffset.UtcNow
        "error", Encode.string error
        "sourceTopic", Encode.string source.Topic
        "sourcePartition", Encode.int source.Partition.Value
        "sourceOffset", Encode.int64 source.Offset.Value
        "key", Encode.option Encode.string (Option.ofObj source.Message.Key)
        "originalValue", Encode.string source.Message.Value
    ]
    |> Encode.toString 0

let publish (producer: IProducer<string, string>) (source: ConsumeResult<string, string>) (error: string) =
    task {
        let message =
            Message<string, string>(Key = source.Message.Key, Value = encode source error)

        let! delivered = producer.ProduceAsync(topic, message)
        printfn $"[dlq]  {source.TopicPartitionOffset} -> dlq offset {delivered.Offset.Value}: {error}"
    }
