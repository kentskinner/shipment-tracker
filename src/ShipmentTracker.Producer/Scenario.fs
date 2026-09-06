module ShipmentTracker.Producer.Scenario

open System
open ShipmentTracker.Domain

// Sample pools - real UN/LOCODEs and carrier SCACs where it's cheap to be
// plausible. Simulation flavor only; nothing downstream depends on these.
let private locations =
    [ "Rotterdam"; "Shanghai"; "Hamburg"; "Los Angeles"; "Singapore"; "Felixstowe" ]

let private carriers = [ "MAEU"; "MSCU"; "CMDU"; "HLCU" ]
let private offices = [ "NL-RTM"; "CN-SHA"; "DE-HAM"; "US-LAX"; "SG-SIN" ]
let private people = [ "Fred"; "Ada"; "Grace"; "Linus"; "Barbara" ]

let private pick (random: Random) (xs: 'a list) = xs[random.Next xs.Length]

/// The five payloads of one shipment's lifecycle, with randomized details.
let private lifecyclePayloads (random: Random) : ShipmentEvent list =
    let origin = pick random locations
    let destination = locations |> List.filter ((<>) origin) |> pick random

    [ ShipmentCreated
          { Origin = origin
            Destination = destination
            CarrierId = CarrierId(pick random carriers) }
      ShipmentPickedUp { PickedUpBy = pick random people }
      ContainerLoaded { ContainerId = ContainerId $"CONT-%04d{random.Next 10000}" }
      CustomsCleared { ClearedBy = CustomsOffice(pick random offices) }
      ShipmentDelivered
          { SignatureName = pick random people
            DeliveredTo = pick random locations } ]

/// Randomly interleave several per-shipment queues into one sequence,
/// preserving each queue's internal order - the same guarantee Kafka
/// gives per partition, so the generator produces exactly the kind of
/// stream the projector must be correct against.
let rec private interleave (random: Random) (queues: ('id * 'a) list list) acc =
    match queues |> List.filter (List.isEmpty >> not) with
    | [] -> List.rev acc
    | active ->
        let idx = random.Next active.Length
        let head = List.head active[idx]
        let rest = active |> List.mapi (fun i q -> if i = idx then List.tail q else q)
        interleave random rest (head :: acc)

/// Generate a full interleaved scenario: `count` shipments, all five
/// lifecycle events each, shuffled together with an advancing clock.
/// Pure given the Random - the same seed reproduces the same scenario.
let generate (random: Random) (count: int) : Envelope list =
    let queues =
        [ for i in 1..count ->
              let sid = ShipmentId $"SIM-%03d{i}"
              let correlationId = CorrelationId(Guid.NewGuid())

              lifecyclePayloads random
              |> List.map (fun payload -> (sid, correlationId), payload) ]

    let start = DateTimeOffset.UtcNow

    interleave random queues []
    |> List.mapi (fun i ((sid, correlationId), payload) ->
        { ShipmentId = sid
          EventId = EventId(Guid.NewGuid())
          CorrelationId = correlationId
          // advancing clock: events land minutes apart, not all at once
          OccurredAt = start.AddMinutes(float (i * 7 + random.Next 5))
          SchemaVersion = 1
          Payload = payload })
