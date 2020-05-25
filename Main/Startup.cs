using AsteroidWorker.ECS;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Firestore;
using Google.Cloud.Firestore.V1;
using Grpc.Auth;
using Grpc.Core;
using Improbable.Worker;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace AsteroidWorker
{
    static class Startup
    {
        const string WorkerType = "asteroid_worker";

        static readonly Stopwatch stopwatch = new Stopwatch();

        static int Main(string[] args)
        {
            stopwatch.Restart();

            if (args.Length != 4)
            {
                PrintUsage();
                return 1;
            }

            Assembly.Load("GeneratedCode");

            using (var connection = ConnectWithReceptionist(args[1], Convert.ToUInt16(args[2]), args[3]))
            {
                var channel = new Channel(FirestoreClient.DefaultEndpoint.Host, FirestoreClient.DefaultEndpoint.Port, GoogleCredential.FromFile(Path.Combine(Directory.GetCurrentDirectory(), CloudFirestoreInfo.GoogleCredentialFile)).ToChannelCredentials());
                var task = FirestoreDb.CreateAsync(CloudFirestoreInfo.FirebaseProjectId, FirestoreClient.Create(channel));

                var connected = true;
                SpatialOSConnectionSystem.connection = connection;

                var dispatcher = new Dispatcher();
                
                dispatcher.OnDisconnect(op =>
                {
                    Console.Error.WriteLine("[disconnect] " + op.Reason);
                    connected = false;
                });

                CloudFirestoreInfo.Database = task.Result;

                var factory = new TaskFactory(TaskCreationOptions.LongRunning, TaskContinuationOptions.None);

                factory.StartNew(ResourcesInventorySystem.UpdateLoop);
                factory.StartNew(ResourceGeneratorSystem.UpdateLoop);
                factory.StartNew(ResourceExtractorSystem.UpdateLoop);

                stopwatch.Stop();
                    
                connection.SendLogMessage(LogLevel.Info, "Initialization", string.Format("Init Time {0}ms", stopwatch.Elapsed.TotalMilliseconds.ToString("N0")));

                while (connected)
                {
                    stopwatch.Restart();

                    using (var opList = connection.GetOpList(100))
                    {
                        Parallel.Invoke(
                            () => dispatcher.Process(opList),
                            () => PositionsSystem.ProccessOpList(opList),
                            () => IdentificationsSystem.ProccessOpList(opList),
                            () => ResourcesInventorySystem.ProccessOpList(opList),
                            () => ResourceGeneratorSystem.ProccessOpList(opList),
                            () => ResourceExtractorSystem.ProccessOpList(opList)
                        );
                    }

                    SpatialOSConnectionSystem.Update();

                    stopwatch.Stop();
                }
                
                SpatialOSConnectionSystem.connection = null;

                channel.ShutdownAsync().Wait();
            }

            return 1;
        }

        static Connection ConnectWithReceptionist(string hostname, ushort port, string workerId)
        {
            var connectionParameters = new ConnectionParameters
            {
                EnableDynamicComponents = true,
                WorkerType = WorkerType,
                Network =
                {
                    ConnectionType = NetworkConnectionType.Tcp,
                    UseExternalIp = false,
                }
            };

            Connection connection;

            using (var future = Connection.ConnectAsync(hostname, port, workerId, connectionParameters))
            {
                connection = future.Get();
            }

            return connection;
        }

        static void PrintUsage()
        {
            Console.WriteLine("Usage: mono ShipWorker.exe receptionist <hostname> <port> <worker_id>");
            Console.WriteLine("Connects to SpatialOS");
            Console.WriteLine("    <hostname>      - hostname of the receptionist to connect to.");
            Console.WriteLine("    <port>          - port to use");
            Console.WriteLine("    <worker_id>     - name of the worker assigned by SpatialOS.");
        }
    }
}