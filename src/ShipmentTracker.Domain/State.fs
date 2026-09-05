namespace ShipmentTracker.Domain

type ShipmentStatus =
    | Created
    | InTransit
    | Delivered

type ShipmentState =
    { Status: ShipmentStatus
      ShipmentId: ShipmentId
      Origin: string
      Destination: string
      CarrierId: CarrierId
      LastEventId: EventId
      PickedUpBy: string option
      ContainerId: ContainerId option
      ClearedBy: CustomsOffice option
      SignatureName: string option
      DeliveredTo: string option }

module Shipment =

    type TransitionError =
        | AlreadyCreated of ShipmentId * EventId
        | EventForUnknownShipment of ShipmentId * EventId
        | UnexpectedEvent of ShipmentId * ShipmentStatus * EventId

    let apply (current: ShipmentState option) (envelope: Envelope) : Result<ShipmentState, TransitionError> =
        match current, envelope.Payload with
        | None, ShipmentCreated data ->
            Ok
                { ShipmentId = envelope.ShipmentId
                  Status = Created
                  Origin = data.Origin
                  Destination = data.Destination
                  CarrierId = data.CarrierId
                  PickedUpBy = None
                  ContainerId = None
                  ClearedBy = None
                  SignatureName = None
                  DeliveredTo = None
                  LastEventId = envelope.EventId }
        | Some state, ShipmentCreated _ -> Error(AlreadyCreated(state.ShipmentId, envelope.EventId))
        | Some state, ShipmentPickedUp data when state.Status = Created ->
            Ok
                { state with
                    Status = InTransit
                    PickedUpBy = Some data.PickedUpBy
                    LastEventId = envelope.EventId }
        | Some state, ContainerLoaded data when state.Status = Created || state.Status = InTransit ->
            Ok
                { state with
                    ContainerId = Some data.ContainerId
                    LastEventId = envelope.EventId }
        | Some state, CustomsCleared data when state.Status = InTransit ->
            Ok
                { state with
                    ClearedBy = Some data.ClearedBy
                    LastEventId = envelope.EventId }
        | Some state, ShipmentDelivered data when state.Status = InTransit ->
            Ok
                { state with
                    Status = Delivered
                    SignatureName = Some data.SignatureName
                    DeliveredTo = Some data.DeliveredTo
                    LastEventId = envelope.EventId }
        | None, _ -> Error(EventForUnknownShipment(envelope.ShipmentId, envelope.EventId))
        | Some state, _ -> Error(UnexpectedEvent(state.ShipmentId, state.Status, envelope.EventId))
