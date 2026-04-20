namespace SAEA.P2P.Protocol
{
    public enum P2PMessageType : byte
    {
        Register = 0x10,
        NatProbe = 0x20,
        PunchRequest = 0x30,
        PunchSync = 0x31,
        PunchAck = 0x32,
        RelayRequest = 0x40,
        LocalDiscover = 0x50,
        AuthChallenge = 0x60,
        Heartbeat = 0x70,
        UserData = 0x80
    }
}