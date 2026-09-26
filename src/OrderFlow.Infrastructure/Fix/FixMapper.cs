using OrderFlow.Application.Orders;
using OrderFlow.Domain.Orders;
using QuickFix.Fields;
using QuickFix.FIX44;
namespace OrderFlow.Infrastructure.Fix;

public static class FixMapper
{
    public static NewOrderSingle ToMessage(OrderRequest request, DateTime createdAt)
    {
        var message = new NewOrderSingle(new ClOrdID(request.ClOrdId), new Symbol(request.Symbol), new Side(ToFixSide(request.Side)),
            new TransactTime(createdAt), new OrdType(OrdType.LIMIT));

        message.SetField(new Symbol(request.Symbol)); message.SetField(new OrderQty(request.Quantity));

        message.SetField(new Price(request.Price)); message.SetField(new TimeInForce(TimeInForce.DAY));

        message.SetField(new Account(request.AccountId));

        return message;
    }

    public static OrderRequest FromMessage(NewOrderSingle message) => new(message.ClOrdID.Value,
        message.Symbol.Value, FromFixSide(message.Side.Value), message.OrderQty.Value, message.Price.Value,
        message.IsSetField(Tags.Account) ? message.Account.Value : "CLIENTE-001");

    public static ExecutionReport ToMessage(OrderDecision decision)
    {
        var accepted = decision.Status == OrderStatus.New;

        var report = new ExecutionReport(new OrderID(decision.Id.ToString("N")), new ExecID(decision.ExecId),
            new ExecType(accepted ? ExecType.NEW : ExecType.REJECTED), new OrdStatus(accepted ? OrdStatus.NEW : OrdStatus.REJECTED),
            new Symbol(decision.Symbol), new Side(ToFixSide(decision.Side)),
            new LeavesQty(accepted ? decision.Quantity : 0), new CumQty(0), new AvgPx(0));

        report.SetField(new ClOrdID(decision.ClOrdId)); report.SetField(new OrderQty(decision.Quantity));

        report.SetField(new Account(decision.AccountId));

        report.SetField(new Price(decision.Price)); report.SetField(new TransactTime(DateTime.UtcNow));

        if (decision.Reason is not null)
        {
            // FIX session uses ASCII; keep business text readable without encoding mismatch.
            report.SetField(new Text(ToAscii(decision.Reason)));

            report.SetField(new OrdRejReason(OrdRejReason.OTHER));
        }
        return report;
    }

    public static OrderDecision FromMessage(ExecutionReport report)
    {
        var accepted = report.ExecType.Value == ExecType.NEW && report.OrdStatus.Value == OrdStatus.NEW;

        var rejected = report.ExecType.Value == ExecType.REJECTED && report.OrdStatus.Value == OrdStatus.REJECTED;

        if ((!accepted && !rejected) || report.CumQty.Value != 0 || report.AvgPx.Value != 0 ||
            report.LeavesQty.Value != (accepted ? report.OrderQty.Value : 0))
            throw new InvalidOperationException("ExecutionReport incompatível com o contrato do desafio.");

        return new(Guid.ParseExact(report.OrderID.Value, "N"), report.ClOrdID.Value, report.ExecID.Value,
            report.Symbol.Value, FromFixSide(report.Side.Value), report.OrderQty.Value, report.Price.Value,
            accepted ? OrderStatus.New : OrderStatus.Rejected,
            report.IsSetField(Tags.Text) ? report.Text.Value : null,
            report.IsSetField(Tags.Account) ? report.Account.Value : "CLIENTE-001");
    }

    private static char ToFixSide(OrderSide side) => side switch
    {
        OrderSide.Buy => Side.BUY,
        OrderSide.Sell => Side.SELL,
        _ => throw new ArgumentException("Lado não suportado.", nameof(side))
    };

    private static OrderSide FromFixSide(char side) => side switch
    {
        Side.BUY => OrderSide.Buy,
        Side.SELL => OrderSide.Sell,
        _ => throw new QuickFix.IncorrectTagValue(Tags.Side)
    };

    private static string ToAscii(string text) => new([.. text.Normalize(System.Text.NormalizationForm.FormD).Where(c => c <= 127)]);
}
