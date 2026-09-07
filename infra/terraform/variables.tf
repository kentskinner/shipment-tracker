variable "region" {
  description = "AWS region for all resources"
  type        = string
  default     = "us-west-2"
}

variable "vpc_cidr" {
  description = "Address range for the VPC. /16 is the AWS maximum; there is no cost to a large range, and shrinking one later means renumbering."
  type        = string
  default     = "10.0.0.0/16"
}

# Two AZs: the minimum for MSK broker replication, and the unit of
# physical failure worth planning around.
variable "az_count" {
  description = "Number of availability zones to spread subnets across"
  type        = number
  default     = 2
}
