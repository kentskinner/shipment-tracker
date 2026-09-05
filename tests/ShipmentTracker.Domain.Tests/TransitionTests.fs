module TransitionTests

open System
open Xunit
open ShipmentTracker.Domain

let buildState shipmentId = 
        {   Status = Created
            ShipmentId = shipmentId
            Origin = "Rotterdam"
            Destination = "Singapore"
            CarrierId = CarrierId "MAEU"
            LastEventId = EventId (Guid.NewGuid())
            PickedUpBy = None
            ContainerId = None
            ClearedBy = None
            SignatureName = None
            DeliveredTo = None }

let buildEnvelope shipmentId payload =
          { ShipmentId = shipmentId
            EventId = EventId (Guid.NewGuid())
            CorrelationId = CorrelationId (Guid.NewGuid())
            OccurredAt = DateTimeOffset.UtcNow
            SchemaVersion = 1
            Payload = payload }

[<Fact>]
let ``creating a shipment from nothing yields Created state`` () =
    let envelope = buildEnvelope (ShipmentId "SHP-001") (ShipmentCreated
                    { Origin = "Rotterdam"
                      Destination = "Singapore"
                      CarrierId = CarrierId "MAEU" })
                    
    let result = Shipment.apply None envelope

    match result with
        | Ok state -> 
            Assert.Equal(Created, state.Status)
            Assert.Equal("Rotterdam", state.Origin)
            Assert.Equal(envelope.EventId, state.LastEventId)
        | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``picked-up-from-Created goes InTransit`` () =

    let state = buildState (ShipmentId "SHP-001")

    let envelope =
          { ShipmentId = ShipmentId "SHP-001"
            EventId = EventId (Guid.NewGuid())
            CorrelationId = CorrelationId (Guid.NewGuid())
            OccurredAt = DateTimeOffset.UtcNow
            SchemaVersion = 1
            Payload =
                ShipmentPickedUp
                    { PickedUpBy = "Fred" } }
    let result = Shipment.apply (Some state) envelope

    match result with
        | Ok state -> 
            Assert.Equal(InTransit, state.Status)
            Assert.Equal(Some "Fred", state.PickedUpBy)
            Assert.Equal(envelope.EventId, state.LastEventId)
        | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``created when already created returns error`` () =

    let state = buildState (ShipmentId "SHP-001")

    let envelope = buildEnvelope (ShipmentId "SHP-001") (ShipmentCreated
                    { Origin = "Rotterdam"
                      Destination = "Singapore"
                      CarrierId = CarrierId "MAEU" })

    let result = Shipment.apply (Some state) envelope

    match result with
        | Ok state -> failwith $"expected Error, got {state}"
        | Error e -> 
            Assert.Equal(Shipment.AlreadyCreated(ShipmentId "SHP-001", envelope.EventId), e)