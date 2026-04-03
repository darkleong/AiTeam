
```csharp
using Xunit;
using DevAgent.Services;

namespace DevAgent.Tests.Generated
{
    public class DevAgentServiceTests
    {
        private readonly DevAgentService _service;

        public DevAgentServiceTests()
        {
            _service = new DevAgentService();
        }

        [Fact]
        public void GetVersion_ReturnsNonNullString()
        {
            // Act
            var version = _service.GetVersion();

            // Assert
            Assert.NotNull(version);
        }

        [Fact]
        public void GetVersion_ReturnsNonEmptyString()
        {
            // Act
            var version = _service.GetVersion();

            // Assert
            Assert.NotEmpty(version);
        }

        [Fact]
        public void GetVersion_ReturnsExpectedVersionFormat()
        {
            // Act
            var version = _service.GetVersion();

            // Assert
            Assert.Matches(@"^\d+\.\d+\.\d+$", version);
        }

        [Fact]
        public void GetVersion_ReturnsConsistentValue()
        {
            // Act
            var version1 = _service.GetVersion();
            var version2 = _service.GetVersion();

            // Assert
            Assert.Equal(version1, version2);
        }

        [Fact]
        public void GetVersion_ReturnsString()
        {
            // Act
            var version = _service.GetVersion();

            // Assert
            Assert.IsType<string>(version);
        }
    }
}
```