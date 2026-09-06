module ShipmentTracker.Producer.Program

open System
open Confluent.Kafka
open ShipmentTracker.Domain

let private topic = "shipment-events"

/// A full five-event lifecycle for one shipment. One CorrelationId spans
/// the whole flow: correlation identifies the business journey, while each
/// event keeps its own EventId.
let private lifecycle (shipmentId: ShipmentId) : Envelope list =
    let correlationId = CorrelationId(Guid.NewGuid())

    let envelope payload =
        { ShipmentId = shipmentId
          EventId = EventId(Guid.NewGuid())
          CorrelationId = correlationId
          OccurredAt = DateTimeOffset.UtcNow
          SchemaVersion = 1
          Payload = payload }

    [ ShipmentCreated
          { Origin = "Rotterdam"
            Destination = "Singapore"
            CarrierId = CarrierId "MAEU" }
      ShipmentPickedUp { PickedUpBy = "Fred" }
      ContainerLoaded { ContainerId = ContainerId "CONT-001" }
      CustomsCleared { ClearedBy = CustomsOffice "NL-RTM" }
      ShipmentDelivered
          { SignatureName = "Ada"
            DeliveredTo = "Singapore warehouse" } ]
    |> List.map envelope

let private publish (producer: IProducer<string, string>) (envelope: Envelope) =
    task {
        // The partition key is the raw shipmentId string: same key -> same
        // partition -> per-shipment ordering. Wrappers stay out of the wire.
        let (ShipmentId key) = envelope.ShipmentId

        let message =
            Message<string, string>(Key = key, Value = Serialization.serializeEnvelope envelope)

        let! result = producer.ProduceAsync(topic, message)
        printfn $"{key} -> partition {result.Partition.Value}, offset {result.Offset.Value}"
    }

let private publishAll (envelopes: Envelope list) =
    let config =
        ProducerConfig(
            BootstrapServers = "localhost:9092",
            // Acks.All: the write is confirmed only once fully replicated.
            Acks = Acks.All,
            // Broker-side dedup of client retries: a lost ack cannot
            // produce a duplicate write. (Distinct from consumer-side
            // idempotency, which the projector handles.)
            EnableIdempotence = true
        )

    use producer = ProducerBuilder<string, string>(config).Build()

    // Awaiting each send before the next preserves publish order even
    // beyond what idempotence already guarantees about retries.
    for envelope in envelopes do
        (publish producer envelope).GetAwaiter().GetResult()

    printfn $"published {List.length envelopes} events"

[<EntryPoint>]
let main argv =
    match argv with
    | [| "demo"; shipmentId |] ->
        publishAll (lifecycle (ShipmentId shipmentId))
        0
    | [| "simulate"; count |] ->
        publishAll (Scenario.generate (Random()) (int count))
        0
    | [| "simulate"; count; seed |] ->
        // Seeded runs reproduce the exact same scenario - same shipments,
        // same details, same interleaving.
        publishAll (Scenario.generate (Random(int seed)) (int count))
        0
    | _ ->
        eprintfn "usage: dotnet run -- demo <shipmentId>"
        eprintfn "       dotnet run -- simulate <shipmentCount> [seed]"
        1
