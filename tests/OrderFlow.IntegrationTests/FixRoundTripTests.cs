using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrderFlow.Infrastructure.Fix;
using QuickFix;
using QuickFix.Fields;
using QuickFix.Store;
using QuickFix.Transport;
using QuickFix.FIX44;
using OrderFlow.Application.Orders;
using OrderFlow.Infrastructure.Persistence.Orders;
using OrderFlow.Infrastructure.Persistence.Configuration;
using OrderFlow.Domain.Orders;

namespace OrderFlow.IntegrationTests;

[Collection("Postgres")]
public sealed class FixRoundTripTests(PostgresFixture fixture)
{
    [Fact]
    public async Task RealFix44SocketAcceptsRejectsAndRecoversDuplicate()
    {
        await fixture.ResetAsync();

        var root = Path.Combine(Path.GetTempPath(), "orderflow-fix-test-"+Guid.NewGuid().ToString("D"));

        Directory.CreateDirectory(root);

        var listener = new TcpListener(IPAddress.Loopback,0); listener.Start(); var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();

        var services = new ServiceCollection().AddLogging().AddAccumulatorDatabase(fixture.ConnectionString)
            .AddScoped<IAccumulatorUnitOfWork,PostgresAccumulatorUnitOfWork>().AddScoped<IOrderProcessor,ProcessOrder>().BuildServiceProvider();

        await using var ownedServices = services;

        var acceptApp = new AccumulatorFixApplication(services.GetRequiredService<IServiceScopeFactory>(),NullLogger<AccumulatorFixApplication>.Instance);

        var probe = new Probe();

        var acceptSettings = Settings("acceptor",root,port); var initSettings=Settings("initiator",root,port);

        using var acceptor = new ThreadedSocketAcceptor(acceptApp,new FileStoreFactory(acceptSettings),acceptSettings,NullLoggerFactory.Instance,new MessageFactory());

        using var initiator = new SocketInitiator(probe,new FileStoreFactory(initSettings),initSettings,NullLoggerFactory.Instance,new MessageFactory());

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        try
        {
            acceptor.Start(); initiator.Start();

            while (!probe.IsLoggedOn) await Task.Delay(50,timeout.Token);

            var request=new OrderRequest(Guid.NewGuid().ToString("D"),"PETR4",OrderSide.Buy,99999,999.99m);

            probe.Send(request); var accepted=await probe.Reports.Reader.ReadAsync(timeout.Token);

            Assert.Equal("FIX.4.4",accepted.Header.GetString(Tags.BeginString));

            Assert.Equal(ExecType.NEW,accepted.ExecType.Value); Assert.Equal(OrdStatus.NEW,accepted.OrdStatus.Value);

            Assert.Equal(0,accepted.CumQty.Value); Assert.Equal(0,accepted.AvgPx.Value); Assert.Equal(99999,accepted.LeavesQty.Value);

            Assert.Equal(request.ClOrdId,accepted.ClOrdID.Value);

            // Reiniciar a engine do cliente preserva FileStore e exige reconciliação da mesma identidade.
            var previousSequence = accepted.Header.GetInt(Tags.MsgSeqNum);

            initiator.Stop(); initiator.Start();

            while (!probe.IsLoggedOn) await Task.Delay(50,timeout.Token);

            probe.Send(request); var duplicate=await probe.Reports.Reader.ReadAsync(timeout.Token);

            Assert.True(duplicate.Header.GetInt(Tags.MsgSeqNum) > previousSequence);

            Assert.Equal(accepted.ExecID.Value,duplicate.ExecID.Value);

            probe.Send(request with { ClOrdId=Guid.NewGuid().ToString("D"), Quantity=10 });

            var rejected=await probe.Reports.Reader.ReadAsync(timeout.Token);

            Assert.Equal(ExecType.REJECTED,rejected.ExecType.Value); Assert.Equal(OrdStatus.REJECTED,rejected.OrdStatus.Value);

            Assert.Equal(0,rejected.LeavesQty.Value); Assert.Contains("Limite",rejected.Text.Value);

            await using var db=fixture.Accumulator();

            Assert.Equal(99998000.01m,(await db.Exposures.SingleAsync(x => x.AccountId == "CLIENTE-001" && x.Symbol =="PETR4")).Amount);

            Assert.Equal(2,await db.Orders.CountAsync());
        }
        finally 
        {
            initiator.Stop();
            acceptor.Stop();
        }
    }

    private static SessionSettings Settings(string role,string root,int port)
    {
        var initiator=role=="initiator";

        var config=$"""
        [DEFAULT]
        ConnectionType={role}
        NonStopSession=Y
        FileStorePath={Path.Combine(root,role)}
        UseDataDictionary=Y
        DataDictionary={Path.Combine(AppContext.BaseDirectory,"config","FIX44.xml")}
        ReconnectInterval=1
        HeartBtInt=30
        ResetOnLogon=N
        [SESSION]
        BeginString=FIX.4.4
        SenderCompID={(initiator?"PROBE":"TESTACC")}
        TargetCompID={(initiator?"TESTACC":"PROBE")}
        {(initiator?"SocketConnectHost=127.0.0.1\nSocketConnectPort=":"SocketAcceptPort=")}{port}
        """;

        return new SessionSettings(new StringReader(config));
    }

    private sealed class Probe() : FixApplicationBase(NullLogger.Instance)
    {
        public Channel<ExecutionReport> Reports { get; }=Channel.CreateUnbounded<ExecutionReport>();

        public void OnMessage(ExecutionReport report,SessionID session)=>Reports.Writer.TryWrite(report);

        public void Send(OrderRequest request)=>Session.SendToTarget(FixMapper.ToMessage(request,DateTime.UtcNow),SessionId!);
    }
}
