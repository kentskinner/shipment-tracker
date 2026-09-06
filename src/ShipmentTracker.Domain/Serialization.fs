namespace ShipmentTracker.Domain

open Thoth.Json.Net

module Serialization = 

    let private encodeShipmentId (ShipmentId raw) = Encode.string raw
    let private encodeCorrelationId (CorrelationId raw) = Encode.guid raw
    let private encodeEventId (EventId raw) = Encode.guid raw
    let private encodeCarrierId (CarrierId raw) = Encode.string raw
    let private encodeContainerId (ContainerId raw) = Encode.string raw
    let private encodeCustomsOffice (CustomsOffice raw) = Encode.string raw
    
    let private encodeShipmentEvent (event: ShipmentEvent) = 
        match event with
        | ShipmentCreated data -> 
            "ShipmentCreated",
            Encode.object [ 
                "origin", Encode.string data.Origin
                "destination", Encode.string data.Destination
                "carrierId", encodeCarrierId data.CarrierId
            ]
        | ShipmentPickedUp data ->
            "ShipmentPickedUp",
            Encode.object [ 
                "pickedUpBy", Encode.string data.PickedUpBy
            ]
        | ContainerLoaded data -> 
            "ContainerLoaded",
            Encode.object [ 
                "containerId", encodeContainerId data.ContainerId
            ]   
        | CustomsCleared data ->
            "CustomsCleared",
            Encode.object [ 
                "clearedBy", encodeCustomsOffice data.ClearedBy
            ]
        | ShipmentDelivered data ->
            "ShipmentDelivered",
            Encode.object [ 
                "signatureName", Encode.string data.SignatureName
                "deliveredTo", Encode.string data.DeliveredTo
            ]
    
    let encodeEnvelope (e: Envelope) =
        let eventType, payload = encodeShipmentEvent e.Payload
        Encode.object [
            "eventType", Encode.string eventType
            "shipmentId", encodeShipmentId e.ShipmentId
            "eventId", encodeEventId e.EventId
            "correlationId", encodeCorrelationId e.CorrelationId
            "schemaVersion", Encode.int e.SchemaVersion
            "occurredAt", Encode.datetimeOffset e.OccurredAt
            "payload", payload
        ]

    let private shipmentIdDecoder : Decoder<ShipmentId> =
        Decode.string |> Decode.map ShipmentId
    let private eventIdDecoder : Decoder<EventId> =
        Decode.guid |> Decode.map EventId
    let private correlationIdDecoder : Decoder<CorrelationId> =
        Decode.guid |> Decode.map CorrelationId
    let private carrierIdDecoder : Decoder<CarrierId> =
        Decode.string |> Decode.map CarrierId
    let private containerIdDecoder : Decoder<ContainerId> =
        Decode.string |> Decode.map ContainerId
    let private customsOfficeDecoder : Decoder<CustomsOffice> =
        Decode.string |> Decode.map CustomsOffice

    let private shipmentCreatedDecoder : Decoder<ShipmentCreatedData> =
        Decode.object (fun get ->
            { Origin = get.Required.Field "origin" Decode.string
              Destination = get.Required.Field "destination" Decode.string
              CarrierId = get.Required.Field "carrierId" carrierIdDecoder })

    let private containerLoadedDecoder : Decoder<ContainerLoadedData> =
        Decode.object (fun get ->
            { ContainerId = get.Required.Field "containerId" containerIdDecoder })

    let private shipmentPickedUpDecoder : Decoder<ShipmentPickedUpData> =
        Decode.object (fun get ->
            { PickedUpBy = get.Required.Field "pickedUpBy" Decode.string })

    let private customsClearedDecoder : Decoder<CustomsClearedData> =
        Decode.object (fun get ->
            { ClearedBy = get.Required.Field "clearedBy" customsOfficeDecoder })

    let private shipmentDeliveredDecoder : Decoder<ShipmentDeliveredData> =
        Decode.object (fun get ->
            { SignatureName = get.Required.Field "signatureName" Decode.string
              DeliveredTo = get.Required.Field "deliveredTo" Decode.string })

    let private payloadDecoder (eventType: string) : Decoder<ShipmentEvent> =
        match eventType with
        | "ShipmentCreated" -> shipmentCreatedDecoder |> Decode.map ShipmentCreated
        | "ShipmentPickedUp" -> shipmentPickedUpDecoder |> Decode.map ShipmentPickedUp
        | "ShipmentDelivered" -> shipmentDeliveredDecoder |> Decode.map ShipmentDelivered
        | "CustomsCleared" -> customsClearedDecoder |> Decode.map CustomsCleared
        | "ContainerLoaded" -> containerLoadedDecoder |> Decode.map ContainerLoaded
        | other -> Decode.fail $"unknown event type: {other}"

    let envelopeDecoder : Decoder<Envelope> =
        Decode.field "schemaVersion" Decode.int
        |> Decode.andThen(fun v ->
            if v = 1 then
                Decode.field "eventType" Decode.string
                |> Decode.andThen (fun t ->
                    Decode.object (fun get ->
                        { ShipmentId = get.Required.Field "shipmentId" shipmentIdDecoder
                          EventId = get.Required.Field "eventId" eventIdDecoder
                          OccurredAt = get.Required.Field "occurredAt" Decode.datetimeOffset
                          CorrelationId = get.Required.Field "correlationId" correlationIdDecoder
                          SchemaVersion = get.Required.Field "schemaVersion" Decode.int
                          Payload = get.Required.Field "payload" (payloadDecoder t) }))
            else
                Decode.fail $"unsupported schema version: {v}")

    let serializeEnvelope (e: Envelope) : string =
        encodeEnvelope e |> Encode.toString 0

    let decodeEnvelope (json: string) : Result<Envelope, string> =
        Decode.fromString envelopeDecoder json