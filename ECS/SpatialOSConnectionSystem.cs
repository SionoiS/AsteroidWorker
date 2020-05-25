using Improbable;
using Improbable.Worker;
using RogueFleet.Asteroids;
using RogueFleet.Core;
using RogueFleet.Items;
using System.Collections.Concurrent;

namespace AsteroidWorker.ECS
{
    internal readonly struct LogMessage
    {
        internal readonly LogLevel logLevel;
        internal readonly string logger;
        internal readonly string message;

        internal LogMessage(LogLevel logLevel, string logger, string message)
        {
            this.logLevel = logLevel;
            this.logger = logger;
            this.message = message;
        }
    }

    static class SpatialOSConnectionSystem
    {
        internal static Connection connection;

        static readonly UpdateParameters updateParameterNoLoopback = new UpdateParameters() { Loopback = ComponentUpdateLoopback.None };
        //static readonly UpdateParameters updateParameterLoopback = new UpdateParameters() { Loopback = ComponentUpdateLoopback.ShortCircuited };

        //static readonly CommandParameters commandParameterShortcircuit = new CommandParameters() { AllowShortCircuit = true };
        //static readonly CommandParameters commandParameterNoShortCircuit = new CommandParameters() { AllowShortCircuit = false };

        internal static void Update()
        {
            SendAddComponents();
            SendComponentUpdates();

            SendCommandRequests();
            SendCommandResponses();

            SendLogMessages();
        }

        #region AddComponents
        internal static ConcurrentQueue<AddComponentOp<PersistenceData>> addPersistenceOps = new ConcurrentQueue<AddComponentOp<PersistenceData>>();
        internal static ConcurrentQueue<AddComponentOp<ResourceInventoryData>> addResourceInventoryOps = new ConcurrentQueue<AddComponentOp<ResourceInventoryData>>();
        internal static ConcurrentQueue<AddComponentOp<IdentificationData>> addIdentificationOps = new ConcurrentQueue<AddComponentOp<IdentificationData>>();

        static void SendAddComponents()
        {
            ProccessAdds(Persistence.Metaclass, ref addPersistenceOps);
            ProccessAdds(ResourceInventory.Metaclass, ref addResourceInventoryOps);
            ProccessAdds(Identification.Metaclass, ref addIdentificationOps);
        }
        static void ProccessAdds<TComponent, TData, TUpdate>(IComponentMetaclass<TComponent, TData, TUpdate> metaclass, ref ConcurrentQueue<AddComponentOp<TData>> addOps) where TComponent : IComponentMetaclass
        {
            while (addOps.Count > 0)
            {
                if (addOps.TryDequeue(out var op))
                {
                    connection.SendAddComponent(metaclass, op.EntityId, op.Data, updateParameterNoLoopback);
                }
            }
        }
        #endregion

        #region Updates
        internal static ConcurrentQueue<ComponentUpdateOp<ResourceInventory.Update>> updateResourceInventoryOps = new ConcurrentQueue<ComponentUpdateOp<ResourceInventory.Update>>();
        //internal static ConcurrentQueue<ComponentUpdateOp<Damageable.Update>> updateDamageableOps = new ConcurrentQueue<ComponentUpdateOp<Damageable.Update>>();

        static void SendComponentUpdates()
        {
            ProccessUpdates(ResourceInventory.Metaclass, ref updateResourceInventoryOps);
            //ProccessUpdates(Damageable.Metaclass, ref updateDamageableOps);
        }

        static void ProccessUpdates<TComponent, TData, TUpdate>(IComponentMetaclass<TComponent, TData, TUpdate> metaclass, ref ConcurrentQueue<ComponentUpdateOp<TUpdate>> updateOps) where TComponent : IComponentMetaclass
        {
            while (updateOps.Count > 0)
            {
                if (updateOps.TryDequeue(out var op))
                {
                    connection.SendComponentUpdate(metaclass, op.EntityId, op.Update, updateParameterNoLoopback);
                }
            }
        }
        #endregion

        #region Command Requests
        //internal static ConcurrentQueue<CommandRequestOp<AsteroidSpawner.Commands.PopulateGridCell, PopulateGridCellRequest>> requestPopulateGridCellOps = new ConcurrentQueue<CommandRequestOp<AsteroidSpawner.Commands.PopulateGridCell, PopulateGridCellRequest>>();
        
        internal static ConcurrentQueue<EntityId> deleteEntityOps = new ConcurrentQueue<EntityId>();

        static void SendCommandRequests()
        {
            //ProccessRequests(AsteroidSpawner.Commands.PopulateGridCell.Metaclass, ref requestPopulateGridCellOps);

            while (deleteEntityOps.Count > 0)
            {
                if (deleteEntityOps.TryDequeue(out var op))
                {
                    connection.SendDeleteEntityRequest(op, null);
                }
            }
        }

        /*static void ProccessRequests<TCommand, TRequest, TResponse>(ICommandMetaclass<TCommand, TRequest, TResponse> metaclass, ref ConcurrentQueue<CommandRequestOp<TCommand, TRequest>> requestOps, ref ConcurrentDictionary<RequestId<OutgoingCommandRequest<TCommand>>, EntityId> requestIds) where TCommand : ICommandMetaclass
        {
            while (requestOps.Count > 0)
            {
                if (requestOps.TryDequeue(out var op))
                {
                    var id = connection.SendCommandRequest(metaclass, op.EntityId, op.Request, null, commandParameterNoShortCircuit);

                    requestIds[id] = op.EntityId;
                }
            }
        }*/

        /*static void ProccessRequests<TCommand, TRequest, TResponse>(ICommandMetaclass<TCommand, TRequest, TResponse> metaclass, ref ConcurrentQueue<CommandRequestOp<TCommand, TRequest>> requestOps) where TCommand : ICommandMetaclass
        {
            while (requestOps.Count > 0)
            {
                if (requestOps.TryDequeue(out var op))
                {
                    connection.SendCommandRequest(metaclass, op.EntityId, op.Request, null, commandParameterNoShortCircuit);
                }
            }
        }*/
        #endregion

        #region Command Responses
        internal static ConcurrentQueue<CommandResponseOp<Harvestable.Commands.GenerateResource, ResourceGenerationReponse>> responseGenerateResourceOps = new ConcurrentQueue<CommandResponseOp<Harvestable.Commands.GenerateResource, ResourceGenerationReponse>>();
        internal static ConcurrentQueue<CommandResponseOp<Harvestable.Commands.ExtractResource, ResourceExtractionReponse>> responseExtractResourceOps = new ConcurrentQueue<CommandResponseOp<Harvestable.Commands.ExtractResource, ResourceExtractionReponse>>();

        static void SendCommandResponses()
        {
            ProccessResponses(Harvestable.Commands.GenerateResource.Metaclass, ref responseGenerateResourceOps);
            ProccessResponses(Harvestable.Commands.ExtractResource.Metaclass, ref responseExtractResourceOps);
        }

        static void ProccessResponses<TCommand, TRequest, TResponse>(ICommandMetaclass<TCommand, TRequest, TResponse> metaclass, ref ConcurrentQueue<CommandResponseOp<TCommand, TResponse>> responseOps) where TCommand : ICommandMetaclass
        {
            while (responseOps.Count > 0)
            {
                if (responseOps.TryDequeue(out var op))
                {
                    var id = new RequestId<IncomingCommandRequest<TCommand>>(op.RequestId.Id);

                    connection.SendCommandResponse(metaclass, id, op.Response.Value);
                }
            }
        }
        #endregion

        #region Logging
        internal static readonly ConcurrentQueue<LogMessage> logMessages = new ConcurrentQueue<LogMessage>();

        static void SendLogMessages()
        {
            while (logMessages.Count > 0)
            {
                if (logMessages.TryDequeue(out var op))
                {
                    //TODO add a worker flag to change at what minimum level logs are sent.

                    connection.SendLogMessage(op.logLevel, op.logger, op.message);
                }
            }
        }
        #endregion
    }
}
