using System;
using System.Text;
using System.Threading.Tasks;
using SAEA.Common;
using SAEA.P2P.Security;
using SAEA.P2P.Common;

namespace SAEA.P2PTest.Tests
{
    public static class AuthEncryptionTest
    {
        public static async Task RunAsync()
        {
            ConsoleHelper.WriteLine("=== AuthEncryptionTest ===");
            ConsoleHelper.WriteLine("");

            TestCryptoServiceCreation();
            ConsoleHelper.WriteLine("");

            TestCryptoServiceEncryptDecrypt();
            ConsoleHelper.WriteLine("");

            TestCryptoServiceDisabled();
            ConsoleHelper.WriteLine("");

            TestAuthManagerCreation();
            ConsoleHelper.WriteLine("");

            TestAuthChallengeResponse();
            ConsoleHelper.WriteLine("");

            TestKeyExchange();
            ConsoleHelper.WriteLine("");

            ConsoleHelper.WriteLine("=== AuthEncryptionTest Complete ===");
        }

        static void TestCryptoServiceCreation()
        {
            ConsoleHelper.WriteLine("--- TestCryptoServiceCreation ---");

            var crypto1 = new CryptoService();
            ConsoleHelper.WriteLine($"Without key: IsEnabled={crypto1.IsEnabled}");

            var crypto2 = new CryptoService("aes-test-key-16");
            ConsoleHelper.WriteLine($"With 16-byte key: IsEnabled={crypto2.IsEnabled}");

            crypto1.SetKey("aes-test-key-24");
            ConsoleHelper.WriteLine($"After SetKey (24): IsEnabled={crypto1.IsEnabled}");

            if (!crypto1.IsEnabled && crypto2.IsEnabled)
            {
                ConsoleHelper.WriteLine("TestCryptoServiceCreation: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestCryptoServiceCreation: FAILED");
            }
        }

        static void TestCryptoServiceEncryptDecrypt()
        {
            ConsoleHelper.WriteLine("--- TestCryptoServiceEncryptDecrypt ---");

            var key = "aes-secret-key-16";
            var crypto = new CryptoService(key);

            var originalText = "Hello P2P Encryption!";
            var originalBytes = Encoding.UTF8.GetBytes(originalText);

            ConsoleHelper.WriteLine($"Original: {originalText}");

            var encryptedBytes = crypto.Encrypt(originalBytes);
            ConsoleHelper.WriteLine($"Encrypted length: {encryptedBytes.Length}");
            ConsoleHelper.WriteLine($"Encrypted (base64): {Encoding.UTF8.GetString(encryptedBytes)}");

            var decryptedBytes = crypto.Decrypt(encryptedBytes);
            var decryptedText = Encoding.UTF8.GetString(decryptedBytes);
            ConsoleHelper.WriteLine($"Decrypted: {decryptedText}");

            if (originalText == decryptedText)
            {
                ConsoleHelper.WriteLine("TestCryptoServiceEncryptDecrypt: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestCryptoServiceEncryptDecrypt: FAILED");
            }
        }

        static void TestCryptoServiceDisabled()
        {
            ConsoleHelper.WriteLine("--- TestCryptoServiceDisabled ---");

            var crypto = new CryptoService();

            var data = Encoding.UTF8.GetBytes("No encryption");
            var encrypted = crypto.Encrypt(data);
            var decrypted = crypto.Decrypt(encrypted);

            ConsoleHelper.WriteLine($"IsEnabled: {crypto.IsEnabled}");
            ConsoleHelper.WriteLine($"Original == Encrypted: {data == encrypted}");
            ConsoleHelper.WriteLine($"Encrypted == Decrypted: {encrypted == decrypted}");

            if (!crypto.IsEnabled && data == encrypted && encrypted == decrypted)
            {
                ConsoleHelper.WriteLine("TestCryptoServiceDisabled: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestCryptoServiceDisabled: FAILED");
            }
        }

        static void TestAuthManagerCreation()
        {
            ConsoleHelper.WriteLine("--- TestAuthManagerCreation ---");

            var auth1 = new AuthManager("node-password-123");
            ConsoleHelper.WriteLine("Created AuthManager with password");

            var crypto = new CryptoService("encryption-key-16");
            var auth2 = new AuthManager("node-password-456", crypto);
            ConsoleHelper.WriteLine("Created AuthManager with password and CryptoService");

            ConsoleHelper.WriteLine("TestAuthManagerCreation: PASSED");
        }

        static void TestAuthChallengeResponse()
        {
            ConsoleHelper.WriteLine("--- TestAuthChallengeResponse ---");

            var auth = new AuthManager("test-secret-password");
            var challenge = AuthChallenge.Create();

            ConsoleHelper.WriteLine($"Challenge created: {challenge.ChallengeData}");
            ConsoleHelper.WriteLine($"Challenge expired: {challenge.IsExpired}");

            var response = auth.ComputeResponse(challenge);
            ConsoleHelper.WriteLine($"Computed response: {response}");
            ConsoleHelper.WriteLine($"Response length: {response.Length}");

            var verified = auth.VerifyResponse(challenge, response);
            ConsoleHelper.WriteLine($"Verify response: {verified}");

            var wrongResponse = "wrong-response-hash";
            var wrongVerified = auth.VerifyResponse(challenge, wrongResponse);
            ConsoleHelper.WriteLine($"Verify wrong response: {wrongVerified}");

            if (verified && !wrongVerified)
            {
                ConsoleHelper.WriteLine("TestAuthChallengeResponse: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestAuthChallengeResponse: FAILED");
            }
        }

        static void TestKeyExchange()
        {
            ConsoleHelper.WriteLine("--- TestKeyExchange ---");

            var keyExchange1 = new KeyExchange();
            var keyExchange2 = new KeyExchange();

            ConsoleHelper.WriteLine("Created two KeyExchange instances");

            ConsoleHelper.WriteLine($"SessionKey1: {keyExchange1.SessionKey}");
            ConsoleHelper.WriteLine($"SessionKey2: {keyExchange2.SessionKey}");
            ConsoleHelper.WriteLine($"KeyExchange1.IsActive: {keyExchange1.IsActive}");
            ConsoleHelper.WriteLine($"KeyExchange2.IsActive: {keyExchange2.IsActive}");

            var sharedKeyExchange = KeyExchange.FromSharedKey("shared-secret-key");
            ConsoleHelper.WriteLine($"FromSharedKey: {sharedKeyExchange.SessionKey}");

            if (keyExchange1.IsActive && keyExchange2.IsActive && 
                !string.IsNullOrEmpty(keyExchange1.SessionKey) &&
                sharedKeyExchange.SessionKey == "shared-secret-key")
            {
                ConsoleHelper.WriteLine("TestKeyExchange: PASSED");
            }
            else
            {
                ConsoleHelper.WriteLine("TestKeyExchange: FAILED");
            }
        }
    }
}