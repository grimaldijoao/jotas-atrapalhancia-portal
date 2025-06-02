using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class FirebaseAuthHandler
    {
        public FirebaseAuthHandler()
        {
            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromFile("renejotas-731e3d8bca97.json")
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
