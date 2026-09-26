using OrderFlow.Domain.Orders;
using OrderFlow.Infrastructure.Persistence.Orders;

namespace OrderFlow.IntegrationTests;

[Collection("Postgres")]
public sealed class PaginationTests(PostgresFixture fixture)
{
    [Fact]
    public async Task PagesHaveStableOrderTotalsAndAccountIsolation()
    {
        await fixture.ResetAsync();

        await using var db = fixture.Generator();

        var timestamp = DateTime.UtcNow;

        for (var i = 0; i < 23; i++)
            db.Submissions.Add(new Submission { ClOrdId = Guid.NewGuid().ToString("D"), AccountId = "CLIENTE-001",
                Symbol = "PETR4", Side = OrderSide.Buy, Price = 1, Quantity = 1, CreatedAt = timestamp });

        db.Submissions.Add(new Submission { ClOrdId = Guid.NewGuid().ToString("D"), AccountId = "CLIENTE-002",
            Symbol = "VALE3", Side = OrderSide.Sell, Price = 1, Quantity = 1, CreatedAt = timestamp });

        await db.SaveChangesAsync();

        var first = await SubmissionQueries.ListPageAsync(db, "CLIENTE-001", 1, 10);

        var second = await SubmissionQueries.ListPageAsync(db, "CLIENTE-001", 2, 10);

        var last = await SubmissionQueries.ListPageAsync(db, "CLIENTE-001", int.MaxValue, 10);

        Assert.Equal(23, first.TotalCount); Assert.Equal(3, first.TotalPages);

        Assert.Equal(10, first.Items.Count); Assert.Equal(10, second.Items.Count);

        Assert.Equal(3, last.Page); Assert.Equal(3, last.Items.Count);

        var ids = first.Items.Concat(second.Items).Concat(last.Items).Select(x => x.ClOrdId).ToList();

        Assert.Equal(23, ids.Distinct().Count());

        Assert.Equal(ids.OrderByDescending(x => x, StringComparer.Ordinal), ids);

        Assert.All(first.Items.Concat(second.Items).Concat(last.Items), x => Assert.Equal("CLIENTE-001", x.AccountId));

        var other = await SubmissionQueries.ListPageAsync(db, "CLIENTE-002", 1, 10);

        Assert.Single(other.Items); Assert.Equal("S", other.Items[0].Side);

        var empty = await SubmissionQueries.ListPageAsync(db, "missing", 1, 10);

        Assert.Empty(empty.Items); Assert.Equal(0, empty.TotalPages); Assert.Equal(1, empty.Page);
    }

    [Theory]
    [InlineData(0, 10)][InlineData(-1, 10)][InlineData(1, 0)][InlineData(1, 101)]
    public async Task InvalidPaginationIsRejected(int page, int size)
    {
        await using var db = fixture.Generator();

        await Assert.ThrowsAsync<ArgumentException>(() => SubmissionQueries.ListPageAsync(db, null, page, size));
    }
}
