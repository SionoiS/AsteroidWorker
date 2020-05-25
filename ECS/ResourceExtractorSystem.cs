using Improbable.Worker;
using RogueFleet.Asteroids;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AsteroidWorker.ECS
{
    public static class ResourceExtractorSystem
    {
        static readonly Dispatcher dispatcher = new Dispatcher();

        static ResourceExtractorSystem()
        {
            dispatcher.OnCommandRequest(Harvestable.Commands.ExtractResource.Metaclass, OnExtractResourceRequest);
        }

        internal static void ProccessOpList(OpList opList)
        {
            dispatcher.Process(opList);
        }

        static readonly ConcurrentQueue<CommandRequestOp<Harvestable.Commands.ExtractResource, ResourceExtractionRequest>> extractResourceRequestOps = new ConcurrentQueue<CommandRequestOp<Harvestable.Commands.ExtractResource, ResourceExtractionRequest>>();

        static void OnExtractResourceRequest(CommandRequestOp<Harvestable.Commands.ExtractResource, ResourceExtractionRequest> op)
        {
            extractResourceRequestOps.Enqueue(op);
        }

        static TimeSpan frameRate = TimeSpan.FromMilliseconds(100);
        static readonly Stopwatch stopwatch = new Stopwatch();

        internal static void UpdateLoop()
        {
            while (true)
            {
                stopwatch.Restart();
                Update();
                stopwatch.Stop();

                var frameTime = frameRate - stopwatch.Elapsed;
                if (frameTime > TimeSpan.Zero)
                {
                    Task.Delay(frameTime).Wait();
                }
                else
                {
                    //connection.SendLogMessage(LogLevel.Warn, "Game Loop", string.Format("Frame Time {0}ms", frameTime.TotalMilliseconds.ToString("N0")));
                }
            }
        }

        static void Update()
        {
            ProcessExtractResourceOps();
        }

        static void ProcessExtractResourceOps()
        {
            while (extractResourceRequestOps.TryDequeue(out var op))
            {
                var commandRequest = op.Request;
                var asteroidEntityId = op.EntityId.Id;

                var response = new ResourceExtractionReponse();

                if (ResourcesInventorySystem.TryGetResourceInfo(asteroidEntityId, 0, out var resourceInfo))
                {
                    int extractionRate = commandRequest.extractRate;//flat amount
                    if (extractionRate < 0)//percent amount
                    {
                        extractionRate = (int)(-1.0d / extractionRate * resourceInfo.quantity);
                    }

                    int actualAmountExtracted = extractionRate;

                    ResourcesInventorySystem.QueueResourceQuantityDeltaOp(asteroidEntityId, 0, -actualAmountExtracted);

                    if (resourceInfo.quantity <= extractionRate)
                    {
                        actualAmountExtracted = resourceInfo.quantity;

                        SpatialOSConnectionSystem.deleteEntityOps.Enqueue(new EntityId(asteroidEntityId));

                        var asteroidRef = CloudFirestoreInfo.Database.Collection(CloudFirestoreInfo.AsteroidsCollection).Document(IdentificationsSystem.GetEntityDBId(asteroidEntityId));
                        asteroidRef.DeleteAsync();
                    }

                    response.databaseId = resourceInfo.databaseId;
                    response.quantity = actualAmountExtracted;
                    response.type = resourceInfo.type;
                }

                SpatialOSConnectionSystem.responseExtractResourceOps.Enqueue(
                    new CommandResponseOp<Harvestable.Commands.ExtractResource, ResourceExtractionReponse>
                    {
                        EntityId = new EntityId(asteroidEntityId),
                        RequestId = new RequestId<OutgoingCommandRequest<Harvestable.Commands.ExtractResource>>(op.RequestId.Id),
                        Response = response,
                    });
            }
        }
    }
}
