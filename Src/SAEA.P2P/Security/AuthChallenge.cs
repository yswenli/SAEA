using System;

namespace SAEA.P2P.Security
{
    public class AuthChallenge
    {
        public string ChallengeId { get; set; }
        public string ChallengeData { get; set; }
        public DateTime CreatedTime { get; set; }
        public int TimeoutMs { get; set; } = 30000;

        public static AuthChallenge Create()
        {
            return new AuthChallenge
            {
                ChallengeId = Guid.NewGuid().ToString("N"),
                ChallengeData = Guid.NewGuid().ToString("N"),
                CreatedTime = DateTime.UtcNow
            };
        }

        public bool IsExpired => (DateTime.UtcNow - CreatedTime).TotalMilliseconds > TimeoutMs;
    }
}