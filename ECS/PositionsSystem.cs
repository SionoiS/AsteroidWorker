using Improbable;
using Improbable.Worker;
using System.Collections.Generic;

namespace AsteroidWorker.ECS
{
    static class PositionsSystem
    {
        static readonly Dispatcher dispatcher = new Dispatcher();

        static PositionsSystem()
        {
            dispatcher.OnAddComponent(Position.Metaclass, OnComponentAdded);
            dispatcher.OnRemoveComponent(Position.Metaclass, OnComponentRemoved);
        }

        internal static void ProccessOpList(OpList opList)
        {
            dispatcher.Process(opList);
        }

        static void OnComponentAdded(AddComponentOp<PositionData> op)
        {
            components[op.EntityId.Id] = op.Data;
        }

        static void OnComponentRemoved(RemoveComponentOp op)
        {
            components.TryRemove(op.EntityId.Id, out var _);
        }

        static readonly Dictionary<long, PositionData> components = new Dictionary<long, PositionData>();

        internal static bool TryGetComponent(long entityId, out PositionData position)
        {
            return components.TryGetValue(entityId, out position);
        }

        internal static PositionData GetComponent(long entityId)
        {
            return components[entityId];
        }
    }
}
