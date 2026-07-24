using Fusion;

public sealed class RoomJoinResult
{
    public bool IsSuccess { get; }
    public string Message { get; }
    public ShutdownReason ShutdownReason { get; }

    private RoomJoinResult(
        bool isSuccess,
        string message,
        ShutdownReason shutdownReason)
    {
        IsSuccess = isSuccess;
        Message = message;
        ShutdownReason = shutdownReason;
    }

    public static RoomJoinResult Success(string message)
    {
        return new RoomJoinResult(true, message, ShutdownReason.Ok);
    }

    public static RoomJoinResult Fail(string message, ShutdownReason shutdownReason)
    {
        return new RoomJoinResult(false, message, shutdownReason);
    }
}
