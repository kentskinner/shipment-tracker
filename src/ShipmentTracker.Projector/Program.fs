module ShipmentTracker.Projector.Program

open System
open System.Collections.Generic
open System.Threading
open Confluent.Kafka
open ShipmentTracker.Domain

let private topic = "shipment-events"

// In-memory projection store: shipmentId -> current state.
// Milestone 4 replaces this with DynamoDB and conditional writes; until
// then a restart loses the projection, which is fine - it rebuilds by
// replaying the topic (that is the point of keeping the log).
let private store = Dictionary<ShipmentId, ShipmentState>()

let private tryFind id =
    match store.TryGetValue id with
    | true, state -> Some state
    | _ -> None

let private describe (state: ShipmentState) =
    let (ShipmentId sid) = state.ShipmentId
    let extras =
        [ state.ContainerId |> Option.map (fun (ContainerId c) -> $"container {c}")
          state.ClearedBy |> Option.map (fun (CustomsOffice o) -> $"cleared by {o}")
          state.SignatureName |> Option.map (fun s -> $"signed {s}") ]
        |> List.choose id
    match extras with
    | [] -> $"{sid}: {state.Status}"
    | xs -> $"""{sid}: {state.Status} ({String.Join(", ", xs)})"""

/// Handle one message. Failure policy is deliberately crude for now:
/// undecodable messages and rejected transitions are logged and skipped.
/// The dead-letter topic replaces this - a poison message must never
/// halt the partition, but it also must not vanish silently.
let private handle (result: ConsumeResult<string, string>) =
    match Serialization.decodeEnvelope result.Message.Value with
    | Error err ->
        printfn $"[skip] undecodable message at {result.TopicPartitionOffset}: {err}"
    | Ok envelope ->
        match Shipment.apply (tryFind envelope.ShipmentId) envelope with
        | Error rejection ->
            printfn $"[skip] transition rejected at {result.TopicPartitionOffset}: %A{rejection}"
        | Ok newState ->
            store[envelope.ShipmentId] <- newState
            printfn $"[ok]   {describe newState}"

[<EntryPoint>]
let main argv =
    // --idle-exit N: exit after N seconds with no messages (for tests and
    // scripted runs). Without it, run until Ctrl+C like a real service.
    let idleExitSeconds =
        match argv with
        | [| "--idle-exit"; n |] -> Some(int n)
        | _ -> None

    let config =
        ConsumerConfig(
            BootstrapServers = "localhost:9092",
            GroupId = "shipment-projector",
            // Manual commits: the offset moves only after we have processed
            // and stored the result. Crash before the commit -> redelivery
            // -> at-least-once. Duplicates are the projector's problem to
            // absorb (idempotency, milestone 4), not Kafka's to prevent.
            EnableAutoCommit = false,
            // Only applies when the group has no committed offset yet:
            // start from the beginning of the log. (A group's committed
            // offset always wins over this setting - reusing an old group
            // id would silently skip history.)
            AutoOffsetReset = AutoOffsetReset.Earliest
        )

    use cts = new CancellationTokenSource()
    Console.CancelKeyPress.Add(fun e ->
        e.Cancel <- true
        cts.Cancel())

    use consumer = ConsumerBuilder<string, string>(config).Build()
    consumer.Subscribe(topic)
    printfn $"projector consuming '{topic}' as group '{config.GroupId}' - Ctrl+C to stop"

    let mutable idleSeconds = 0
    try
        while not cts.IsCancellationRequested do
            match consumer.Consume(TimeSpan.FromSeconds 1.0) with
            | null ->
                idleSeconds <- idleSeconds + 1
                match idleExitSeconds with
                | Some limit when idleSeconds >= limit -> cts.Cancel()
                | _ -> ()
            | result ->
                idleSeconds <- 0
                handle result
                // Commit AFTER processing: this line is the at-least-once
                // guarantee. Moving it before handle would make delivery
                // at-most-once (crash after commit, before processing =
                // event lost forever).
                consumer.Commit(result)
    finally
        // Leave the group cleanly so partitions rebalance immediately
        // instead of waiting for the session timeout.
        consumer.Close()

    0
