namespace SAEA.P2P.Common
{
    public enum NodeState
    {
        Init = 0,
        Connecting = 1,
        Authenticating = 2,
        Registered = 3,
        HolePunching = 4,
        Relaying = 5,
        Connected = 6,
        Idle = 7,
        Disconnected = 8,
        Error = 9
    }
}