module Builders

open System
open ShipmentTracker.Domain

let buildState shipmentId =
    { Status = Created
      ShipmentId = shipmentId
      Origin = "Rotterdam"
      Destination = "Singapore"
      CarrierId = CarrierId "MAEU"
      LastEventId = EventId(Guid.NewGuid())
      PickedUpBy = None
      ContainerId = None
      ClearedBy = None
      SignatureName = None
      DeliveredTo = None }

/// One canonical example of every event type — tests that must cover
/// the whole contract loop over this instead of hand-picking cases.
let allPayloads =
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

let buildEnvelope shipmentId payload =
    { ShipmentId = shipmentId
      EventId = EventId(Guid.NewGuid())
      CorrelationId = CorrelationId(Guid.NewGuid())
      OccurredAt = DateTimeOffset.UtcNow
      SchemaVersion = 1
      Payload = payload }