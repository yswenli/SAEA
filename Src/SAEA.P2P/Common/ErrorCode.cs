namespace SAEA.P2P.Common
{
    public static class ErrorCode
    {
        public static readonly string RegisterPeerIdMissing = "EP01";
        public static readonly string RegisterPeerIdInvalid = "EP02";
        public static readonly string RegisterPeerIdDuplicate = "EP03";
        public static readonly string RegisterNATInfoMissing = "EP04";
        public static readonly string RegisterTimeout = "EP05";
        public static readonly string RegisterFailed = "EP06";
        public static readonly string RegisterServerUnavailable = "EP07";
        public static readonly string RegisterAuthFailed = "EP08";
        public static readonly string RegisterRejected = "EP09";

        public static readonly string DiscoveryNoResponse = "ED01";
        public static readonly string DiscoveryTimeout = "ED02";
        public static readonly string DiscoveryNoCandidates = "ED03";
        public static readonly string DiscoveryNatDetectionFailed = "ED04";
        public static readonly string DiscoveryAddressInvalid = "ED05";
        public static readonly string DiscoveryPortBlocked = "ED06";
        public static readonly string DiscoveryFailed = "ED07";

        public static readonly string PunchFailed = "EO01";
        public static readonly string PunchTimeout = "EO02";
        public static readonly string PunchNoRoute = "EO03";

        public static readonly string HolePunchFailed = "EH01";
        public static readonly string HolePunchTimeout = "EH02";

        public static readonly string RelayFailed = "ER01";
        public static readonly string RelayServerUnavailable = "ER02";
        public static readonly string RelaySessionNotFound = "ER03";
        public static readonly string RelayQuotaExceeded = "ER04";

        public static readonly string AuthFailed = "EA01";
        public static readonly string AuthTimeout = "EA03";

        public static readonly string EncryptionFailed = "EE01";
        public static readonly string DecryptionFailed = "EE02";

        private static readonly System.Collections.Generic.Dictionary<string, string> Descriptions =
            new System.Collections.Generic.Dictionary<string, string>
            {
                { RegisterPeerIdMissing, "Peer ID missing in registration request" },
                { RegisterPeerIdInvalid, "Peer ID invalid" },
                { RegisterPeerIdDuplicate, "Peer ID already registered" },
                { RegisterNATInfoMissing, "NAT info missing in registration request" },
                { RegisterTimeout, "Registration timeout" },
                { RegisterFailed, "Registration failed" },
                { RegisterServerUnavailable, "Registration server unavailable" },
                { RegisterAuthFailed, "Registration authentication failed" },
                { RegisterRejected, "Registration rejected" },
                { DiscoveryNoResponse, "Discovery no response" },
                { DiscoveryTimeout, "Discovery timeout" },
                { DiscoveryNoCandidates, "Discovery no candidates found" },
                { DiscoveryNatDetectionFailed, "NAT detection failed" },
                { DiscoveryAddressInvalid, "Discovery address invalid" },
                { DiscoveryPortBlocked, "Discovery port blocked" },
                { DiscoveryFailed, "Discovery failed" },
                { PunchFailed, "Hole punch failed" },
                { PunchTimeout, "Hole punch timeout" },
                { PunchNoRoute, "No route to peer" },
                { HolePunchFailed, "Hole punch failed" },
                { HolePunchTimeout, "Hole punch timeout" },
                { RelayFailed, "Relay connection failed" },
                { RelayServerUnavailable, "Relay server unavailable" },
                { RelaySessionNotFound, "Relay session not found" },
                { RelayQuotaExceeded, "Relay quota exceeded" },
                { AuthFailed, "Authentication failed" },
                { AuthTimeout, "Authentication timeout" },
                { EncryptionFailed, "Encryption failed" },
                { DecryptionFailed, "Decryption failed" }
            };

        public static string GetDescription(string errorCode)
        {
            if (Descriptions.TryGetValue(errorCode, out var description))
            {
                return description;
            }
            return "Unknown error";
        }
    }
}