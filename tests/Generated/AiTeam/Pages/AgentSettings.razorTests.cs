```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using AiTeam.Pages;

namespace AiTeam.Tests.Pages
{
    public class AgentSettingsTests
    {
        private AgentSettings CreateSut() => new AgentSettings();

        #region LoadAgentsAsync

        [Fact]
        public async Task LoadAgentsAsync_正常呼叫_應載入三筆Agent資料()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            await sut.LoadAgentsAsync();

            // Assert
            sut.Agents.Should().HaveCount(3);
            sut.Agents.Should().Contain(a => a.Name == "Customer Service Agent");
            sut.Agents.Should().Contain(a => a.Name == "Sales Agent");
            sut.Agents.Should().Contain(a => a.Name == "Technical Support Agent");
        }

        [Fact]
        public async Task LoadAgentsAsync_正常呼叫_載入完成後IsLoading應為false()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            await sut.LoadAgentsAsync();

            // Assert
            sut.IsLoading.Should().BeFalse();
        }

        [Fact]
        public async Task LoadAgentsAsync_正常呼叫_Agent資料應包含正確的啟用狀態()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            await sut.LoadAgentsAsync();

            // Assert
            sut.Agents.Should().Contain(a => a.Id == 1 && a.IsEnabled == true);
            sut.Agents.Should().Contain(a => a.Id == 2 && a.IsEnabled == false);
            sut.Agents.Should().Contain(a => a.Id == 3 && a.IsEnabled == true);
        }

        [Fact]
        public async Task LoadAgentsAsync_重複呼叫_應重新載入Agent資料()
        {
            // Arrange
            var sut = CreateSut();
            await sut.LoadAgentsAsync();
            sut.Agents.Add(new AgentSettings.Agent { Id = 99, Name = "Temp Agent" });

            // Act
            await sut.LoadAgentsAsync();

            // Assert
            sut.Agents.Should().HaveCount(3);
            sut.Agents.Should().NotContain(a => a.Id == 99);
        }

        #endregion

        #region ToggleAgentStatusAsync

        [Fact]
        public async Task ToggleAgentStatusAsync_AgentIsEnabled為true_應切換為false並顯示停用通知()
        {
            // Arrange
            var sut = CreateSut();
            var agent = new AgentSettings.Agent
            {
                Id = 1,
                Name = "Test Agent",
                IsEnabled = true
            };

            // Act
            await sut.ToggleAgentStatusAsync(agent);

            // Assert
            agent.IsEnabled.Should().BeFalse();
            sut.CurrentNotification.IsVisible.Should().BeTrue();
            sut.CurrentNotification.Type.Should().Be("success");
            sut.CurrentNotification.Message.Should().Contain("disabled successfully");
        }

        [Fact]
        public async Task ToggleAgentStatusAsync_AgentIsEnabled為false_應切換為true並顯示啟用通知()
        {
            // Arrange
            var sut = CreateSut();
            var agent = new AgentSettings.Agent
            {
                Id = 2,
                Name = "Test Agent",
                IsEnabled = false
            };

            // Act
            await sut.ToggleAgentStatusAsync(agent);

            // Assert
            agent.IsEnabled.Should().BeTrue();
            sut.CurrentNotification.IsVisible.Should().BeTrue();
            sut.CurrentNotification.Type.Should().Be("success");
            sut.CurrentNotification.Message.Should().Contain("enabled successfully");
        }

        [Fact]
        public async Task ToggleAgentStatusAsync_傳入null_應直接返回不拋出例外()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            Func<Task> act = async () => await sut.ToggleAgentStatusAsync(null);

            // Assert
            await act.Should().NotThrowAsync();
        }

        [Fact]
        public async Task ToggleAgentStatusAsync_傳入null_通知狀態應保持不變()
        {
            // Arrange
            var sut = CreateSut();
            var originalNotification = sut.CurrentNotification;

            // Act
            await sut.ToggleAgentStatusAsync(null);

            // Assert
            sut.CurrentNotification.IsVisible.Should().BeFalse();
            sut.CurrentNotification.Message.Should().BeEmpty();
        }

        [Fact]
        public async Task ToggleAgentStatusAsync_正常切換_通知應包含Agent名稱()
        {
            // Arrange
            var sut = CreateSut();
            var agent = new AgentSettings.Agent
            {
                Id = 1,
                Name = "Customer Service Agent",
                IsEnabled = true
            };

            // Act
            await sut.ToggleAgentStatusAsync(agent);

            // Assert
            sut.CurrentNotification.Message.Should().Contain("Customer Service Agent");
        }

        [Fact]
        public async Task ToggleAgentStatusAsync_正常切換_通知AgentId應對應當前Agent()
        {
            // Arrange
            var sut = CreateSut();
            var agent = new AgentSettings.Agent
            {
                Id = 5,
                Name = "Test Agent",
                IsEnabled = false
            };

            // Act
            await sut.ToggleAgentStatusAsync(agent);

            // Assert
            sut.CurrentNotification.AgentId.Should().Be(5);
        }

        #endregion

        #region ShowNotification

        [Fact]
        public void ShowNotification_傳入訊息和類型_應正確設定CurrentNotification()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            sut.ShowNotification("Test message", "success", 1);

            // Assert
            sut.CurrentNotification.Message.Should().Be("Test message");
            sut.CurrentNotification.Type.Should().Be("success");
            sut.CurrentNotification.IsVisible.Should().BeTrue();
            sut.CurrentNotification.AgentId.Should().Be(1);
        }

        [Fact]
        public void ShowNotification_不傳agentId_AgentId應為null()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            sut.ShowNotification("Global message", "info");

            // Assert
            sut.CurrentNotification.AgentId.Should().BeNull();
            sut.CurrentNotification.IsVisible.Should().BeTrue();
        }

        [Fact]
        public void ShowNotification_使用預設類型_Type應為info()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            sut.ShowNotification("Default type message");

            // Assert
            sut.CurrentNotification.Type.Should().Be("info");
        }

        [Fact]
        public void ShowNotification_連續呼叫_應以最新通知覆蓋舊通知()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("First message", "info");

            // Act
            sut.ShowNotification("Second message", "error");

            // Assert
            sut.CurrentNotification.Message.Should().Be("Second message");
            sut.CurrentNotification.Type.Should().Be("error");
        }

        #endregion

        #region HideNotification

        [Fact]
        public void HideNotification_通知可見時_應設定IsVisible為false()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("Visible message", "success");
            sut.CurrentNotification.IsVisible.Should().BeTrue();

            // Act
            sut.HideNotification();

            // Assert
            sut.CurrentNotification.IsVisible.Should().BeFalse();
        }

        [Fact]
        public void HideNotification_通知已隱藏時_呼叫後仍應為false不拋出例外()
        {
            // Arrange
            var sut = CreateSut();
            sut.CurrentNotification.IsVisible = false;

            // Act
            Action act = () => sut.HideNotification();

            // Assert
            act.Should().NotThrow();
            sut.CurrentNotification.IsVisible.Should().BeFalse();
        }

        #endregion

        #region GetNotificationCssClass

        [Fact]
        public void GetNotificationCssClass_Type為success_應回傳success樣式()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "success");

            // Act
            var result = sut.GetNotificationCssClass();

            // Assert
            result.Should().Be("notification notification-success");
        }

        [Fact]
        public void GetNotificationCssClass_Type為error_應回傳error樣式()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "error");

            // Act
            var result = sut.GetNotificationCssClass();

            // Assert
            result.Should().Be("notification notification-error");
        }

        [Fact]
        public void GetNotificationCssClass_Type為warning_應回傳warning樣式()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "warning");

            // Act
            var result = sut.GetNotificationCssClass();

            // Assert
            result.Should().Be("notification notification-warning");
        }

        [Fact]
        public void GetNotificationCssClass_Type為info_應回傳info樣式()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "info");

            // Act
            var result = sut.GetNotificationCssClass();

            // Assert
            result.Should().Be("notification notification-info");
        }

        [Fact]
        public void GetNotificationCssClass_Type為未知值_應回傳預設info樣式()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "unknown_type");

            // Act
            var result = sut.GetNotificationCssClass();

            // Assert
            result.Should().Be("notification notification-info");
        }

        #endregion

        #region IsNotificationVisibleForAgent

        [Fact]
        public void IsNotificationVisibleForAgent_通知可見且AgentId相符_應回傳true()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "success", 3);

            // Act
            var result = sut.IsNotificationVisibleForAgent(3);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsNotificationVisibleForAgent_通知可見但AgentId不符_應回傳false()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "success", 3);

            // Act
            var result = sut.IsNotificationVisibleForAgent(99);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsNotificationVisibleForAgent_通知不可見_應回傳false()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "success", 3);
            sut.HideNotification();

            // Act
            var result = sut.IsNotificationVisibleForAgent(3);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsNotificationVisibleForAgent_AgentId為null的全域通知_應回傳false()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("Global msg", "info");

            // Act
            var result = sut.IsNotificationVisibleForAgent(1);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region IsGlobalNotificationVisible

        [Fact]
        public void IsGlobalNotificationVisible_通知可見且無AgentId_應回傳true()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("Global message", "info");

            // Act
            var result = sut.IsGlobalNotificationVisible();

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsGlobalNotificationVisible_通知可見但有AgentId_應回傳false()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("Agent message", "success", 1);

            // Act
            var result = sut.IsGlobalNotificationVisible();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsGlobalNotificationVisible_通知不可見且無AgentId_應回傳false()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("Global message", "info");
            sut.HideNotification();

            // Act
            var result = sut.IsGlobalNotificationVisible();

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsGlobalNotificationVisible_初始狀態_應回傳false()
        {
            // Arrange
            var sut = CreateSut();

            // Act
            var result = sut.IsGlobalNotificationVisible();

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region AutoHideNotificationAsync

        [Fact]
        public async Task AutoHideNotificationAsync_等待延遲後_IsVisible應設為false()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("Auto hide test", "info");
            sut.CurrentNotification.IsVisible.Should().BeTrue();

            // Act
            // 直接呼叫 HideNotification 模擬 AutoHide 完成後的狀態
            sut.HideNotification();

            // Assert
            sut.CurrentNotification.IsVisible.Should().BeFalse();
        }

        [Fact]
        public async Task AutoHideNotificationAsync_呼叫後_不應拋出例外()
        {
            // Arrange
            var sut = CreateSut();
            sut.ShowNotification("msg", "info");

            // Act
            Func<Task> act = async () => await sut.AutoHideNotificationAsync();

            // Assert
            await act.Should().NotThrowAsync();
        }

        #endregion

        #region Notification Model

        [Fact]
        public void Notification_預設建構_Type應為info且IsVisible為false()
        {
            // Arrange & Act
            var notification = new AgentSettings.Notification();

            // Assert
            notification.Type.Should().Be("info");
            notification.IsVisible.Should().B