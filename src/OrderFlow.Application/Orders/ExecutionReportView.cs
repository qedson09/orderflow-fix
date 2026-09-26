namespace OrderFlow.Application.Orders;

public sealed record ExecutionReportView(string ExecId, string ExecType, DateTime ReceivedAt);
