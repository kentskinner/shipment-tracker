# Two security groups. The broker group's ingress rule references the
# app group by ID instead of by IP range, so the rule keeps working as
# app instances are replaced and change addresses.

resource "aws_security_group" "app" {
  name        = "shipment-tracker-app"
  description = "Producer / projector instances"
  vpc_id      = aws_vpc.main.id

  # Stateful egress: replies to outbound requests are allowed back in
  # automatically, so no ingress rules are needed here at all.
  egress {
    description = "All outbound (brokers, DynamoDB endpoint, CloudWatch)"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "shipment-tracker-app" }
}

resource "aws_security_group" "kafka" {
  name        = "shipment-tracker-kafka"
  description = "MSK brokers"
  vpc_id      = aws_vpc.main.id

  ingress {
    description     = "Kafka TLS from app instances only"
    from_port       = 9094 # MSK's TLS listener port (9092 is plaintext, disabled)
    to_port         = 9094
    protocol        = "tcp"
    security_groups = [aws_security_group.app.id]
  }

  egress {
    description = "Broker-to-broker replication and control traffic"
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = { Name = "shipment-tracker-kafka" }
}
