using System.Collections.Generic;
using FloVMP.Core.Resources;
using Xunit;

namespace FloVMP.Core.Tests
{
    public class DynamicResourceManagerTests
    {
        [Fact]
        public void StartAndStopResource_TransitionsStatesCorrectly()
        {
            var manager = new DynamicResourceManager();
            manager.RegisterResource(new ResourceInfo
            {
                Name = "flovmp-chat",
                Type = ResourceType.Script,
                State = ResourceState.Stopped
            });

            bool started = manager.StartResource("flovmp-chat", out var startMsg);
            Assert.True(started);
            Assert.Contains("started successfully", startMsg);
            Assert.Equal(ResourceState.Running, manager.GetResource("flovmp-chat")?.State);

            bool stopped = manager.StopResource("flovmp-chat", out var stopMsg);
            Assert.True(stopped);
            Assert.Contains("stopped", stopMsg);
            Assert.Equal(ResourceState.Stopped, manager.GetResource("flovmp-chat")?.State);
        }

        [Fact]
        public void StartResource_WithUnmetDependencies_FailsWithWarning()
        {
            var manager = new DynamicResourceManager();
            manager.RegisterResource(new ResourceInfo
            {
                Name = "flovmp-core",
                Type = ResourceType.Gamemode,
                State = ResourceState.Stopped
            });

            manager.RegisterResource(new ResourceInfo
            {
                Name = "flovmp-inventory",
                Type = ResourceType.Script,
                State = ResourceState.Stopped,
                Dependencies = new List<string> { "flovmp-core" }
            });

            // Trying to start inventory when core is stopped should fail
            bool started = manager.StartResource("flovmp-inventory", out var msg);
            Assert.False(started);
            Assert.Contains("must be running before starting", msg);

            // Start core first
            Assert.True(manager.StartResource("flovmp-core", out _));

            // Now inventory starts cleanly
            Assert.True(manager.StartResource("flovmp-inventory", out _));
        }

        [Fact]
        public void StopResource_WhenDependedUponByActiveResource_IsBlocked()
        {
            var manager = new DynamicResourceManager();
            manager.RegisterResource(new ResourceInfo { Name = "base-lib", State = ResourceState.Running });
            manager.RegisterResource(new ResourceInfo
            {
                Name = "gameplay",
                State = ResourceState.Running,
                Dependencies = new List<string> { "base-lib" }
            });

            bool stopped = manager.StopResource("base-lib", out var msg);
            Assert.False(stopped);
            Assert.Contains("active resources depend on it: gameplay", msg);
        }

        [Fact]
        public void RestartResource_PerformsCleanCycle()
        {
            var manager = new DynamicResourceManager();
            manager.RegisterResource(new ResourceInfo { Name = "hud", State = ResourceState.Running });

            bool restarted = manager.RestartResource("hud", out var msg);
            Assert.True(restarted);
            Assert.Equal(ResourceState.Running, manager.GetResource("hud")?.State);
        }
    }
}
