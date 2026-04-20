namespace SAEA.P2P.NAT
{
    public class HolePunchResult
    {
        public bool Success { get; set; }
        public NATType SourceNATType { get; set; }
        public NATType TargetNATType { get; set; }
        public System.Net.IPEndPoint EstablishedAddress { get; set; }
        public string ErrorMessage { get; set; }
        public int Attempts { get; set; }
        
        public static HolePunchResult Succeeded(System.Net.IPEndPoint address, NATType sourceNat, NATType targetNat, int attempts)
        {
            return new HolePunchResult
            {
                Success = true,
                EstablishedAddress = address,
                SourceNATType = sourceNat,
                TargetNATType = targetNat,
                Attempts = attempts
            };
        }
        
        public static HolePunchResult Failed(string error, NATType sourceNat, NATType targetNat)
        {
            return new HolePunchResult
            {
                Success = false,
                ErrorMessage = error,
                SourceNATType = sourceNat,
                TargetNATType = targetNat
            };
        }
    }
}