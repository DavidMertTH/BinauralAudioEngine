using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Code
{
    public class MultiRaycastTest : MonoBehaviour
    {
        [SerializeField] private int rayCount = 5;
        [SerializeField] private float spacing = 2.0f;
        [SerializeField] private float rayLength = 100f;
        [SerializeField] private int maxHitsPerRay = 3;

        private void Update()
        {
            // Allocate raycast commands and results
            using var commands = new NativeArray<RaycastCommand>(rayCount, Allocator.TempJob);
            using var results = new NativeArray<RaycastHit>(rayCount * maxHitsPerRay, Allocator.TempJob);

            // Set up raycast commands
            for (int i = 0; i < rayCount; i++)
            {
                var origin = new Vector3(i * spacing, 0, -10);
                var direction = Vector3.forward;
                var raycastCommands = commands;
                raycastCommands[i] = new RaycastCommand(origin, direction, new QueryParameters(hitMultipleFaces: true));
            }

            // Schedule and complete batch
            var handle = RaycastCommand.ScheduleBatch(commands, results, 1, 3, default);
            handle.Complete();

            // Process results
            for (var i = 0; i < rayCount; i++)
            {
                for (var j = 0; j < maxHitsPerRay; j++)
                {
                    var result = results[i * maxHitsPerRay + j];
                    if (result.collider == null)
                    {
                        Debug.DrawRay(commands[i].from, commands[i].direction * rayLength, Color.green);
                        break;
                    }

                    if (j == maxHitsPerRay - 1)
                    {
                        Debug.DrawLine(commands[i].from, result.point, Color.red);
                    }
                }
            }
        }
    }
}