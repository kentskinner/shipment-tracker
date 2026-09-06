module SerializationTests

open Xunit
open ShipmentTracker.Domain
open ShipmentTracker.Domain.Serialization
open Builders

[<Fact>]
let ``every event type round-trips through the wire format unchanged`` () =
    for payload in allPayloads do
        let envelope = buildEnvelope (ShipmentId "SHP-001") payload
        let result = envelope |> serializeEnvelope |> decodeEnvelope
        Assert.Equal(Ok envelope, result)

[<Fact>]
let ``garbage input yields an Error, not an exception`` () =
    match decodeEnvelope "this is not even json" with
    | Error _ -> ()
    | Ok e -> failwith $"expected Error, got {e}"

[<Fact>]
let ``unknown event type yields an Error naming the bad type`` () =
    let json =
        buildEnvelope (ShipmentId "SHP-001") (ShipmentPickedUp { PickedUpBy = "Fred" })
        |> serializeEnvelope

    let sabotaged = json.Replace("ShipmentPickedUp", "TeleportInitiated")

    match decodeEnvelope sabotaged with
    | Error msg -> Assert.Contains("TeleportInitiated", msg)
    | Ok e -> failwith $"expected Error, got {e}"

[<Fact>]
let ``unsupported schema version yields an Error naming the version`` () =
    let json =
        buildEnvelope (ShipmentId "SHP-001") (ShipmentPickedUp { PickedUpBy = "Fred" })
        |> serializeEnvelope

    let sabotaged = json.Replace("\"schemaVersion\":1", "\"schemaVersion\":99")

    match decodeEnvelope sabotaged with
    | Error msg -> Assert.Contains("99", msg)
    | Ok e -> failwith $"expected Error, got {e}"
