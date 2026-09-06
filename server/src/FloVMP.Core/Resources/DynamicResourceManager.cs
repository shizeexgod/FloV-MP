using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace FloVMP.Core.Resources
{
    public enum ResourceState
    {
        Stopped,
        Starting,
        Running,
        Failed
    }

    public enum ResourceType
    {
        Gamemode,
        Script,
        MapAsset,
        VehiclePack,
        UserInterface
    }

    public record ResourceInfo
    {
        public string Name { get; init; } = string.Empty;
        public ResourceType Type { get; init; } = ResourceType.Script;
        public string Version { get; init; } = "1.0.0";
        public string Author { get; init; } = "FloV:MP";
        public ResourceState State { get; set; } = ResourceState.Stopped;
        public DateTime? StartedAt { get; set; }
        public List<string> Dependencies { get; init; } = new();
    }

    /// <summary>
    /// Dynamic Resource Manager for FloV:MP.
    /// Manages real-time lifecycle of server resources (scripts, maps, cars, UI)
    /// allowing on-the-fly starting, stopping, and restarting from the txAdmin Cloud Control Plane.
    /// </summary>
    public class DynamicResourceManager
    {
        private readonly ConcurrentDictionary<string, ResourceInfo> _resources = new(StringComparer.OrdinalIgnoreCase);
        private readonly object _transitionLock = new();

        public event Action<string, ResourceState, ResourceState>? OnResourceStateChanged;

        public int TotalResources => _resources.Count;
        public int RunningResourcesCount => _resources.Values.Count(r => r.State == ResourceState.Running);

        public void RegisterResource(ResourceInfo info)
        {
            if (info == null || string.IsNullOrWhiteSpace(info.Name)) return;
            _resources[info.Name.Trim()] = info;
        }

        public bool UnregisterResource(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            return _resources.TryRemove(name.Trim(), out _);
        }

        public ResourceInfo? GetResource(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return _resources.TryGetValue(name.Trim(), out var info) ? info : null;
        }

        public IReadOnlyList<ResourceInfo> GetAllResources()
        {
            return _resources.Values.OrderBy(r => r.Name).ToList();
        }

        public bool StartResource(string name, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                message = "Resource name cannot be empty";
                return false;
            }

            lock (_transitionLock)
            {
                if (!_resources.TryGetValue(name.Trim(), out var res))
                {
                    message = $"Resource '{name}' is not registered";
                    return false;
                }

                if (res.State == ResourceState.Running)
                {
                    message = $"Resource '{name}' is already running";
                    return true;
                }

                // Verify dependencies
                foreach (var dep in res.Dependencies)
                {
                    if (!_resources.TryGetValue(dep, out var depInfo) || depInfo.State != ResourceState.Running)
                    {
                        message = $"Dependency '{dep}' must be running before starting '{name}'";
                        return false;
                    }
                }

                var oldState = res.State;
                res.State = ResourceState.Running;
                res.StartedAt = DateTime.UtcNow;

                OnResourceStateChanged?.Invoke(res.Name, oldState, ResourceState.Running);
                message = $"Resource '{name}' started successfully";
                return true;
            }
        }

        public bool StopResource(string name, out string message)
        {
            message = string.Empty;
            if (string.IsNullOrWhiteSpace(name))
            {
                message = "Resource name cannot be empty";
                return false;
            }

            lock (_transitionLock)
            {
                if (!_resources.TryGetValue(name.Trim(), out var res))
                {
                    message = $"Resource '{name}' is not registered";
                    return false;
                }

                if (res.State == ResourceState.Stopped)
                {
                    message = $"Resource '{name}' is already stopped";
                    return true;
                }

                // Check if any running resource depends on this one
                var runningDependents = _resources.Values
                    .Where(r => r.State == ResourceState.Running && r.Dependencies.Contains(res.Name, StringComparer.OrdinalIgnoreCase))
                    .Select(r => r.Name)
                    .ToList();

                if (runningDependents.Count > 0)
                {
                    message = $"Cannot stop '{name}' because active resources depend on it: {string.Join(", ", runningDependents)}";
                    return false;
                }

                var oldState = res.State;
                res.State = ResourceState.Stopped;
                res.StartedAt = null;

                OnResourceStateChanged?.Invoke(res.Name, oldState, ResourceState.Stopped);
                message = $"Resource '{name}' stopped";
                return true;
            }
        }

        public bool RestartResource(string name, out string message)
        {
            if (!StopResource(name, out var stopMsg))
            {
                message = stopMsg;
                return false;
            }

            return StartResource(name, out message);
        }
    }
}
