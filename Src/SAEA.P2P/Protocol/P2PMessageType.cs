namespace SAEA.P2P.Protocol
{
    public enum P2PMessageType : byte
    {
        Register = 0x10,
        RegisterAck = 0x11,
        NodeList = 0x12,
        NatProbe = 0x20,
        NatProbeAck = 0x21,
        PunchRequest = 0x30,
        PunchSync = 0x31,
        PunchAck = 0x32,
        PunchReady = 0x33,
        RelayRequest = 0x40,
        RelayData = 0x41,
        RelayAck = 0x42,
        LocalDiscover = 0x50,
        LocalDiscoverAck = 0x51,
        AuthChallenge = 0x60,
        AuthResponse = 0x61,
        AuthSuccess = 0x62,
        Heartbeat = 0x70,
        HeartbeatAck = 0x71,
        UserData = 0x80
    }
}