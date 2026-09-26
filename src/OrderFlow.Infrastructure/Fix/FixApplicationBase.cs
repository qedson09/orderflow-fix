using Microsoft.Extensions.Logging;
using QuickFix;
using QuickFix.Fields;
namespace OrderFlow.Infrastructure.Fix;

public abstract class FixApplicationBase(ILogger logger) : MessageCracker, IApplication
{
    private int _loggedOn;

    protected SessionID? SessionId { get; private set; }

    public bool IsLoggedOn => Volatile.Read(ref _loggedOn) == 1;

    public void OnCreate(SessionID sessionID) => SessionId = sessionID;

    public void OnLogon(SessionID sessionID) { Volatile.Write(ref _loggedOn, 1); logger.LogInformation("FIX logon {Session}", sessionID); }

    public void OnLogout(SessionID sessionID) { Volatile.Write(ref _loggedOn, 0); logger.LogWarning("FIX logout {Session}", sessionID); }

    public void ToAdmin(QuickFix.Message message, SessionID sessionID) { }

    public void FromAdmin(QuickFix.Message message, SessionID sessionID)
    {
        if (message.Header.GetString(Tags.MsgType) is "3" or "5")
            logger.LogWarning("FIX evento administrativo {Type} na sessão {Session}", message.Header.GetString(Tags.MsgType), sessionID);
    }

    public void ToApp(QuickFix.Message message, SessionID sessionID) { }

    public void FromApp(QuickFix.Message message, SessionID sessionID) => Crack(message, sessionID);
}
