using System;

namespace SAEA.P2P.Security
{
    public class AuthManager
    {
        private string _nodeIdPassword;
        private CryptoService _cryptoService;

        public AuthManager(string nodeIdPassword, CryptoService cryptoService = null)
        {
            _nodeIdPassword = nodeIdPassword;
            _cryptoService = cryptoService;
        }

        public string ComputeResponse(AuthChallenge challenge)
        {
            if (challenge == null || challenge.IsExpired)
                throw new Common.P2PException(Common.ErrorCode.AuthFailed, "Challenge expired or null");

            var input = challenge.ChallengeData + _nodeIdPassword;
            return ComputeHash(input);
        }

        public bool VerifyResponse(AuthChallenge challenge, string response)
        {
            var expected = ComputeResponse(challenge);
            return expected == response;
        }

        private string ComputeHash(string input)
        {
            using (var sha256 = System.Security.Cryptography.SHA256.Create())
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(input);
                var hash = sha256.ComputeHash(bytes);
                return BitConverter.ToString(hash).Replace("-", "").ToLower();
            }
        }
    }
}