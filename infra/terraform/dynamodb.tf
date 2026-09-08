# The projection store: one item per shipment, keyed by shipmentId.
# DynamoDB is schemaless beyond its keys, so only the key attribute is
# declared here; the projection fields appear when items are written.
# PAY_PER_REQUEST bills per operation (about $1.25 per million writes)
# with no idle cost, which suits a store that is usually idle.

resource "aws_dynamodb_table" "projections" {
  name         = "shipment-projections"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "shipmentId"

  attribute {
    name = "shipmentId"
    type = "S"
  }
}

output "projection_table_name" {
  value = aws_dynamodb_table.projections.name
}
