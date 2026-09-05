namespace ShipmentTracker.Domain

open System

type EventId = EventId of Guid
type CorrelationId = CorrelationId of Guid
type ShipmentId = ShipmentId of string
type ContainerId = ContainerId of string
type CarrierId = CarrierId of string

type CustomsOffice = CustomsOffice of string

type ShipmentCreatedData =
    { Origin: string
      Destination: string
      CarrierId: CarrierId }

type ContainerLoadedData = { ContainerId: ContainerId }

type ShipmentPickedUpData = { PickedUpBy: string } // Driver name

type CustomsClearedData = { ClearedBy: CustomsOffice }

type ShipmentDeliveredData =
    { SignatureName: string
      DeliveredTo: string }

type ShipmentEvent =
    | ShipmentCreated of ShipmentCreatedData
    | ShipmentPickedUp of ShipmentPickedUpData
    | ContainerLoaded of ContainerLoadedData
    | CustomsCleared of CustomsClearedData
    | ShipmentDelivered of ShipmentDeliveredData

type Envelope =
    { ShipmentId: ShipmentId
      EventId: EventId
      Payload: ShipmentEvent
      OccurredAt: DateTimeOffset
      CorrelationId: CorrelationId
      SchemaVersion: int }
