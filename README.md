# shipment-tracker

Event-driven shipment tracking in F# with Kafka. **Work in progress.**

A small system built to be deeply understood rather than broad: shipment
lifecycle events flow through Kafka (partitioned by shipment ID), and an F#
consumer folds them into a current-state projection — with deliberate
attention to ordering, offsets, idempotency, retries, and dead-lettering.

A full README covering the architecture and its tradeoffs will land once the
system does. So far: the pure domain model (events, projection state, and a
total transition function) with the messaging and infrastructure layers to
follow.
