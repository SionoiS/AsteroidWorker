using Improbable;
using Improbable.Worker;
using ItemGenerator;
using ItemGenerator.Resources;
using RogueFleet.Asteroids;
using RogueFleet.Core;
using RogueFleet.Items;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using Xoshiro.Base;
using Xoshiro.PRNG32;

namespace AsteroidWorker.ECS
{
    public static class ResourceGeneratorSystem
    {
        static readonly Dispatcher dispatcher = new Dispatcher();

        static ResourceGeneratorSystem()
        {
            dispatcher.OnCommandRequest(Harvestable.Commands.GenerateResource.Metaclass, OnGenerateResourceRequest);
        }

        internal static void ProccessOpList(OpList opList)
        {
            dispatcher.Process(opList);
        }

        static readonly ConcurrentQueue<CommandRequestOp<Harvestable.Commands.GenerateResource, ResourceGenerationRequest>> generateResourceRequestOps = new ConcurrentQueue<CommandRequestOp<Harvestable.Commands.GenerateResource, ResourceGenerationRequest>>();

        static void OnGenerateResourceRequest(CommandRequestOp<Harvestable.Commands.GenerateResource, ResourceGenerationRequest> op)
        {
            generateResourceRequestOps.Enqueue(op);
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

        static readonly DateTime centuryBegin = new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc);


        static void Update()
        {
            ProccessGenerateResourceOps();
        }

        static void ProccessGenerateResourceOps()
        {
            while (generateResourceRequestOps.TryDequeue(out var op))
            {
                var asteroidEntityId = op.EntityId.Id;
                
                var response = new ResourceGenerationReponse();

                if (ResourcesInventorySystem.TryGetResourceInfo(asteroidEntityId, 0, out var resourceInfo))
                {
                    response.databaseId = resourceInfo.databaseId;
                    response.type = resourceInfo.type;
                    response.quantity = resourceInfo.quantity;
                }
                else
                {
                    var position = PositionsSystem.GetComponent(asteroidEntityId).coords;

                    var time = (long)(DateTime.Now.ToUniversalTime() - centuryBegin).TotalSeconds;

                    var lowSample = ProbabilityMap.EvaluateLowSample(position.x, position.y, position.z, time);
                    var medSample = ProbabilityMap.EvaluateMedSample(position.x, position.y, position.z, time);
                    var highSample = ProbabilityMap.EvaluateHighSample(position.x, position.y, position.z, time);

                    var sample = ProbabilityMap.LayeringSample(lowSample, medSample, highSample);

                    var seed = asteroidEntityId ^ BitConverter.ToInt64(Encoding.UTF8.GetBytes(op.Request.userDatabaseId), 0);
                    IRandomU prng = new XoShiRo128starstar(seed);

                    var scanner = op.Request.scanner;

                    var quantity = QuantityGenerator.Sample(sample, prng, scanner);

                    if (quantity > 0)
                    {
                        response.quantity = quantity;

                        var scannerResource = scanner.speciality;
                        if (scannerResource == ResourceType.Random)
                        {
                            scannerResource = (ResourceType)prng.Next(1, (int)ResourceType.Count);
                        }

                        response.type = scannerResource;

                        var resourceDBId = Helpers.GenerateCloudFireStoreRandomDocumentId(prng);
                        var asteroidDBId = Helpers.GenerateCloudFireStoreRandomDocumentId(prng);

                        response.databaseId = resourceDBId;

                        SpatialOSConnectionSystem.addPersistenceOps.Enqueue(
                            new AddComponentOp<PersistenceData>
                            {
                                EntityId = op.EntityId,
                                Data = new PersistenceData()
                            });

                        var addComponentOp = new AddComponentOp<IdentificationData>
                        {
                            EntityId = op.EntityId,
                            Data = new IdentificationData
                            {
                                entityDatabaseId = asteroidDBId,
                            }
                        };

                        IdentificationsSystem.OnComponentAdded(addComponentOp);
                        SpatialOSConnectionSystem.addIdentificationOps.Enqueue(addComponentOp);

                        ResourcesInventorySystem.QueueAddResourceOp(asteroidEntityId, resourceInfo.databaseId, new Resource(resourceInfo.type, resourceInfo.quantity));

                        var asteroidRef = CloudFirestoreInfo.Database.Collection(CloudFirestoreInfo.AsteroidsCollection).Document(asteroidDBId);
                        asteroidRef.CreateAsync(new Dictionary<string, object> { { CloudFirestoreInfo.CoordinatesField, new double[3] { position.x, position.y, position.z } } });
                    }
                }

                SpatialOSConnectionSystem.responseGenerateResourceOps.Enqueue(
                    new CommandResponseOp<Harvestable.Commands.GenerateResource, ResourceGenerationReponse>//just using CommandResponseOp as container for request raw id and response
                    {
                        EntityId = new EntityId(),
                        RequestId = new RequestId<OutgoingCommandRequest<Harvestable.Commands.GenerateResource>>(op.RequestId.Id),
                        Response = response,
                    });
            }
        }
    }
}
