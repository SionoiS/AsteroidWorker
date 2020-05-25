using Google.Cloud.Firestore;

namespace AsteroidWorker
{
    internal static class CloudFirestoreInfo
    {
        //Try having names with less than 8 characters to save space.

        internal const string GoogleCredentialFile = "RogueFleetOnline-f755aa7ec387.json";

        internal const string FirebaseProjectId = "roguefleetonline";

        internal static FirestoreDb Database;

        internal const string ResourcesCollection = "ress";
        internal const string AsteroidsCollection = "rocks";

        internal const string CoordinatesField = "coords";
        internal const string QuantityField = "counts";
        
    }
}
