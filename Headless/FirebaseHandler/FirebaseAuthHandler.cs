using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Headless.Shared.Interfaces;

namespace Headless.Shared
{
    public class FirebaseAuthHandler : ITokenDecoder
    {
        public FirebaseAuthHandler()
        {
            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromFile(Path.Combine("Headless", "FirebaseHandler", "renejotas-731e3d8bca97.json"))
            });
        }

        public Dictionary<string, string> DecodeToken(string idToken)
        {
            var decodeResult = FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(idToken)
                .GetAwaiter()
                .GetResult();

            return decodeResult.Claims.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value?.ToString() ?? string.Empty
            );
        }
    }
}
