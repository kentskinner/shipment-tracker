module TransitionTests

open System
open Xunit
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

let buildEnvelope shipmentId payload =
    { ShipmentId = shipmentId
      EventId = EventId(Guid.NewGuid())
      CorrelationId = CorrelationId(Guid.NewGuid())
      OccurredAt = DateTimeOffset.UtcNow
      SchemaVersion = 1
      Payload = payload }

let applyAll (envelopes: Envelope list) : Result<ShipmentState option, Shipment.TransitionError> =
    let folder acc envelope =
        acc
        |> Result.bind (fun state -> Shipment.apply state envelope |> Result.map Some)

    List.fold folder (Ok None) envelopes

[<Fact>]
let ``creating a shipment from nothing yields Created state`` () =
    let envelope =
        buildEnvelope
            (ShipmentId "SHP-001")
            (ShipmentCreated
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
          EventId = EventId(Guid.NewGuid())
          CorrelationId = CorrelationId(Guid.NewGuid())
          OccurredAt = DateTimeOffset.UtcNow
          SchemaVersion = 1
          Payload = ShipmentPickedUp { PickedUpBy = "Fred" } }

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

    let envelope =
        buildEnvelope
            (ShipmentId "SHP-001")
            (ShipmentCreated
                { Origin = "Rotterdam"
                  Destination = "Singapore"
                  CarrierId = CarrierId "MAEU" })

    let result = Shipment.apply (Some state) envelope

    match result with
    | Ok state -> failwith $"expected Error, got {state}"
    | Error e -> Assert.Equal(Shipment.AlreadyCreated(ShipmentId "SHP-001", envelope.EventId), e)

[<Fact>]
let ``container loaded when created succeeds with status remaining created`` () =

    let state = buildState (ShipmentId "SHP-001")

    let envelope =
        buildEnvelope (ShipmentId "SHP-001") (ContainerLoaded { ContainerId = ContainerId "CONT-001" })

    let result = Shipment.apply (Some state) envelope

    match result with
    | Ok state ->
        Assert.Equal(Created, state.Status)
        Assert.Equal(Some(ContainerId "CONT-001"), state.ContainerId)
        Assert.Equal(envelope.EventId, state.LastEventId)
    | Error e -> failwith $"expected Ok, got {e}"

[<Fact>]
let ``event for unknown shipment returns error`` () =

    let envelope =
        buildEnvelope (ShipmentId "SHP-001") (ShipmentPickedUp { PickedUpBy = "Fred" })

    let result = Shipment.apply None envelope

    match result with
    | Ok state -> failwith $"expected Error, got {state}"
    | Error e -> Assert.Equal(Shipment.EventForUnknownShipment(ShipmentId "SHP-001", envelope.EventId), e)

[<Fact>]
let ``picked up when already in transit returns error`` () =

    let state =
        { buildState (ShipmentId "SHP-001") with
            Status = InTransit }

    let envelope =
        buildEnvelope (ShipmentId "SHP-001") (ShipmentPickedUp { PickedUpBy = "Fred" })

    let result = Shipment.apply (Some state) envelope

    match result with
    | Ok state -> failwith $"expected Error, got {state}"
    | Error e -> Assert.Equal(Shipment.UnexpectedEvent(ShipmentId "SHP-001", InTransit, envelope.EventId), e)

[<Fact>]
let ``event after delivery returns error`` () =

    let state =
        { buildState (ShipmentId "SHP-001") with
            Status = Delivered }

    let envelope =
        buildEnvelope (ShipmentId "SHP-001") (ContainerLoaded { ContainerId = ContainerId "CONT-001" })

    let result = Shipment.apply (Some state) envelope

    match result with
    | Ok state -> failwith $"expected Error, got {state}"
    | Error e -> Assert.Equal(Shipment.UnexpectedEvent(ShipmentId "SHP-001", Delivered, envelope.EventId), e)

[<Fact>]
let ``delivered without pickup returns error`` () =

    let state = buildState (ShipmentId "SHP-001")

    let envelope =
        buildEnvelope
            (ShipmentId "SHP-001")
            (ShipmentDelivered
                { SignatureName = "Ada"
                  DeliveredTo = "Singapore warehouse" })

    let result = Shipment.apply (Some state) envelope

    match result with
    | Ok state -> failwith $"expected Error, got {state}"
    | Error e -> Assert.Equal(Shipment.UnexpectedEvent(ShipmentId "SHP-001", Created, envelope.EventId), e)

[<Fact>]
let ``Lifecycle folder`` () =
    let events =
        [ buildEnvelope
              (ShipmentId "SHP-001")
              (ShipmentCreated
                  { Origin = "Rotterdam"
                    Destination = "Singapore"
                    CarrierId = CarrierId "MAEU" })
          buildEnvelope (ShipmentId "SHP-001") (ShipmentPickedUp { PickedUpBy = "Fred" })
          buildEnvelope (ShipmentId "SHP-001") (ContainerLoaded { ContainerId = ContainerId "CONT-001" })
          buildEnvelope (ShipmentId "SHP-001") (CustomsCleared { ClearedBy = CustomsOffice "CUSTOMS-001" })
          buildEnvelope
              (ShipmentId "SHP-001")
              (ShipmentDelivered
                  { SignatureName = "Ada"
                    DeliveredTo = "Singapore warehouse" }) ]

    let result = applyAll events

    match result with
    | Ok(Some state) ->
        Assert.Equal(Delivered, state.Status)
        Assert.Equal(Some(CustomsOffice "CUSTOMS-001"), state.ClearedBy)
        Assert.Equal(Some(ContainerId "CONT-001"), state.ContainerId)
        Assert.Equal(Some "Ada", state.SignatureName)
    | other -> failwith $"expected Ok, got {other}"
