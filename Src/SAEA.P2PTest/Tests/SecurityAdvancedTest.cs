using System;
using System.Text;
using SAEA.P2P.Common;
using SAEA.P2P.Security;

namespace SAEA.P2PTest.Tests
{
    /// <summary>
    /// 安全相关进阶测试：加解密透传/往返、认证挑战、密钥交换。
    /// </summary>
    public static class SecurityAdvancedTest
    {
        public static void Run()
        {
            TestHarness.Section("SecurityAdvancedTest");

            CryptoDisabledPassthrough();
            CryptoSetKeyToggles();
            CryptoRoundTrip();
            CryptoDifferentKeys();
            CryptoWrongKeyFails();
            AuthChallengeEdges();
            AuthManagerEdges();
            KeyExchangeEdges();

            TestHarness.WriteSummary("SecurityAdvancedTest");
        }

        static void CryptoDisabledPassthrough()
        {
            TestHarness.Section("crypto disabled passthrough");

            var crypto = new CryptoService();
            TestHarness.Expect(!crypto.IsEnabled, "default service is disabled");

            var data = new byte[] { 1, 2, 3 };
            TestHarness.Expect(ReferenceEquals(crypto.Encrypt(data), data), "disabled Encrypt returns same reference");
            TestHarness.Expect(ReferenceEquals(crypto.Decrypt(data), data), "disabled Decrypt returns same reference");
            TestHarness.Expect(crypto.EncryptString("abc") == "abc", "disabled EncryptString passthrough");
            TestHarness.Expect(crypto.DecryptString("abc") == "abc", "disabled DecryptString passthrough");
        }

        static void CryptoSetKeyToggles()
        {
            TestHarness.Section("crypto SetKey toggles");

            var crypto = new CryptoService();
            crypto.SetKey("0123456789abcdef");
            TestHarness.Expect(crypto.IsEnabled, "SetKey with value enables");
            crypto.SetKey("");
            TestHarness.Expect(!crypto.IsEnabled, "SetKey empty disables");
            crypto.SetKey(null);
            TestHarness.Expect(!crypto.IsEnabled, "SetKey null disables");
        }

        static void CryptoRoundTrip()
        {
            TestHarness.Section("crypto round trip");

            var crypto = new CryptoService("0123456789abcdef0123456789abcdef");
            var text = "hello 世界 🚀";

            var encrypted = crypto.EncryptString(text);
            TestHarness.Expect(!string.IsNullOrEmpty(encrypted) && encrypted != text, "ciphertext differs from plaintext");
            TestHarness.Expect(crypto.DecryptString(encrypted) == text, "string round trip");

            var bytes = Encoding.UTF8.GetBytes(text);
            var decrypted = crypto.Decrypt(crypto.Encrypt(bytes));
            TestHarness.Expect(Encoding.UTF8.GetString(decrypted) == text, "byte round trip");
        }

        static void CryptoDifferentKeys()
        {
            TestHarness.Section("crypto different keys");

            var a = new CryptoService("0123456789abcdef");
            var b = new CryptoService("fedcba9876543210");
            TestHarness.Expect(a.EncryptString("data") != b.EncryptString("data"), "different keys produce different ciphertext");
        }

        static void CryptoWrongKeyFails()
        {
            TestHarness.Section("crypto wrong key");

            var a = new CryptoService("0123456789abcdef");
            var b = new CryptoService("zzzzzzzzzzzzzzzz");
            var encrypted = a.EncryptString("secret-value");

            string result = null;
            bool threw = false;
            try { result = b.DecryptString(encrypted); }
            catch { threw = true; }

            TestHarness.Expect(threw || result != "secret-value", "wrong key cannot recover plaintext");
        }

        static void AuthChallengeEdges()
        {
            TestHarness.Section("AuthChallenge edges");

            var c1 = AuthChallenge.Create();
            var c2 = AuthChallenge.Create();
            TestHarness.Expect(c1.ChallengeId != c2.ChallengeId, "challenge ids are unique");
            TestHarness.Expect(c1.ChallengeData != c2.ChallengeData, "challenge data is unique");
            TestHarness.Expect(!c1.IsExpired, "fresh challenge not expired");

            var old = AuthChallenge.Create();
            old.CreatedTime = DateTime.UtcNow.AddSeconds(-60);
            TestHarness.Expect(old.IsExpired, "old challenge is expired");
        }

        static void AuthManagerEdges()
        {
            TestHarness.Section("AuthManager edges");

            var manager = new AuthManager("password");
            var challenge = AuthChallenge.Create();

            var response = manager.ComputeResponse(challenge);
            TestHarness.Expect(response == manager.ComputeResponse(challenge), "response is deterministic");
            TestHarness.Expect(response.Length == 64, "response is 64-char sha256 hex", response.Length.ToString());
            TestHarness.Expect(manager.VerifyResponse(challenge, response), "VerifyResponse accepts valid response");
            TestHarness.Expect(!manager.VerifyResponse(challenge, "wrong"), "VerifyResponse rejects wrong response");

            var other = new AuthManager("different");
            TestHarness.Expect(other.ComputeResponse(challenge) != response, "password affects response");

            var expired = AuthChallenge.Create();
            expired.CreatedTime = DateTime.UtcNow.AddSeconds(-60);
            TestHarness.Throws<P2PException>(() => manager.ComputeResponse(expired), "expired challenge throws");
            TestHarness.Throws<P2PException>(() => manager.ComputeResponse(null), "null challenge throws");
        }

        static void KeyExchangeEdges()
        {
            TestHarness.Section("KeyExchange edges");

            var k1 = KeyExchange.Create();
            var k2 = KeyExchange.Create();
            TestHarness.Expect(k1.SessionKey != k2.SessionKey, "session keys are unique");
            TestHarness.Expect(k1.SessionKey.Length == 32, "session key is 16-byte hex", k1.SessionKey.Length.ToString());
            TestHarness.Expect(k1.IsActive, "key active on creation");

            k1.Deactivate();
            TestHarness.Expect(!k1.IsActive, "Deactivate clears active flag");

            var shared = KeyExchange.FromSharedKey("shared-123");
            TestHarness.Expect(shared.SessionKey == "shared-123", "FromSharedKey uses provided key");
            TestHarness.Expect(shared.IsActive, "FromSharedKey key is active");
        }
    }
}