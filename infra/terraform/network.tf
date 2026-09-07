# The network layout: a VPC with only private subnets. Nothing in this
# system faces the internet, so there is no public tier, no internet
# gateway, and no NAT gateway. Egress to AWS services goes through VPC
# endpoints instead (the DynamoDB gateway endpoint below is free).

data "aws_availability_zones" "available" {
  state = "available"
}

resource "aws_vpc" "main" {
  cidr_block           = var.vpc_cidr
  enable_dns_support   = true
  enable_dns_hostnames = true # MSK bootstrap addresses are DNS names

  tags = { Name = "shipment-tracker" }
}

# Private subnets 10.0.10.0/24, 10.0.11.0/24, ... one per AZ.
# The .10 offset leaves low numbers free in case an edge tier is ever
# needed, and encodes "app tier" into the address itself.
resource "aws_subnet" "private" {
  count             = var.az_count
  vpc_id            = aws_vpc.main.id
  cidr_block        = cidrsubnet(var.vpc_cidr, 8, 10 + count.index)
  availability_zone = data.aws_availability_zones.available.names[count.index]

  tags = { Name = "shipment-tracker-private-${count.index}" }
}

# One route table for the private tier. Its only route is the implicit
# local one (traffic within 10.0.0.0/16); with no default route these
# subnets cannot reach or be reached from the internet at all.
resource "aws_route_table" "private" {
  vpc_id = aws_vpc.main.id

  tags = { Name = "shipment-tracker-private" }
}

resource "aws_route_table_association" "private" {
  count          = var.az_count
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private.id
}

# Gateway endpoint so the private subnets can reach DynamoDB without a
# NAT gateway (~$35/month). Gateway endpoints exist only for S3 and
# DynamoDB and are free, including traffic.
resource "aws_vpc_endpoint" "dynamodb" {
  vpc_id            = aws_vpc.main.id
  service_name      = "com.amazonaws.${var.region}.dynamodb"
  vpc_endpoint_type = "Gateway"
  route_table_ids   = [aws_route_table.private.id]

  tags = { Name = "shipment-tracker-dynamodb" }
}
