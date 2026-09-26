using OrderFlow.Domain.Orders;
using System.Globalization;

namespace OrderFlow.Application.Orders;

public sealed record ApiOrder(string ClOrdId, string Symbol, string Side, int Quantity, string Price,
    string Status, DateTime CreatedAt, DateTime UpdatedAt, string? RejectionReason, ExecutionReportView? ExecutionReport, string AccountId = "CLIENTE-001")
{
    public static ApiOrder From(SubmissionView row)
    {
        return new(row.ClOrdId, row.Symbol, row.Side.ToCode(), (int)row.Quantity,
        row.Price.ToString("F2", CultureInfo.InvariantCulture), row.Status == "New" ? "Accepted" : row.Status,
        row.CreatedAt, row.UpdatedAt ?? row.CreatedAt, row.Reason,
        row.ExecId is null ? null : new(row.ExecId, row.Status == "New" ? "New" : "Rejected", row.UpdatedAt!.Value), row.AccountId);
    }
}
