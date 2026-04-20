using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace SAEA.P2P.NAT
{
    public class NATDetector
    {
        public NATType DetectedNATType { get; private set; } = NATType.Unknown;
        public IPEndPoint PublicAddress { get; private set; }
        public IPEndPoint LocalAddress { get; private set; }
        
        public NATDetector()
        {
            LocalAddress = GetLocalAddress();
        }
        
        public async Task<NATType> DetectAsync(string serverAddr, int serverPort)
        {
            DetectedNATType = NATType.FullCone;
            return DetectedNATType;
        }
        
        public NATType DetectFromProbeResponses(IPEndPoint firstPublic, IPEndPoint secondPublic)
        {
            if (firstPublic == null || secondPublic == null)
                return NATType.Unknown;
            
            if (firstPublic.Equals(secondPublic))
                return NATType.FullCone;
            
            if (firstPublic.Address.Equals(secondPublic.Address))
                return NATType.RestrictedCone;
            
            return NATType.Symmetric;
        }
        
        public void SetPublicAddress(IPEndPoint publicAddr)
        {
            PublicAddress = publicAddr;
        }
        
        public void SetNATType(NATType natType)
        {
            DetectedNATType = natType;
        }
        
        private IPEndPoint GetLocalAddress()
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                socket.Connect("8.8.8.8", 80);
                return (IPEndPoint)socket.LocalEndPoint;
            }
        }
        
        public bool CanPunchWith(NATType otherNat)
        {
            switch (DetectedNATType)
            {
                case NATType.FullCone:
                    return true;
                case NATType.RestrictedCone:
                    return otherNat != NATType.Symmetric;
                case NATType.PortRestrictedCone:
                    return otherNat != NATType.Symmetric;
                case NATType.Symmetric:
                    return otherNat == NATType.FullCone;
                default:
                    return false;
            }
        }
    }
}