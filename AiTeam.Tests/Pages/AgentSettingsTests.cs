
```csharp
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using AiTeam.Pages;
using AiTeam.Services;
using AiTeam.Models;
using Moq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace AiTeam.Tests.Pages
{
    public class AgentSettingsTests : TestContext
    {
        private readonly Mock<IAgentService> _mockAgentService;

        public AgentSettingsTests()
        {
            _mockAgentService = new Mock<IAgentService>();
            Services.AddSingleton(_mockAgentService.Object);
        }

        private AgentModel CreateTestAgent(bool isEnabled = true)
        {
            return new AgentModel
            {
                Id = "test-agent-1",
                Name = "Test Agent",
                Description = "Test Description",
                IsEnabled = isEnabled
            };
        }

        [Fact]
        public void AgentSettings_WhenLoaded_ShouldDisplayAgentList()
        {
            // Arrange
            var agents = new List<AgentModel>
            {
                CreateTestAgent(true),
                CreateTestAgent(false)
            };
            agents[1].Id = "test-agent-2";
            agents[1].Name = "Test Agent 2";

            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);

            // Act
            var component = RenderComponent<AgentSettings>();

            // Assert
            var agentItems = component.FindAll(".agent-item");
            Assert.Equal(2, agentItems.Count);
        }

        [Fact]
        public void AgentSettings_WhenAgentEnabled_ShouldShowEnabledStatus()
        {
            // Arrange
            var agents = new List<AgentModel> { CreateTestAgent(true) };
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);

            // Act
            var component = RenderComponent<AgentSettings>();

            // Assert
            var toggleButton = component.Find(".toggle-button");
            Assert.Contains("enabled", toggleButton.ClassList);
        }

        [Fact]
        public void AgentSettings_WhenAgentDisabled_ShouldShowDisabledStatus()
        {
            // Arrange
            var agents = new List<AgentModel> { CreateTestAgent(false) };
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);

            // Act
            var component = RenderComponent<AgentSettings>();

            // Assert
            var toggleButton = component.Find(".toggle-button");
            Assert.DoesNotContain("enabled", toggleButton.ClassList);
        }

        [Fact]
        public async Task AgentSettings_WhenToggleEnabled_ShouldShowSuccessMessage()
        {
            // Arrange
            var agent = CreateTestAgent(false);
            var agents = new List<AgentModel> { agent };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent.Id, true))
                .ReturnsAsync(true);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButton = component.Find(".toggle-button");
            await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            var notification = component.Find(".notification-area");
            Assert.NotNull(notification);
            
            var message = component.Find(".notification-message");
            Assert.NotNull(message);
            Assert.Contains("啟用", message.TextContent);
        }

        [Fact]
        public async Task AgentSettings_WhenToggleDisabled_ShouldShowSuccessMessage()
        {
            // Arrange
            var agent = CreateTestAgent(true);
            var agents = new List<AgentModel> { agent };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent.Id, false))
                .ReturnsAsync(true);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButton = component.Find(".toggle-button");
            await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            var notification = component.Find(".notification-area");
            Assert.NotNull(notification);
            
            var message = component.Find(".notification-message");
            Assert.NotNull(message);
            Assert.Contains("停用", message.TextContent);
        }

        [Fact]
        public async Task AgentSettings_NotificationMessage_ShouldBeInCorrectPosition()
        {
            // Arrange
            var agent = CreateTestAgent(false);
            var agents = new List<AgentModel> { agent };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent.Id, true))
                .ReturnsAsync(true);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButton = component.Find(".toggle-button");
            await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert - Verify notification is inside the notification-area, not elsewhere
            var notificationArea = component.Find(".notification-area");
            Assert.NotNull(notificationArea);
            
            // Verify the message is a child of notification-area (correct position)
            var messageInNotificationArea = notificationArea.QuerySelector(".notification-message");
            Assert.NotNull(messageInNotificationArea);
            
            // Verify there's no stray notification outside the notification-area
            var allMessages = component.FindAll(".notification-message");
            Assert.Single(allMessages);
        }

        [Fact]
        public async Task AgentSettings_NotificationMessage_ShouldBeNearToggleButton()
        {
            // Arrange
            var agent = CreateTestAgent(false);
            var agents = new List<AgentModel> { agent };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent.Id, true))
                .ReturnsAsync(true);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButton = component.Find(".toggle-button");
            await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert - Verify the agent-item contains both toggle button and status message
            var agentItem = component.Find(".agent-item");
            Assert.NotNull(agentItem);
            
            var toggleInItem = agentItem.QuerySelector(".toggle-button");
            Assert.NotNull(toggleInItem);
            
            // The notification should be in agent-controls section near the toggle
            var agentControls = agentItem.QuerySelector(".agent-controls");
            Assert.NotNull(agentControls);
            
            var notificationInControls = agentControls.QuerySelector(".status-message");
            Assert.NotNull(notificationInControls);
        }

        [Fact]
        public async Task AgentSettings_WhenToggleFails_ShouldShowErrorMessage()
        {
            // Arrange
            var agent = CreateTestAgent(false);
            var agents = new List<AgentModel> { agent };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent.Id, true))
                .ReturnsAsync(false);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButton = component.Find(".toggle-button");
            await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert
            var errorMessage = component.Find(".error-message");
            Assert.NotNull(errorMessage);
            Assert.Contains("失敗", errorMessage.TextContent);
        }

        [Fact]
        public async Task AgentSettings_SuccessMessage_ShouldHaveCorrectCssClass()
        {
            // Arrange
            var agent = CreateTestAgent(false);
            var agents = new List<AgentModel> { agent };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent.Id, true))
                .ReturnsAsync(true);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButton = component.Find(".toggle-button");
            await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert - Success message should have 'success' CSS class for proper styling
            var statusMessage = component.Find(".status-message");
            Assert.Contains("success", statusMessage.ClassList);
        }

        [Fact]
        public async Task AgentSettings_ErrorMessage_ShouldHaveCorrectCssClass()
        {
            // Arrange
            var agent = CreateTestAgent(false);
            var agents = new List<AgentModel> { agent };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent.Id, true))
                .ReturnsAsync(false);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButton = component.Find(".toggle-button");
            await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert - Error message should have 'error' CSS class for proper styling
            var statusMessage = component.Find(".status-message");
            Assert.Contains("error", statusMessage.ClassList);
        }

        [Fact]
        public void AgentSettings_InitialState_ShouldNotShowNotification()
        {
            // Arrange
            var agents = new List<AgentModel> { CreateTestAgent(true) };
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);

            // Act
            var component = RenderComponent<AgentSettings>();

            // Assert - No notification should be visible on initial load
            var notifications = component.FindAll(".status-message");
            Assert.Empty(notifications);
        }

        [Fact]
        public async Task AgentSettings_MultipleAgents_NotificationShouldAppearForCorrectAgent()
        {
            // Arrange
            var agent1 = CreateTestAgent(false);
            var agent2 = new AgentModel
            {
                Id = "test-agent-2",
                Name = "Test Agent 2",
                IsEnabled = true
            };
            
            var agents = new List<AgentModel> { agent1, agent2 };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent1.Id, true))
                .ReturnsAsync(true);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButtons = component.FindAll(".toggle-button");
            await toggleButtons[0].ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert - Only the first agent item should show notification
            var agentItems = component.FindAll(".agent-item");
            
            var firstAgentMessage = agentItems[0].QuerySelector(".status-message");
            Assert.NotNull(firstAgentMessage);
            
            var secondAgentMessage = agentItems[1].QuerySelector(".status-message");
            Assert.Null(secondAgentMessage);
        }

        [Fact]
        public async Task AgentSettings_NotificationArea_ShouldHaveProperAriaAttributes()
        {
            // Arrange
            var agent = CreateTestAgent(false);
            var agents = new List<AgentModel> { agent };
            
            _mockAgentService.Setup(s => s.GetAgentsAsync())
                .ReturnsAsync(agents);
            _mockAgentService.Setup(s => s.UpdateAgentStatusAsync(agent.Id, true))
                .ReturnsAsync(true);

            // Act
            var component = RenderComponent<AgentSettings>();
            var toggleButton = component.Find(".toggle-button");
            await toggleButton.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

            // Assert - Notification should have aria-live for accessibility
            var notification = component.Find(".notification-area");
            Assert.NotNull(notification);
            Assert.Equal("polite", notification.GetAttribute("aria-live"));
        }
    }
}
```